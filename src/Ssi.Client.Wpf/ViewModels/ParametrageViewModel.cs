using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ssi.Client.Wpf.Services;
using Ssi.Contracts;

namespace Ssi.Client.Wpf.ViewModels;

/// <summary>
/// Paramétrage de la station : produits et prix datés, cuves, volucompteurs et pistolets, comptes financiers,
/// reprise des stocks et soldes de départ. Chaque modification est motivée ou tracée par le serveur.
/// </summary>
public sealed partial class ParametrageViewModel(ClientApi api, Session session) : PageViewModel
{
    public override string Titre => "Paramétrage de la station";

    public ObservableCollection<ProduitDto> Produits { get; } = [];
    public ObservableCollection<FamilleDto> Familles { get; } = [];
    public ObservableCollection<TaxeDto> Taxes { get; } = [];
    public ObservableCollection<PrixDto> HistoriquePrix { get; } = [];
    public ObservableCollection<CuveDto> Cuves { get; } = [];
    public ObservableCollection<VolucompteurDto> Volucompteurs { get; } = [];
    public ObservableCollection<CompteFinancierDto> Comptes { get; } = [];
    public IReadOnlyList<TypeCarburant?> TypesCarburant { get; } = [null, Contracts.TypeCarburant.Gasoil, Contracts.TypeCarburant.SansPlomb];
    public IReadOnlyList<TypeCompteFinancier> TypesCompte { get; } = Enum.GetValues<TypeCompteFinancier>();

    // Produit
    [ObservableProperty] private ProduitDto? produit;
    [ObservableProperty] private Guid? produitId;
    [ObservableProperty] private string codeProduit = "";
    [ObservableProperty] private string designation = "";
    [ObservableProperty] private FamilleDto? famille;
    [ObservableProperty] private string unite = "L";
    [ObservableProperty] private string? marque;
    [ObservableProperty] private string? conditionnement;
    [ObservableProperty] private TaxeDto? taxe;
    [ObservableProperty] private string seuilAlerte = "0";
    [ObservableProperty] private TypeCarburant? typeCarburant;
    [ObservableProperty] private bool produitActif = true;
    [ObservableProperty] private string nouveauPrix = "";
    [ObservableProperty] private DateTime? debutPrix = DateTime.Now;
    [ObservableProperty] private string heureDebutPrix = DateTime.Now.ToString("HH:mm");
    [ObservableProperty] private bool prixAchatReference;

    // Cuve
    [ObservableProperty] private CuveDto? cuve;
    [ObservableProperty] private Guid? cuveId;
    [ObservableProperty] private string codeCuve = "";
    [ObservableProperty] private ProduitDto? produitCuve;
    [ObservableProperty] private string capacite = "";
    [ObservableProperty] private string seuilMinimal = "0";
    [ObservableProperty] private string toleranceLitres = "100";
    [ObservableProperty] private string tolerancePct = "0,5";
    [ObservableProperty] private bool cuveActive = true;

    // Volucompteur et pistolet
    [ObservableProperty] private VolucompteurDto? volucompteur;
    [ObservableProperty] private string codeVolucompteur = "";
    [ObservableProperty] private string libelleVolucompteur = "";
    [ObservableProperty] private PistoletDto? pistolet;
    [ObservableProperty] private string codePistolet = "";
    [ObservableProperty] private ProduitDto? produitPistolet;
    [ObservableProperty] private CuveDto? cuvePistolet;
    [ObservableProperty] private string indexMaximum = "999999,99";
    [ObservableProperty] private string indexInitial = "0";
    [ObservableProperty] private bool pistoletActif = true;

    // Comptes et reprises
    [ObservableProperty] private string codeCompte = "";
    [ObservableProperty] private string libelleCompte = "";
    [ObservableProperty] private TypeCompteFinancier typeCompte = TypeCompteFinancier.Banque;
    [ObservableProperty] private string? banque;
    [ObservableProperty] private string? rib;
    [ObservableProperty] private CompteFinancierDto? compte;
    [ObservableProperty] private string soldeInitial = "";
    [ObservableProperty] private DateTime? dateSolde = DateTime.Today;
    [ObservableProperty] private ProduitDto? produitStock;
    [ObservableProperty] private CuveDto? cuveStock;
    [ObservableProperty] private string quantiteStock = "";
    [ObservableProperty] private string coutStock = "";

    public bool PeutCatalogue => session.A(Droits.CatalogueCreer, Droits.CatalogueModifier);
    public bool PeutPrix => session.A(Droits.PrixValider);
    public bool PeutEquipements => session.A(Droits.StationParametrer);
    public bool PeutComptes => session.A(Droits.AdminParametres);
    public bool PeutStockInitial => session.A(Droits.StockAjuster);
    public IEnumerable<ProduitDto> Carburants => Produits.Where(p => p.Categorie == CategorieProduit.Carburant);
    public IEnumerable<CuveDto> CuvesPistolet => Cuves.Where(c => c.ProduitId == ProduitPistolet?.Id);
    public IEnumerable<CuveDto> CuvesStock => Cuves.Where(c => c.ProduitId == ProduitStock?.Id);
    partial void OnProduitPistoletChanged(ProduitDto? value) { OnPropertyChanged(nameof(CuvesPistolet)); CuvePistolet = CuvesPistolet.FirstOrDefault(); }
    partial void OnProduitStockChanged(ProduitDto? value) { OnPropertyChanged(nameof(CuvesStock)); CuveStock = CuvesStock.FirstOrDefault(); }

    public override Task ChargerAsync() => ExecuterAsync(async () =>
    {
        Remplir(Produits, await api.GetAsync<List<ProduitDto>>("/api/produits"));
        Remplir(Familles, await api.GetAsync<List<FamilleDto>>("/api/produits/familles"));
        Remplir(Taxes, await api.GetAsync<List<TaxeDto>>("/api/admin/taxes"));
        Remplir(Cuves, await api.GetAsync<List<CuveDto>>("/api/equipements/cuves"));
        Remplir(Volucompteurs, await api.GetAsync<List<VolucompteurDto>>("/api/equipements/volucompteurs"));
        if (session.A(Droits.BanqueConsulter, Droits.CaisseConsulter)) Remplir(Comptes, await api.GetAsync<List<CompteFinancierDto>>("/api/tresorerie/comptes"));
        OnPropertyChanged(nameof(Carburants));
        Volucompteur = Volucompteurs.FirstOrDefault(v => v.Id == Volucompteur?.Id);
    });

    // ---------- Produits et prix ----------
    async partial void OnProduitChanged(ProduitDto? value)
    {
        if (value is null) return;
        ProduitId = value.Id; CodeProduit = value.Code; Designation = value.Designation; Famille = Familles.FirstOrDefault(f => f.Id == value.FamilleId);
        Unite = value.Unite; Marque = value.Marque; Conditionnement = value.Conditionnement; Taxe = Taxes.FirstOrDefault(t => t.Id == value.TaxeId);
        SeuilAlerte = value.SeuilAlerte.ToString(); TypeCarburant = value.TypeCarburant; ProduitActif = value.Actif;
        if (session.A(Droits.PrixConsulter))
            await ExecuterAsync(async () => Remplir(HistoriquePrix, await api.GetAsync<List<PrixDto>>($"/api/produits/{value.Id}/prix")));
    }

    [RelayCommand]
    private void NouveauProduit()
    {
        Produit = null; ProduitId = null; CodeProduit = ""; Designation = ""; Famille = null; Unite = "L"; Marque = null; Conditionnement = null;
        Taxe = null; SeuilAlerte = "0"; TypeCarburant = null; ProduitActif = true; HistoriquePrix.Clear();
    }

    [RelayCommand]
    private Task EnregistrerProduit() => ExecuterAsync(async () =>
    {
        if (Famille is null || Taxe is null) throw new ErreurApi(System.Net.HttpStatusCode.BadRequest, "Produit", "Choisissez la famille et la taxe applicable.", null);
        var dto = new EnregistrementProduitDto(CodeProduit.Trim(), Designation.Trim(), Famille.Id, Unite.Trim(), Marque, Conditionnement, Taxe.Id,
            Nombre(SeuilAlerte, "Seuil d'alerte"), TypeCarburant, ProduitActif);
        var p = ProduitId is { } id ? await api.PutAsync<ProduitDto>($"/api/produits/{id}", dto) : await api.PostAsync<ProduitDto>("/api/produits", dto);
        await ChargerAsync();
        Produit = Produits.FirstOrDefault(x => x.Id == p.Id);
    }, "Produit enregistré.");

    /// <summary>Nouveau prix daté : les ventes antérieures conservent leur prix (R-07, R-08).</summary>
    [RelayCommand]
    private Task EnregistrerPrix() => ExecuterAsync(async () =>
    {
        if (ProduitId is not { } id) return;
        var prix = Nombre(NouveauPrix, "Prix");
        if (!TimeSpan.TryParse(HeureDebutPrix, out var heure)) throw new ErreurApi(System.Net.HttpStatusCode.BadRequest, "Prix", "Heure de début invalide (hh:mm).", null);
        var debut = new DateTimeOffset((DebutPrix ?? DateTime.Today).Date + heure);
        var libelle = PrixAchatReference ? "prix d'achat de référence HT" : "prix de vente TTC";
        if (Saisir($"{Designation} : nouveau {libelle} {prix:N2} à partir du {debut:dd/MM/yyyy HH:mm}.\nUn poste en cours au moment du changement doit être clôturé à cette heure (H-06).\nMotif / référence :", "Changement de prix") is not { } motif) return;
        Remplir(HistoriquePrix, await api.PostAsync<List<PrixDto>>($"/api/produits/{id}/prix", new NouveauPrixDto(prix, debut, motif, PrixAchatReference), Guid.NewGuid().ToString()));
        NouveauPrix = "";
        Remplir(Produits, await api.GetAsync<List<ProduitDto>>("/api/produits"));
    }, "Prix enregistré.");

    // ---------- Cuves ----------
    partial void OnCuveChanged(CuveDto? value)
    {
        if (value is null) return;
        CuveId = value.Id; CodeCuve = value.Code; ProduitCuve = Produits.FirstOrDefault(p => p.Id == value.ProduitId); Capacite = value.Capacite.ToString();
        SeuilMinimal = value.SeuilMinimal.ToString(); ToleranceLitres = value.ToleranceEcartLitres.ToString(); TolerancePct = value.ToleranceEcartPct.ToString(); CuveActive = value.Actif;
    }

    [RelayCommand] private void NouvelleCuve() { Cuve = null; CuveId = null; CodeCuve = ""; Capacite = ""; SeuilMinimal = "0"; CuveActive = true; }

    [RelayCommand]
    private Task EnregistrerCuve() => ExecuterAsync(async () =>
    {
        if (ProduitCuve is null) throw new ErreurApi(System.Net.HttpStatusCode.BadRequest, "Cuve", "Choisissez le carburant stocké.", null);
        var dto = new EnregistrementCuveDto(CodeCuve.Trim(), ProduitCuve.Id, Nombre(Capacite, "Capacité"), Nombre(SeuilMinimal, "Seuil minimal"),
            Nombre(ToleranceLitres, "Tolérance (L)"), Nombre(TolerancePct, "Tolérance (%)"), CuveActive, null);
        var c = CuveId is { } id ? await api.PutAsync<CuveDto>($"/api/equipements/cuves/{id}", dto) : await api.PostAsync<CuveDto>("/api/equipements/cuves", dto);
        await ChargerAsync();
        Cuve = Cuves.FirstOrDefault(x => x.Id == c.Id);
    }, "Cuve enregistrée.");

    // ---------- Volucompteurs et pistolets ----------
    partial void OnVolucompteurChanged(VolucompteurDto? value) { CodeVolucompteur = value?.Code ?? ""; LibelleVolucompteur = value?.Libelle ?? ""; }

    partial void OnPistoletChanged(PistoletDto? value)
    {
        if (value is null) return;
        CodePistolet = value.Code; ProduitPistolet = Produits.FirstOrDefault(p => p.Id == value.ProduitId); CuvePistolet = Cuves.FirstOrDefault(c => c.Id == value.CuveId);
        IndexMaximum = value.IndexMaximum.ToString(); IndexInitial = value.IndexInitialCycle.ToString(); PistoletActif = value.Actif;
    }

    [RelayCommand]
    private Task CreerVolucompteur() => ExecuterAsync(async () =>
    {
        var v = await api.PostAsync<VolucompteurDto>("/api/equipements/volucompteurs", new EnregistrementVolucompteurDto(CodeVolucompteur.Trim(), LibelleVolucompteur.Trim(), true));
        await ChargerAsync();
        Volucompteur = Volucompteurs.FirstOrDefault(x => x.Id == v.Id);
    }, "Volucompteur créé.");

    [RelayCommand] private void NouveauPistolet() { Pistolet = null; CodePistolet = ""; IndexInitial = "0"; PistoletActif = true; }

    [RelayCommand]
    private Task EnregistrerPistolet() => ExecuterAsync(async () =>
    {
        if (Volucompteur is null || ProduitPistolet is null || CuvePistolet is null)
            throw new ErreurApi(System.Net.HttpStatusCode.BadRequest, "Pistolet", "Choisissez le volucompteur, le carburant et la cuve d'alimentation.", null);
        var dto = new EnregistrementPistoletDto(CodePistolet.Trim(), ProduitPistolet.Id, CuvePistolet.Id, Nombre(IndexMaximum, "Index maximum"), Nombre(IndexInitial, "Index initial"), PistoletActif);
        var url = $"/api/equipements/volucompteurs/{Volucompteur.Id}/pistolets";
        _ = Pistolet is { } p ? await api.PutAsync<PistoletDto>($"{url}/{p.Id}", dto) : await api.PostAsync<PistoletDto>(url, dto);
        var id = Volucompteur.Id;
        await ChargerAsync();
        Volucompteur = Volucompteurs.FirstOrDefault(x => x.Id == id);
    }, "Pistolet enregistré.");

    // ---------- Comptes financiers et reprises de départ ----------
    [RelayCommand]
    private Task CreerCompte() => ExecuterAsync(async () =>
    {
        await api.PostAsync<CompteFinancierDto>("/api/tresorerie/comptes", new EnregistrementCompteDto(CodeCompte.Trim(), LibelleCompte.Trim(), TypeCompte, Banque, Rib, true));
        CodeCompte = ""; LibelleCompte = ""; Banque = null; Rib = null;
        await ChargerAsync();
    }, "Compte créé.");

    [RelayCommand]
    private Task EnregistrerSoldeInitial() => ExecuterAsync(async () =>
    {
        if (Compte is null) return;
        var montant = Nombre(SoldeInitial, "Solde initial");
        if (Saisir($"Solde de départ de {Compte.Libelle} : {montant:N2} DH au {Jour(DateSolde):dd/MM/yyyy}.\nIl n'est accepté que si le compte n'a encore aucun mouvement.\nJustificatif (relevé, comptage) :", "Solde initial") is not { } motif) return;
        await api.PostAsync<CompteFinancierDto>("/api/tresorerie/comptes/solde-initial", new SoldeInitialCompteDto(Compte.Id, montant, Jour(DateSolde), motif), Guid.NewGuid().ToString());
        SoldeInitial = "";
        await ChargerAsync();
    }, "Solde initial enregistré.");

    [RelayCommand]
    private Task EnregistrerStockInitial() => ExecuterAsync(async () =>
    {
        if (ProduitStock is null) return;
        if (ProduitStock.Categorie == CategorieProduit.Carburant && CuveStock is null)
            throw new ErreurApi(System.Net.HttpStatusCode.BadRequest, "Stock initial", "Choisissez la cuve.", null);
        var q = Nombre(QuantiteStock, "Quantité");
        var cout = Nombre(CoutStock, "Coût unitaire HT");
        if (Saisir($"Stock de départ {ProduitStock.Designation} {(CuveStock is null ? "" : $"({CuveStock.Code})")} : {q:N3} à {cout:N4} DH HT.\nJustificatif (jaugeage, inventaire) :", "Stock initial") is not { } motif) return;
        await api.PostAsync<MouvementStockDto>("/api/stocks/initial", new StockInitialDto(ProduitStock.Id, ProduitStock.Categorie == CategorieProduit.Carburant ? CuveStock?.Id : null,
            q, cout, null, motif), Guid.NewGuid().ToString());
        QuantiteStock = ""; CoutStock = "";
    }, "Stock initial enregistré.");
}
