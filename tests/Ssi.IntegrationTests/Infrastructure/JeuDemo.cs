using Ssi.Contracts;

namespace Ssi.IntegrationTests.Infrastructure;

/// <summary>Jeu de données « Station Démo » décrit dans docs/06-tests-acceptation.md.</summary>
public sealed class JeuDemo
{
    public const string MotDePasse = "Station2026";
    public required ClientApi Admin { get; init; }
    public required ClientApi Caissier { get; init; }
    public required ClientApi Pompiste { get; init; }
    public required ClientApi Auditeur { get; init; }
    public required ClientApi Responsable { get; init; }
    public required ProduitDto Gasoil { get; init; }
    public required ProduitDto SansPlomb { get; init; }
    public required CuveDto CuveGo { get; init; }
    public required CuveDto CuveSp { get; init; }
    public required PistoletDto PistoletGo { get; init; }
    public required PistoletDto PistoletSp { get; init; }

    public static readonly DateTimeOffset DebutPrix = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    public static async Task<JeuDemo> CreerAsync(FabriqueApi api)
    {
        var admin = api.Client();
        await admin.ConnecterAsync("admin", "Admin2026!");
        var session = await admin.PostOkAsync<SessionDto>("/api/auth/mot-de-passe", new ChangementMotDePasseDto("Admin2026!", MotDePasse));
        admin.Http.DefaultRequestHeaders.Authorization = new("Bearer", session.Jeton);

        async Task<ClientApi> Utilisateur(string login, string role)
        {
            await admin.PostOkAsync<UtilisateurDto>("/api/admin/utilisateurs", new CreationUtilisateurDto(login, login.ToUpperInvariant(), "Initial2026", [role], null));
            var c = api.Client();
            await c.ConnecterAsync(login, "Initial2026");
            var s = await c.PostOkAsync<SessionDto>("/api/auth/mot-de-passe", new ChangementMotDePasseDto("Initial2026", MotDePasse));
            c.Http.DefaultRequestHeaders.Authorization = new("Bearer", s.Jeton);
            return c;
        }

        var caissier = await Utilisateur("caissier", "RESP_CAISSE");
        var pompiste = await Utilisateur("pompiste", "POMPISTE");
        var auditeur = await Utilisateur("auditeur", "AUDITEUR");
        var responsable = await Utilisateur("responsable", "RESP_STATION");

        var produits = await admin.GetOkAsync<List<ProduitDto>>("/api/produits");
        var go = produits.Single(p => p.Code == "GO");
        var sp = produits.Single(p => p.Code == "SP");
        await admin.PostOkAsync<List<PrixDto>>($"/api/produits/{go.Id}/prix", new NouveauPrixDto(12.85m, DebutPrix, "Prix initial démo"), Guid.NewGuid().ToString());
        await admin.PostOkAsync<List<PrixDto>>($"/api/produits/{sp.Id}/prix", new NouveauPrixDto(14.20m, DebutPrix, "Prix initial démo"), Guid.NewGuid().ToString());

        var c1 = await admin.PostOkAsync<CuveDto>("/api/equipements/cuves", new EnregistrementCuveDto("C1-GO", go.Id, 40_000m, 3_000m, 100m, 0.5m, true, null));
        var c2 = await admin.PostOkAsync<CuveDto>("/api/equipements/cuves", new EnregistrementCuveDto("C2-SP", sp.Id, 30_000m, 2_000m, 100m, 0.5m, true, null));
        var v1 = await admin.PostOkAsync<VolucompteurDto>("/api/equipements/volucompteurs", new EnregistrementVolucompteurDto("V1", "Îlot 1", true));
        var pGo = await admin.PostOkAsync<PistoletDto>($"/api/equipements/volucompteurs/{v1.Id}/pistolets",
            new EnregistrementPistoletDto("GO", go.Id, c1.Id, 999_999.99m, 125_430.50m, true));
        var pSp = await admin.PostOkAsync<PistoletDto>($"/api/equipements/volucompteurs/{v1.Id}/pistolets",
            new EnregistrementPistoletDto("SP", sp.Id, c2.Id, 999_999.99m, 80_000.00m, true));

        return new JeuDemo
        {
            Admin = admin, Caissier = caissier, Pompiste = pompiste, Auditeur = auditeur, Responsable = responsable,
            Gasoil = go, SansPlomb = sp, CuveGo = c1, CuveSp = c2, PistoletGo = pGo, PistoletSp = pSp
        };
    }

    /// <summary>Ouvre un poste, saisit les deux relevés (volumes donnés) et renvoie le détail.</summary>
    public async Task<PosteDetailDto> PosteAvecRelevesAsync(decimal volumeGo, decimal volumeSp, DateTimeOffset? debut = null)
    {
        var poste = await Pompiste.PostOkAsync<PosteDetailDto>("/api/postes", new OuverturePosteDto(null, debut, null));
        var id = poste.Poste.Id;
        var prep = await Pompiste.GetOkAsync<List<PreparationReleveDto>>($"/api/postes/{id}/preparation");
        var finGo = prep.Single(p => p.PistoletId == PistoletGo.Id).IndexDebutPropose + volumeGo;
        var finSp = prep.Single(p => p.PistoletId == PistoletSp.Id).IndexDebutPropose + volumeSp;
        var rGo = await Pompiste.PostOkAsync<ResultatReleveDto>($"/api/postes/{id}/releves", new SaisieReleveDto(PistoletGo.Id, null, finGo, null, null, true, null));
        if (!rGo.Enregistre) throw new Xunit.Sdk.XunitException(rGo.Message);
        var rSp = await Pompiste.PostOkAsync<ResultatReleveDto>($"/api/postes/{id}/releves", new SaisieReleveDto(PistoletSp.Id, null, finSp, null, null, true, null));
        if (!rSp.Enregistre) throw new Xunit.Sdk.XunitException(rSp.Message);
        return await Pompiste.GetOkAsync<PosteDetailDto>($"/api/postes/{id}");
    }
}
