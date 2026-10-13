using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using Ssi.Contracts;

// Usage : Ssi.RecetteWindows <client.exe> <adresse API> <mot de passe admin initial> <dossier de sortie>
// 1. Première connexion de l'administrateur par l'écran (changement de mot de passe obligatoire).
// 2. Jeu de données minimal créé par l'API (prix, cuve, pistolet, poste validé, article vendu, tiers).
// 3. Ouverture de chaque entrée du menu et de chaque onglet : capture, bandeau d'erreur, fenêtre d'erreur inattendue.
if (args.Length < 4) { Console.Error.WriteLine("Usage : Ssi.RecetteWindows <client.exe> <adresse API> <mot de passe admin> <dossier>"); return 2; }
var (exe, adresse, mdpInitial, sortie) = (args[0], args[1].TrimEnd('/'), args[2], args[3]);
const string NouveauMdp = "Recette-SSI-2026";
Directory.CreateDirectory(sortie);
var lignes = new List<(string Ecran, string Statut, string Detail)>();
var numero = 0;

using var automation = new UIA3Automation();
var app = Application.Launch(exe);
try
{
    var fenetre = app.GetMainWindow(automation, TimeSpan.FromSeconds(60)) ?? throw new InvalidOperationException("Fenêtre principale introuvable.");
    fenetre.Patterns.Window.PatternOrDefault?.SetWindowVisualState(WindowVisualState.Maximized);
    Attendre(1500);
    Capturer("connexion");

    // Connexion puis changement du mot de passe initial, comme un utilisateur.
    Saisir(Trouver("ChampLogin"), "admin");
    Saisir(Trouver("MotDePasse"), mdpInitial);
    Bouton("Se connecter").Invoke();
    Attendre(3000);
    Capturer("changement-mot-de-passe");
    Saisir(Trouver("Ancien"), mdpInitial);
    Saisir(Trouver("Nouveau"), NouveauMdp);
    Saisir(Trouver("Confirmation"), NouveauMdp);
    Bouton("Changer le mot de passe").Invoke();
    Attendre(4000);
    var menu = Attendu(() => fenetre.FindFirstDescendant(cf => cf.ByControlType(ControlType.List))?.AsListBox(), "menu principal");
    lignes.Add(("Connexion et changement du mot de passe initial", "OK", ""));

    await JeuDeDonneesAsync();

    var entrees = menu.Items.Select(i => i.Name).ToList();
    Console.WriteLine($"Menu : {string.Join(" | ", entrees)}");
    foreach (var libelle in entrees)
    {
        var item = menu.Items.First(i => i.Name == libelle);
        item.Select();
        Attendre(2500);
        Bouton("Actualiser (F5)").Invoke();   // données créées après l'ouverture de session
        Attendre(2500);
        Controler(libelle);
        // Chaque onglet de l'écran
        var onglets = fenetre.FindAllDescendants(cf => cf.ByControlType(ControlType.TabItem)).Select(t => t.Name).ToList();
        foreach (var onglet in onglets.Skip(1))
        {
            var t = fenetre.FindAllDescendants(cf => cf.ByControlType(ControlType.TabItem)).FirstOrDefault(x => x.Name == onglet);
            if (t is null) continue;
            t.AsTabItem().Select();
            Attendre(1500);
            Controler($"{libelle} › {onglet}");
        }
    }

    void Controler(string ecran)
    {
        var detail = new List<string>();
        foreach (var w in app.GetAllTopLevelWindows(automation).Where(w => w.Properties.ProcessId == app.ProcessId && !w.Equals(fenetre)))
        {
            var texte = string.Join(" ", w.FindAllDescendants(cf => cf.ByControlType(ControlType.Text)).Select(x => x.Name));
            Capture.Element(w).ToFile(Path.Combine(sortie, $"{++numero:00}-fenetre-{Nom(ecran)}.png"));
            detail.Add($"Fenêtre « {w.Title} » : {texte}");
            w.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button))?.AsButton().Invoke();
            Attendre(500);
        }
        var bandeau = fenetre.FindFirstDescendant(cf => cf.ByAutomationId("BandeauErreur"));
        if (bandeau is not null && !bandeau.IsOffscreen && !string.IsNullOrWhiteSpace(bandeau.Name)) detail.Add($"Bandeau d'erreur : {bandeau.Name}");
        Capturer(ecran);
        lignes.Add((ecran, detail.Count == 0 ? "OK" : "ERREUR", string.Join(" / ", detail)));
        Console.WriteLine($"{ecran} : {(detail.Count == 0 ? "OK" : string.Join(" / ", detail))}");
    }

    void Capturer(string nom) => Capture.Element(fenetre).ToFile(Path.Combine(sortie, $"{++numero:00}-{Nom(nom)}.png"));

    AutomationElement Trouver(string id) => Attendu(() => fenetre.FindFirstDescendant(cf => cf.ByAutomationId(id)), id);
    FlaUI.Core.AutomationElements.Button Bouton(string nom) => Attendu(() => fenetre.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button).And(cf.ByName(nom)))?.AsButton(), nom);
}
catch (Exception ex)
{
    lignes.Add(("Pilotage", "ERREUR", ex.Message.Replace('\n', ' ')));
    Console.Error.WriteLine(ex);
    try { Capture.Screen().ToFile(Path.Combine(sortie, $"{++numero:00}-ecran-au-moment-de-l-erreur.png")); } catch { }
}
finally
{
    app.Close();
    try { app.Kill(); } catch { }
}

var rapport = new StringBuilder("# Recette du client Windows\n\n| Écran | Statut | Détail |\n|---|---|---|\n");
foreach (var (e, s, d) in lignes) rapport.AppendLine($"| {e} | {s} | {d.Replace("|", "/")} |");
File.WriteAllText(Path.Combine(sortie, "rapport.md"), rapport.ToString());
Console.WriteLine(rapport);
return lignes.Any(l => l.Statut != "OK") ? 1 : 0;

static void Attendre(int ms) => Thread.Sleep(ms);

static string Nom(string s) => new string(s.ToLowerInvariant().Normalize(NormalizationForm.FormD)
    .Where(c => char.IsLetterOrDigit(c) || c == ' ').ToArray()).Trim().Replace(' ', '-');

static T Attendu<T>(Func<T?> recherche, string quoi) where T : class
{
    for (var i = 0; i < 40; i++) { if (recherche() is { } r) return r; Thread.Sleep(500); }
    throw new InvalidOperationException($"Élément introuvable à l'écran : {quoi}");
}

static void Saisir(AutomationElement champ, string texte)
{
    champ.Focus();
    Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
    Keyboard.Type(texte);
    Thread.Sleep(200);
}

async Task JeuDeDonneesAsync()
{
    var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    using var http = new HttpClient { BaseAddress = new Uri(adresse) };
    var session = await Lire<SessionDto>(await http.PostAsJsonAsync("/api/auth/connexion", new ConnexionDto("admin", NouveauMdp), json));
    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.Jeton);

    async Task<T> Lire<T>(HttpResponseMessage r)
    {
        var corps = await r.Content.ReadAsStringAsync();
        if (!r.IsSuccessStatusCode) throw new InvalidOperationException($"{r.RequestMessage?.RequestUri} : {(int)r.StatusCode} {corps}");
        return JsonSerializer.Deserialize<T>(corps, json)!;
    }
    Task<T> Get<T>(string url) => http.GetAsync(url).ContinueWith(t => Lire<T>(t.Result)).Unwrap();
    async Task<T> Post<T>(string url, object? corps = null)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(corps ?? new { }, corps?.GetType() ?? typeof(object), options: json) };
        req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return await Lire<T>(await http.SendAsync(req));
    }

    var debut = DateTimeOffset.Now.Date.AddDays(-2);
    var produits = await Get<List<ProduitDto>>("/api/produits");
    var go = produits.Single(p => p.Code == "GO");
    var sp = produits.Single(p => p.Code == "SP");
    await Post<List<PrixDto>>($"/api/produits/{go.Id}/prix", new NouveauPrixDto(12.85m, debut, "Prix de recette"));
    await Post<List<PrixDto>>($"/api/produits/{sp.Id}/prix", new NouveauPrixDto(14.20m, debut, "Prix de recette"));
    var cuveGo = await Post<CuveDto>("/api/equipements/cuves", new EnregistrementCuveDto("C1-GO", go.Id, 40_000m, 3_000m, 100m, 0.5m, true, null));
    var cuveSp = await Post<CuveDto>("/api/equipements/cuves", new EnregistrementCuveDto("C2-SP", sp.Id, 30_000m, 2_000m, 100m, 0.5m, true, null));
    await Post<MouvementStockDto>("/api/stocks/initial", new StockInitialDto(go.Id, cuveGo.Id, 20_000m, 11.20m, null, "Stock de démarrage"));
    await Post<MouvementStockDto>("/api/stocks/initial", new StockInitialDto(sp.Id, cuveSp.Id, 15_000m, 12.40m, null, "Stock de démarrage"));
    var v1 = await Post<VolucompteurDto>("/api/equipements/volucompteurs", new EnregistrementVolucompteurDto("V1", "Îlot 1", true));
    var pGo = await Post<PistoletDto>($"/api/equipements/volucompteurs/{v1.Id}/pistolets", new EnregistrementPistoletDto("GO", go.Id, cuveGo.Id, 999_999.99m, 125_430.50m, true));
    var pSp = await Post<PistoletDto>($"/api/equipements/volucompteurs/{v1.Id}/pistolets", new EnregistrementPistoletDto("SP", sp.Id, cuveSp.Id, 999_999.99m, 80_000m, true));

    var poste = await Post<PosteDetailDto>("/api/postes", new OuverturePosteDto(null, null, "Poste de recette"));
    var prep = await Get<List<PreparationReleveDto>>($"/api/postes/{poste.Poste.Id}/preparation");
    foreach (var (p, volume) in new[] { (pGo, 720m), (pSp, 400m) })
        await Post<ResultatReleveDto>($"/api/postes/{poste.Poste.Id}/releves",
            new SaisieReleveDto(p.Id, null, prep.Single(x => x.PistoletId == p.Id).IndexDebutPropose + volume, null, null, true, null));
    await Post<PosteDetailDto>($"/api/postes/{poste.Poste.Id}/cloture");
    await Post<PosteDetailDto>($"/api/postes/{poste.Poste.Id}/validation");

    var familles = await Get<List<FamilleDto>>("/api/produits/familles");
    var taxes = await Get<List<TaxeDto>>("/api/admin/taxes");
    var huile = await Post<ProduitDto>("/api/produits", new EnregistrementProduitDto("LUB-15W40", "Huile 15W40 5 L", familles.Single(f => f.Code == "LUB").Id,
        "U", "Afriquia", "5 L", taxes.Single(t => t.Code == "TVA_NORMAL").Id, 5m, null, true));
    await Post<List<PrixDto>>($"/api/produits/{huile.Id}/prix", new NouveauPrixDto(240m, debut, "Prix catalogue"));
    await Post<MouvementStockDto>("/api/stocks/initial", new StockInitialDto(huile.Id, null, 24m, 150m, null, "Inventaire de départ"));
    await Post<VenteDto>("/api/ventes", new CreationVenteDto(null, ModeReglement.Especes, null, [new LigneVenteSaisieDto(huile.Id, 2m, null)]));
    await Post<ClientDto>("/api/clients", new EnregistrementClientDto("C-ATLAS", "Transports Atlas", TypeClient.Credit, "001234567000089", null, null,
        null, "Fès", null, null, null, 30, 50_000m, true, null));
    await Post<FournisseurDto>("/api/fournisseurs", new EnregistrementFournisseurDto("F-NORD", "Pétrolière du Nord", null, null, null, null, "Casablanca",
        null, null, null, 30, null, "GO, SP", true, null));
    lignes.Add(("Jeu de données de recette (API)", "OK", "prix, 2 cuves, 2 pistolets, 1 poste validé, 1 article vendu, 1 client, 1 fournisseur"));
}
