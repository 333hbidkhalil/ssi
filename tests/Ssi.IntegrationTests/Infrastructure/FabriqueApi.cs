using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Ssi.Contracts;

namespace Ssi.IntegrationTests.Infrastructure;

/// <summary>
/// Démarre l'API réelle sur une base PostgreSQL dédiée (créée par les migrations) pour chaque classe de tests.
/// Serveur de test : variable SSI_TEST_PG (défaut : Host=localhost;Username=postgres).
/// </summary>
public sealed class FabriqueApi : WebApplicationFactory<Program>, IAsyncLifetime
{
    public static readonly string Serveur = Environment.GetEnvironmentVariable("SSI_TEST_PG") ?? "Host=localhost;Port=5432;Username=postgres";
    public string NomBase { get; } = $"ssi_test_{Guid.NewGuid():N}"[..30];
    public string ChaineConnexion => $"{Serveur};Database={NomBase};Include Error Detail=true";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Ssi", ChaineConnexion);
        builder.UseSetting("Ssi:MotDePasseAdminInitial", "Admin2026!");
        builder.UseSetting("Ssi:DossierLogs", Path.Combine(Path.GetTempPath(), "ssi-tests-logs"));
        builder.UseSetting("Serilog:MinimumLevel:Default", "Warning");
    }

    public Task InitializeAsync()
    {
        _ = Server; // démarre l'hôte : migrations + données de référence
        return Task.CompletedTask;
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        await using var c = new NpgsqlConnection($"{Serveur};Database=postgres");
        await c.OpenAsync();
        await using var cmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS {NomBase} WITH (FORCE)", c);
        await cmd.ExecuteNonQueryAsync();
    }

    public ClientApi Client() => new(CreateClient());

    private readonly SemaphoreSlim verrouDemo = new(1, 1);
    private JeuDemo? demo;

    /// <summary>Jeu de démonstration créé une seule fois par base de test.</summary>
    public async Task<JeuDemo> DemoAsync()
    {
        await verrouDemo.WaitAsync();
        try { return demo ??= await JeuDemo.CreerAsync(this); }
        finally { verrouDemo.Release(); }
    }

    public async Task<ClientApi> ConnecterAsync(string login, string motDePasse)
    {
        var c = Client();
        await c.ConnecterAsync(login, motDePasse);
        return c;
    }

    /// <summary>Exécute une requête SQL scalaire directement sur la base de test (contrôles).</summary>
    public async Task<T> ScalaireAsync<T>(string sql)
    {
        await using var c = new NpgsqlConnection(ChaineConnexion);
        await c.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, c);
        var r = await cmd.ExecuteScalarAsync();
        return r is null or DBNull ? default! : (T)Convert.ChangeType(r, typeof(T));
    }

    public async Task<int> ExecuterAsync(string sql)
    {
        await using var c = new NpgsqlConnection(ChaineConnexion);
        await c.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, c);
        return await cmd.ExecuteNonQueryAsync();
    }
}

public sealed class ReponseApi(HttpStatusCode statut, string corps)
{
    public HttpStatusCode Statut { get; } = statut;
    public string Corps { get; } = corps;
    public T Lire<T>() => JsonSerializer.Deserialize<T>(Corps, ClientApi.Json)!;
    public override string ToString() => $"{(int)Statut} {Corps}";
}

public sealed class ClientApi(HttpClient http)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    public HttpClient Http { get; } = http;
    public UtilisateurConnecteDto? Utilisateur { get; private set; }

    public async Task ConnecterAsync(string login, string motDePasse)
    {
        var r = await EnvoyerAsync(HttpMethod.Post, "/api/auth/connexion", new ConnexionDto(login, motDePasse));
        if (r.Statut != HttpStatusCode.OK) throw new InvalidOperationException($"Connexion {login} impossible : {r}");
        var s = r.Lire<SessionDto>();
        Http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", s.Jeton);
        Utilisateur = s.Utilisateur;
    }

    public async Task<ReponseApi> EnvoyerAsync(HttpMethod methode, string url, object? corps = null, string? cleIdempotence = null)
    {
        using var req = new HttpRequestMessage(methode, url);
        if (corps is not null) req.Content = JsonContent.Create(corps, corps.GetType(), options: Json);
        if (cleIdempotence is not null) req.Headers.Add("Idempotency-Key", cleIdempotence);
        using var rep = await Http.SendAsync(req);
        return new ReponseApi(rep.StatusCode, await rep.Content.ReadAsStringAsync());
    }

    public Task<ReponseApi> GetAsync(string url) => EnvoyerAsync(HttpMethod.Get, url);
    public Task<ReponseApi> PostAsync(string url, object? corps = null, string? cle = null) => EnvoyerAsync(HttpMethod.Post, url, corps ?? new { }, cle);
    public Task<ReponseApi> PutAsync(string url, object corps) => EnvoyerAsync(HttpMethod.Put, url, corps);
    public Task<ReponseApi> DeleteAsync(string url) => EnvoyerAsync(HttpMethod.Delete, url);

    /// <summary>Appel qui doit réussir (2xx) ; renvoie le corps désérialisé.</summary>
    public async Task<T> OkAsync<T>(Task<ReponseApi> appel)
    {
        var r = await appel;
        if ((int)r.Statut is < 200 or >= 300) throw new Xunit.Sdk.XunitException($"Appel en échec : {r}");
        return r.Lire<T>();
    }

    public async Task OkAsync(Task<ReponseApi> appel)
    {
        var r = await appel;
        if ((int)r.Statut is < 200 or >= 300) throw new Xunit.Sdk.XunitException($"Appel en échec : {r}");
    }

    public Task<T> GetOkAsync<T>(string url) => OkAsync<T>(GetAsync(url));
    public Task<T> PostOkAsync<T>(string url, object? corps = null, string? cle = null) => OkAsync<T>(PostAsync(url, corps, cle));
    public Task<T> PutOkAsync<T>(string url, object corps) => OkAsync<T>(PutAsync(url, corps));
}
