using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ssi.Client.Wpf.Services;
using Ssi.Contracts;

namespace Ssi.Client.Wpf.ViewModels;

/// <summary>
/// Trésorerie : soldes et prévisions, mouvements par compte, transferts internes, frais bancaires, échéancier,
/// charges d'exploitation et rapprochement bancaire. Tous les soldes sont recalculés à partir des mouvements.
/// </summary>
public sealed partial class TresorerieViewModel(ClientApi api, Session session) : PageViewModel
{
    public override string Titre => "Trésorerie et charges";

    public ObservableCollection<CompteFinancierDto> Comptes { get; } = [];
    public ObservableCollection<PrevisionTresorerieDto> Previsions { get; } = [];
    public ObservableCollection<MouvementFinancierDto> Mouvements { get; } = [];
    public ObservableCollection<TransfertDto> Transferts { get; } = [];
    public ObservableCollection<EcheanceDto> Creances { get; } = [];
    public ObservableCollection<EcheanceDto> Dettes { get; } = [];
    public ObservableCollection<ChargeDto> Charges { get; } = [];
    public ObservableCollection<CategorieChargeDto> Categories { get; } = [];
    public ObservableCollection<CentreCoutDto> Centres { get; } = [];
    public ObservableCollection<FournisseurDto> Fournisseurs { get; } = [];
    public IReadOnlyList<ModeReglement?> ModesPaiement { get; } = [null, ModeReglement.Especes, ModeReglement.Cheque, ModeReglement.Virement, ModeReglement.Carte];

    [ObservableProperty] private SituationTresorerieDto? situation;
    [ObservableProperty] private CompteFinancierDto? compte;
    [ObservableProperty] private DateTime? du = DateTime.Today.AddDays(-30);
    [ObservableProperty] private DateTime? au = DateTime.Today;

    // Transfert
    [ObservableProperty] private CompteFinancierDto? source;
    [ObservableProperty] private CompteFinancierDto? destination;
    [ObservableProperty] private string montantTransfert = "";
    [ObservableProperty] private string commission = "0";
    [ObservableProperty] private string libelleTransfert = "Versement espèces en banque";
    [ObservableProperty] private TransfertDto? transfertSelectionne;

    // Frais bancaires
    [ObservableProperty] private string montantFrais = "";
    [ObservableProperty] private string libelleFrais = "Frais de tenue de compte";

    // Charge
    [ObservableProperty] private CategorieChargeDto? categorie;
    [ObservableProperty] private CentreCoutDto? centre;
    [ObservableProperty] private FournisseurDto? fournisseurCharge;
    [ObservableProperty] private string? beneficiaire;
    [ObservableProperty] private string montantHt = "";
    [ObservableProperty] private string montantTva = "0";
    [ObservableProperty] private DateTime? dateCharge = DateTime.Today;
    [ObservableProperty] private DateTime? echeanceCharge;
    [ObservableProperty] private string? description;
    [ObservableProperty] private string? piece;
    [ObservableProperty] private ModeReglement? payeeImmediatement;
    [ObservableProperty] private CompteFinancierDto? comptePaiement;
    [ObservableProperty] private ChargeDto? chargeSelectionnee;

    // Rapprochement
    [ObservableProperty] private string lignesReleve = "";
    [ObservableProperty] private string soldeReleve = "";
    [ObservableProperty] private DateTime? dateReleve = DateTime.Today;
    [ObservableProperty] private RapprochementDto? rapprochement;
    [ObservableProperty] private LigneRapprochementDto? ligneRapprochement;
    [ObservableProperty] private MouvementFinancierDto? mouvementNonPointe;

    private string? cleTransfert, cleCharge, cleFrais;

    public bool PeutCreer => session.A(Droits.BanqueCreer);
    public bool PeutRapprocher => session.A(Droits.BanqueRapprocher);
    public bool VoitCharges => session.A(Droits.ChargesConsulter);
    public bool PeutCreerCharge => session.A(Droits.ChargesCreer);
    public bool PeutAnnulerCharge => session.A(Droits.ChargesAnnuler);

    public override Task ChargerAsync() => ExecuterAsync(async () =>
    {
        Situation = await api.GetAsync<SituationTresorerieDto>("/api/tresorerie/situation");
        Remplir(Comptes, Situation.Comptes);
        Remplir(Previsions, Situation.Previsions);
        Compte = Comptes.FirstOrDefault(c => c.Id == Compte?.Id) ?? Comptes.FirstOrDefault(c => c.Type == TypeCompteFinancier.Banque) ?? Comptes.FirstOrDefault();
        await ChargerMouvementsAsync();
        Remplir(Transferts, (await api.GetAsync<List<TransfertDto>>("/api/tresorerie/transferts")).OrderByDescending(t => t.Date).Take(100));
        if (session.A(Droits.ClientsConsulter)) Remplir(Creances, await api.GetAsync<List<EcheanceDto>>("/api/tresorerie/creances"));
        if (session.A(Droits.FournisseursConsulter))
        {
            Remplir(Dettes, await api.GetAsync<List<EcheanceDto>>("/api/tresorerie/dettes"));
            Remplir(Fournisseurs, await api.GetAsync<List<FournisseurDto>>("/api/fournisseurs?actif=true"));
        }
        if (VoitCharges)
        {
            Remplir(Categories, await api.GetAsync<List<CategorieChargeDto>>("/api/charges/categories"));
            Remplir(Centres, await api.GetAsync<List<CentreCoutDto>>("/api/charges/centres"));
            await ChargerChargesAsync();
        }
    });

    private async Task ChargerMouvementsAsync()
    {
        if (Compte is null) { Mouvements.Clear(); return; }
        Remplir(Mouvements, (await api.GetAsync<Page<MouvementFinancierDto>>(
            $"/api/tresorerie/mouvements?compteId={Compte.Id}&du={Jour(Du):yyyy-MM-dd}&au={Jour(Au):yyyy-MM-dd}&taille=500")).Elements);
    }

    private async Task ChargerChargesAsync()
        => Remplir(Charges, await api.GetAsync<List<ChargeDto>>($"/api/charges?du={Jour(Du):yyyy-MM-dd}&au={Jour(Au):yyyy-MM-dd}"));

    async partial void OnCompteChanged(CompteFinancierDto? value)
    {
        if (!Occupe) await ExecuterAsync(ChargerMouvementsAsync);
    }

    [RelayCommand] private Task Rechercher() => ExecuterAsync(async () => { await ChargerMouvementsAsync(); if (VoitCharges) await ChargerChargesAsync(); });

    [RelayCommand]
    private Task Transferer() => ExecuterAsync(async () =>
    {
        if (Source is null || Destination is null) throw new ErreurApi(System.Net.HttpStatusCode.BadRequest, "Transfert", "Choisissez les comptes source et destination.", null);
        var m = Nombre(MontantTransfert, "Montant");
        var c = Nombre(Commission, "Commission");
        if (!Confirmer($"Transférer {m:N2} DH de {Source.Libelle} vers {Destination.Libelle}" + (c > 0 ? $" (commission {c:N2} DH)" : "") + " ?")) return;
        cleTransfert ??= Guid.NewGuid().ToString();
        await api.PostAsync<TransfertDto>("/api/tresorerie/transferts", new CreationTransfertDto(Source.Id, Destination.Id, m, c, DateOnly.FromDateTime(DateTime.Today), LibelleTransfert), cleTransfert);
        cleTransfert = null; MontantTransfert = "";
        await ChargerAsync();
    }, "Transfert enregistré (aucun impact sur le résultat, sauf la commission).");

    [RelayCommand]
    private Task AnnulerTransfert() => ExecuterAsync(async () =>
    {
        if (TransfertSelectionne is null || Saisir($"Motif de l'annulation du transfert {TransfertSelectionne.Numero} :") is not { } motif) return;
        await api.PostAsync<TransfertDto>($"/api/tresorerie/transferts/{TransfertSelectionne.Id}/annulation", new MotifDto(motif));
        await ChargerAsync();
    }, "Transfert annulé par contre-passation.");

    [RelayCommand]
    private Task EnregistrerFrais() => ExecuterAsync(async () =>
    {
        if (Compte is null) return;
        var m = Nombre(MontantFrais, "Montant");
        cleFrais ??= Guid.NewGuid().ToString();
        await api.PostAsync<MouvementFinancierDto>($"/api/tresorerie/comptes/{Compte.Id}/operations",
            new OperationCompteDto(m, false, NatureMouvementFinancier.FraisBancaires, LibelleFrais, null), cleFrais);
        cleFrais = null; MontantFrais = "";
        await ChargerAsync();
    }, "Frais bancaires enregistrés.");

    [RelayCommand]
    private Task CreerCharge() => ExecuterAsync(async () =>
    {
        if (Categorie is null || Centre is null) throw new ErreurApi(System.Net.HttpStatusCode.BadRequest, "Charge", "Choisissez la catégorie et le centre de coût.", null);
        cleCharge ??= Guid.NewGuid().ToString();
        await api.PostAsync<ChargeDto>("/api/charges", new CreationChargeDto(Jour(DateCharge), FournisseurCharge?.Id, Beneficiaire, Categorie.Id, Centre.Id,
            Nombre(MontantHt, "Montant HT"), Nombre(MontantTva, "TVA"), null, EcheanceCharge is null ? null : Jour(EcheanceCharge), Description, Piece,
            PayeeImmediatement, PayeeImmediatement is null ? null : ComptePaiement?.Id), cleCharge);
        cleCharge = null; MontantHt = ""; MontantTva = "0"; Description = null; Piece = null;
        await ChargerAsync();
    }, "Charge enregistrée.");

    [RelayCommand]
    private Task AnnulerCharge() => ExecuterAsync(async () =>
    {
        if (ChargeSelectionnee is null || Saisir($"Motif de l'annulation de la charge {ChargeSelectionnee.Numero} :") is not { } motif) return;
        await api.PostAsync<ChargeDto>($"/api/charges/{ChargeSelectionnee.Id}/annulation", new MotifDto(motif));
        await ChargerChargesAsync();
    }, "Charge annulée.");

    /// <summary>Lignes du relevé collées depuis le fichier de la banque : « jj/mm/aaaa;libellé;montant » (montant négatif pour un débit).</summary>
    [RelayCommand]
    private Task CreerRapprochement() => ExecuterAsync(async () =>
    {
        if (Compte is null || Compte.Type != TypeCompteFinancier.Banque) throw new ErreurApi(System.Net.HttpStatusCode.BadRequest, "Rapprochement", "Sélectionnez un compte bancaire.", null);
        var lignes = new List<LigneReleveSaisieDto>();
        var n = 0;
        foreach (var brute in LignesReleve.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            n++;
            var p = brute.Split(';', '\t');
            if (p.Length < 3 || !DateOnly.TryParse(p[0], System.Globalization.CultureInfo.CurrentCulture, out var d))
                throw new ErreurApi(System.Net.HttpStatusCode.BadRequest, "Relevé", $"Ligne {n} illisible : « {brute} » (attendu : date;libellé;montant).", null);
            lignes.Add(new LigneReleveSaisieDto(d, p[1].Trim(), Nombre(p[^1], $"Ligne {n}, montant")));
        }
        Rapprochement = await api.PostAsync<RapprochementDto>("/api/tresorerie/rapprochements",
            new CreationRapprochementDto(Compte.Id, Jour(DateReleve), Nombre(SoldeReleve, "Solde du relevé"), lignes));
    }, "Rapprochement créé : les correspondances évidentes sont proposées.");

    [RelayCommand]
    private Task Pointer() => ExecuterAsync(async () =>
    {
        if (Rapprochement is null || LigneRapprochement is null) return;
        var mouvement = MouvementNonPointe?.Id ?? LigneRapprochement.Suggestions.FirstOrDefault();
        if (mouvement == 0) throw new ErreurApi(System.Net.HttpStatusCode.BadRequest, "Pointage", "Choisissez le mouvement SSI correspondant.", null);
        Rapprochement = await api.PostAsync<RapprochementDto>($"/api/tresorerie/rapprochements/{Rapprochement.Id}/pointages", new PointageDto(LigneRapprochement.Id, mouvement));
    });

    [RelayCommand]
    private Task Depointer() => ExecuterAsync(async () =>
    {
        if (Rapprochement is null || LigneRapprochement is null) return;
        Rapprochement = await api.PostAsync<RapprochementDto>($"/api/tresorerie/rapprochements/{Rapprochement.Id}/pointages", new PointageDto(LigneRapprochement.Id, null));
    });

    [RelayCommand]
    private Task ValiderRapprochement() => ExecuterAsync(async () =>
    {
        Rapprochement = await api.PostAsync<RapprochementDto>($"/api/tresorerie/rapprochements/{Rapprochement!.Id}/validation");
    }, "Rapprochement validé : solde bancaire et solde SSI concordent.");
}
