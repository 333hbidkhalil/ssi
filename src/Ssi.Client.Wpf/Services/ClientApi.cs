using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ssi.Contracts;

namespace Ssi.Client.Wpf.Services;

/// <summary>Erreur renvoyée par le serveur, avec le message métier en français (ProblemDetails).</summary>
public sealed class ErreurApi(HttpStatusCode statut, string titre, string message, string? code) : Exception(message)
{
    public HttpStatusCode Statut { get; } = statut;
    public string Titre { get; } = titre;
    public string? Code { get; } = code;
}

/// <summary>
/// Accès HTTP à l'API SSI. Le client ne parle jamais directement à la base : toutes les règles sont appliquées par le serveur.
/// Les opérations sensibles portent une clé d'idempotence : un double-clic ou une nouvelle tentative après coupure ne crée rien en double.
/// </summary>
public sealed class ClientApi(HttpClient http, Session session)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public Uri Adresse => http.BaseAddress!;

    private async Task<HttpResponseMessage> EnvoyerAsync(HttpMethod methode, string url, object? corps, string? cleIdempotence, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(methode, url);
        if (session.Jeton is { } jeton) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jeton);
        if (corps is not null) req.Content = JsonContent.Create(corps, corps.GetType(), options: Json);
        if (cleIdempotence is not null) req.Headers.Add("Idempotency-Key", cleIdempotence);
        HttpResponseMessage rep;
        try { rep = await http.SendAsync(req, ct); }
        catch (HttpRequestException ex)
        {
            throw new ErreurApi(HttpStatusCode.ServiceUnavailable, "Serveur injoignable",
                $"Le serveur SSI ({http.BaseAddress}) ne répond pas. Vérifiez le réseau ; aucune opération n'a été enregistrée. ({ex.Message})", null);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new ErreurApi(HttpStatusCode.RequestTimeout, "Délai dépassé",
                "Le serveur n'a pas répondu à temps. Si vous relancez la même action, elle ne sera pas enregistrée deux fois.", null);
        }
        if (rep.IsSuccessStatusCode) return rep;
        if (rep.StatusCode == HttpStatusCode.Unauthorized) session.Expirer();
        var texte = await rep.Content.ReadAsStringAsync(ct);
        string titre = rep.ReasonPhrase ?? "Erreur", message = texte, code = "";
        try
        {
            using var doc = JsonDocument.Parse(texte);
            var r = doc.RootElement;
            titre = r.TryGetProperty("title", out var t) ? t.GetString() ?? titre : titre;
            message = r.TryGetProperty("detail", out var d) ? d.GetString() ?? titre : titre;
            // « erreurs » : détail par champ renvoyé par SSI ; « errors » : validation automatique d'ASP.NET Core.
            if ((r.TryGetProperty("erreurs", out var e) || r.TryGetProperty("errors", out e)) && e.ValueKind == JsonValueKind.Object)
                message += Environment.NewLine + string.Join(Environment.NewLine, e.EnumerateObject().SelectMany(p => p.Value.EnumerateArray().Select(v => $"• {v.GetString()}")));
            code = r.TryGetProperty("code", out var c) ? c.GetString() ?? "" : "";
        }
        catch (JsonException) { }
        rep.Dispose();
        throw new ErreurApi(rep.StatusCode, titre, message, code);
    }

    public async Task<T> GetAsync<T>(string url, CancellationToken ct = default)
    {
        using var rep = await EnvoyerAsync(HttpMethod.Get, url, null, null, ct);
        return (await rep.Content.ReadFromJsonAsync<T>(Json, ct))!;
    }

    public async Task<T> PostAsync<T>(string url, object? corps = null, string? cleIdempotence = null, CancellationToken ct = default)
    {
        using var rep = await EnvoyerAsync(HttpMethod.Post, url, corps ?? new { }, cleIdempotence, ct);
        return rep.StatusCode == HttpStatusCode.NoContent ? default! : (await rep.Content.ReadFromJsonAsync<T>(Json, ct))!;
    }

    public async Task PostAsync(string url, object? corps = null, string? cleIdempotence = null, CancellationToken ct = default)
    {
        using var _ = await EnvoyerAsync(HttpMethod.Post, url, corps ?? new { }, cleIdempotence, ct);
    }

    public async Task<T> PutAsync<T>(string url, object corps, CancellationToken ct = default)
    {
        using var rep = await EnvoyerAsync(HttpMethod.Put, url, corps, null, ct);
        return (await rep.Content.ReadFromJsonAsync<T>(Json, ct))!;
    }

    public async Task DeleteAsync(string url, CancellationToken ct = default)
    {
        using var _ = await EnvoyerAsync(HttpMethod.Delete, url, null, null, ct);
    }

    /// <summary>Télécharge un fichier (export, PDF) dans Documents\SSI et renvoie son chemin.</summary>
    public async Task<string> TelechargerAsync(string url, CancellationToken ct = default)
    {
        using var rep = await EnvoyerAsync(HttpMethod.Get, url, null, null, ct);
        var nom = rep.Content.Headers.ContentDisposition?.FileNameStar ?? rep.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? $"ssi-{DateTime.Now:yyyyMMddHHmmss}";
        var dossier = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SSI");
        Directory.CreateDirectory(dossier);
        var chemin = Path.Combine(dossier, string.Concat(nom.Split(Path.GetInvalidFileNameChars())));
        await File.WriteAllBytesAsync(chemin, await rep.Content.ReadAsByteArrayAsync(ct), ct);
        return chemin;
    }

    public async Task ConnecterAsync(string login, string motDePasse, CancellationToken ct = default)
    {
        var s = await PostAsync<SessionDto>("/api/auth/connexion", new ConnexionDto(login, motDePasse), null, ct);
        session.Ouvrir(s);
    }
}

/// <summary>Utilisateur connecté et permissions effectives (rechargées par le serveur à chaque requête ; ici pour l'affichage).</summary>
public sealed class Session
{
    public string? Jeton { get; private set; }
    public UtilisateurConnecteDto? Utilisateur { get; private set; }
    public event Action? Expiree;

    public void Ouvrir(SessionDto s) { Jeton = s.Jeton; Utilisateur = s.Utilisateur; }
    public void Fermer() { Jeton = null; Utilisateur = null; }
    public void Expirer() { if (Jeton is null) return; Fermer(); Expiree?.Invoke(); }

    /// <summary>Affichage seulement : le serveur contrôle chaque appel quelle que soit l'interface.</summary>
    public bool A(params string[] permissions) => Utilisateur is { } u && permissions.Any(p => u.Permissions.Contains(p));
}
