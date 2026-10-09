using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ssi.Client.Wpf.Services;
using Ssi.Contracts;

namespace Ssi.Client.Wpf.ViewModels;

/// <summary>Ligne éditable d'une facture ou d'un avoir saisi librement.</summary>
public sealed partial class LigneLibre : ObservableObject
{
    [ObservableProperty] private ProduitDto? produit;
    [ObservableProperty] private string designation = "";
    [ObservableProperty] private string quantite = "1";
    [ObservableProperty] private string prixUnitaireHt = "";
    [ObservableProperty] private string tauxTvaPct = "";
}

/// <summary>
/// Factures clients et fournisseurs, facturation récapitulative des bons station, règlements affectés aux factures.
/// Une facture validée ne se supprime pas : elle s'annule par un motif, ou se corrige par un avoir.
/// </summary>
public sealed partial class FacturesViewModel(ClientApi api, Session session) : PageViewModel
{
    public override string Titre => "Factures et règlements";

    public ObservableCollection<FactureDto> Factures { get; } = [];
    public ObservableCollection<ReglementDto> Reglements { get; } = [];
    public ObservableCollection<ClientDto> Clients { get; } = [];
    public ObservableCollection<CompteFinancierDto> Comptes { get; } = [];
    public IReadOnlyList<SensFacture> Sens { get; } = Enum.GetValues<SensFacture>();
    public IReadOnlyList<ModeReglement> Modes { get; } = [ModeReglement.Especes, ModeReglement.Cheque, ModeReglement.Virement, ModeReglement.Carte, ModeReglement.Effet];

    [ObservableProperty] private SensFacture sensFiltre = SensFacture.Client;
    [ObservableProperty] private bool seulementNonSoldees;
    [ObservableProperty] private DateTime? du = DateTime.Today.AddMonths(-3);
    [ObservableProperty] private DateTime? au = DateTime.Today;
    [ObservableProperty] private FactureDto? factureSelectionnee;
    [ObservableProperty] private FactureDto? detail;
    [ObservableProperty] private ReglementDto? reglementSelectionne;

    // Règlement de la facture sélectionnée
    [ObservableProperty] private ModeReglement modeReglement = ModeReglement.Virement;
    [ObservableProperty] private CompteFinancierDto? compte;
    [ObservableProperty] private string montant = "";
    [ObservableProperty] private string? reference;
    [ObservableProperty] private DateTime? dateReglement = DateTime.Today;

    // Facturation des bons
    [ObservableProperty] private ClientDto? clientBons;
    [ObservableProperty] private DateTime? jusqua = DateTime.Today;

    private string? cleReglement;

    // Saisie libre d'une facture ou d'un avoir
    public ObservableCollection<ClientDto> ClientsSaisie { get; } = [];
    public ObservableCollection<FournisseurDto> Fournisseurs { get; } = [];
    public ObservableCollection<ProduitDto> Produits { get; } = [];
    public ObservableCollection<LigneLibre> LignesSaisie { get; } = [];
    public IReadOnlyList<TypeFacture> Types { get; } = Enum.GetValues<TypeFacture>();
    public IReadOnlyList<OrigineFacture> OriginesClient { get; } = [OrigineFacture.Autre, OrigineFacture.Articles];
    public IReadOnlyList<OrigineFacture> OriginesFournisseur { get; } = [OrigineFacture.Autre, OrigineFacture.Transport, OrigineFacture.AchatStock];
    [ObservableProperty] private TypeFacture typeSaisie = TypeFacture.Facture;
    [ObservableProperty] private OrigineFacture origineSaisie = OrigineFacture.Autre;
    [ObservableProperty] private ClientDto? clientSaisie;
    [ObservableProperty] private FournisseurDto? fournisseurSaisie;
    [ObservableProperty] private string? numeroTiers;
    [ObservableProperty] private DateTime? dateSaisie = DateTime.Today;
    [ObservableProperty] private DateTime? echeanceSaisie;
    [ObservableProperty] private string? commentaireSaisie;
    [ObservableProperty] private FactureDto? factureOrigine;
    public bool SaisieClient => SensFiltre == SensFacture.Client;
    public IReadOnlyList<OrigineFacture> Origines => SaisieClient ? OriginesClient : OriginesFournisseur;
    public bool PeutCreer => session.A(Droits.FacturesCreer);
    partial void OnSensFiltreChanged(SensFacture value)
    {
        OnPropertyChanged(nameof(SaisieClient)); OnPropertyChanged(nameof(Origines));
        OrigineSaisie = OrigineFacture.Autre; FactureOrigine = null;
    }

    public bool PeutValider => session.A(Droits.FacturesValider);
    public bool PeutAnnuler => session.A(Droits.FacturesAnnuler);
    public bool PeutRegler => session.A(Droits.ReglementsCreer);
    public bool PeutAnnulerReglement => session.A(Droits.ReglementsAnnuler);

    public override Task ChargerAsync() => ExecuterAsync(async () =>
    {
        if (session.A(Droits.ClientsConsulter)) Remplir(Clients, (await api.GetAsync<List<ClientDto>>("/api/clients?actif=true")).Where(c => c.Type == TypeClient.Credit));
        if (PeutCreer)
        {
            if (session.A(Droits.ClientsConsulter)) Remplir(ClientsSaisie, await api.GetAsync<List<ClientDto>>("/api/clients?actif=true"));
            if (session.A(Droits.FournisseursConsulter)) Remplir(Fournisseurs, await api.GetAsync<List<FournisseurDto>>("/api/fournisseurs?actif=true"));
            if (session.A(Droits.CatalogueConsulter)) Remplir(Produits, await api.GetAsync<List<ProduitDto>>("/api/produits?actif=true"));
        }
        if (session.A(Droits.BanqueConsulter, Droits.CaisseConsulter)) Remplir(Comptes, (await api.GetAsync<List<CompteFinancierDto>>("/api/tresorerie/comptes")).Where(c => c.Actif));
        await ChargerFacturesAsync(FactureSelectionnee?.Id);
    });

    private async Task ChargerFacturesAsync(Guid? garder)
    {
        var liste = await api.GetAsync<List<FactureDto>>($"/api/factures?sens={SensFiltre}&du={Jour(Du):yyyy-MM-dd}&au={Jour(Au):yyyy-MM-dd}");
        Remplir(Factures, SeulementNonSoldees ? liste.Where(f => f.Statut == StatutDocument.Valide && f.ResteDu != 0) : liste);
        if (session.A(Droits.ReglementsConsulter))
            Remplir(Reglements, await api.GetAsync<List<ReglementDto>>($"/api/tresorerie/reglements?sens={(SensFiltre == SensFacture.Client ? SensReglement.Encaissement : SensReglement.Decaissement)}&du={Jour(Du):yyyy-MM-dd}&au={Jour(Au):yyyy-MM-dd}"));
        FactureSelectionnee = Factures.FirstOrDefault(f => f.Id == garder);
        Detail = FactureSelectionnee is null ? null : await api.GetAsync<FactureDto>($"/api/factures/{FactureSelectionnee.Id}");
    }

    [RelayCommand] private Task Rechercher() => ExecuterAsync(() => ChargerFacturesAsync(null));

    async partial void OnFactureSelectionneeChanged(FactureDto? value)
    {
        if (value is null || Detail?.Id == value.Id) return;
        await ExecuterAsync(async () => Detail = await api.GetAsync<FactureDto>($"/api/factures/{value.Id}"));
        Montant = Detail?.ResteDu > 0 ? Detail.ResteDu.ToString("N2") : "";
        cleReglement = null;
    }

    [RelayCommand]
    private Task Valider() => ExecuterAsync(async () =>
    {
        if (!Confirmer($"Valider la facture {Detail!.Numero} ({Detail.TotalTtc:N2} DH TTC) ? Elle ne pourra plus être modifiée.")) return;
        await api.PostAsync<FactureDto>($"/api/factures/{Detail.Id}/validation", null, Guid.NewGuid().ToString());
        await ChargerFacturesAsync(Detail.Id);
    }, "Facture validée.");

    [RelayCommand]
    private Task Annuler() => ExecuterAsync(async () =>
    {
        if (Saisir($"Motif de l'annulation de la facture {Detail!.Numero} :") is not { } motif) return;
        await api.PostAsync<FactureDto>($"/api/factures/{Detail.Id}/annulation", new MotifDto(motif));
        await ChargerFacturesAsync(Detail.Id);
    }, "Facture annulée (conservée dans l'historique).");

    [RelayCommand]
    private Task Imprimer() => ExecuterAsync(async () =>
    {
        var chemin = await api.TelechargerAsync($"/api/rapports/factures/{Detail!.Id}/pdf");
        OuvrirFichier(chemin);
        Information = $"PDF enregistré : {chemin}";
    });

    [RelayCommand]
    private Task FacturerBons() => ExecuterAsync(async () =>
    {
        if (ClientBons is null) throw new ErreurApi(System.Net.HttpStatusCode.BadRequest, "Client", "Choisissez le client à facturer.", null);
        if (!Confirmer($"Facturer tous les bons non facturés de {ClientBons.Nom} jusqu'au {Jour(Jusqua):dd/MM/yyyy} ?\n(Bons non facturés connus : {ClientBons.BonsNonFactures:N2} DH)")) return;
        var f = await api.PostAsync<FactureDto>("/api/factures/bons-station", new FacturationBonsDto(ClientBons.Id, Jour(Jusqua), DateOnly.FromDateTime(DateTime.Today)),
            Guid.NewGuid().ToString());
        SensFiltre = SensFacture.Client;
        await ChargerFacturesAsync(f.Id);
        Information = $"Facture récapitulative {f.Numero} : {f.TotalTtc:N2} DH TTC (pas de nouveau chiffre d'affaires : les ventes sont déjà comptées par les relevés).";
    });

    [RelayCommand]
    private Task Regler() => ExecuterAsync(async () =>
    {
        var f = Detail!;
        var m = Nombre(Montant, "Montant");
        cleReglement ??= Guid.NewGuid().ToString();
        var sens = f.Sens == SensFacture.Client ? SensReglement.Encaissement : SensReglement.Decaissement;
        var r = await api.PostAsync<ReglementDto>("/api/tresorerie/reglements", new CreationReglementDto(sens,
            f.Sens == SensFacture.Client ? f.TiersId : null, f.Sens == SensFacture.Fournisseur ? f.TiersId : null, null, ModeReglement, Compte?.Id,
            Jour(DateReglement), m, Reference, [new AffectationSaisieDto(f.Id, null, Math.Min(m, f.ResteDu))]), cleReglement);
        cleReglement = null; Reference = null;
        await ChargerFacturesAsync(f.Id);
        Information = $"Règlement {r.Numero} enregistré sur {r.Compte}" + (r.NonAffecte > 0 ? $" ; {r.NonAffecte:N2} DH restent en acompte non affecté." : ".");
    });

    [RelayCommand]
    private Task AnnulerReglement() => ExecuterAsync(async () =>
    {
        if (ReglementSelectionne is null || Saisir($"Motif de l'annulation du règlement {ReglementSelectionne.Numero} ({ReglementSelectionne.Montant:N2} DH) :") is not { } motif) return;
        await api.PostAsync<ReglementDto>($"/api/tresorerie/reglements/{ReglementSelectionne.Id}/annulation", new MotifDto(motif));
        await ChargerFacturesAsync(Detail?.Id);
    }, "Règlement annulé : le mouvement de trésorerie est contre-passé.");

    // ---------- Saisie libre ----------
    [RelayCommand] private void AjouterLigneSaisie() => LignesSaisie.Add(new LigneLibre());

    [RelayCommand]
    private void RetirerLigneSaisie(LigneLibre? ligne) { if (ligne is not null) LignesSaisie.Remove(ligne); }

    /// <summary>Prépare un avoir sur la facture sélectionnée : même tiers, lignes reprises (à réduire au montant de l'avoir).</summary>
    [RelayCommand]
    private void PreparerAvoir()
    {
        if (Detail is null || Detail.Statut != StatutDocument.Valide || Detail.Type != TypeFacture.Facture)
        {
            Erreur = "Sélectionnez une facture validée pour établir un avoir.";
            return;
        }
        TypeSaisie = TypeFacture.Avoir;
        FactureOrigine = Detail;
        ClientSaisie = Detail.Sens == SensFacture.Client ? ClientsSaisie.FirstOrDefault(c => c.Id == Detail.TiersId) : null;
        FournisseurSaisie = Detail.Sens == SensFacture.Fournisseur ? Fournisseurs.FirstOrDefault(f => f.Id == Detail.TiersId) : null;
        LignesSaisie.Clear();
        foreach (var l in Detail.Lignes)
            LignesSaisie.Add(new LigneLibre
            {
                Produit = Produits.FirstOrDefault(p => p.Id == l.ProduitId), Designation = l.Designation, Quantite = l.Quantite.ToString(),
                PrixUnitaireHt = l.PrixUnitaireHt.ToString(), TauxTvaPct = (l.TauxTva * 100).ToString("0.##")
            });
        Information = $"Avoir sur {Detail.Numero} : ajustez les quantités ou les prix au montant à rembourser ou à déduire, puis enregistrez.";
    }

    [RelayCommand]
    private void AnnulerSaisie()
    {
        LignesSaisie.Clear(); FactureOrigine = null; TypeSaisie = TypeFacture.Facture; NumeroTiers = null; CommentaireSaisie = null;
    }

    [RelayCommand]
    private Task EnregistrerSaisie() => ExecuterAsync(async () =>
    {
        if (LignesSaisie.Count == 0) throw new ErreurApi(System.Net.HttpStatusCode.BadRequest, "Facture", "Ajoutez au moins une ligne.", null);
        var client = SaisieClient ? ClientSaisie?.Id ?? throw new ErreurApi(System.Net.HttpStatusCode.BadRequest, "Facture", "Choisissez le client.", null) : (Guid?)null;
        var fournisseur = !SaisieClient ? FournisseurSaisie?.Id ?? throw new ErreurApi(System.Net.HttpStatusCode.BadRequest, "Facture", "Choisissez le fournisseur.", null) : (Guid?)null;
        var n = 0;
        var lignes = LignesSaisie.Select(l =>
        {
            n++;
            var taux = NombreOptionnel(l.TauxTvaPct, $"Ligne {n} : TVA (%)");
            return new LigneFactureSaisieDto(l.Produit?.Id, string.IsNullOrWhiteSpace(l.Designation) ? null : l.Designation.Trim(), Nombre(l.Quantite, $"Ligne {n} : quantité"),
                Nombre(l.PrixUnitaireHt, $"Ligne {n} : prix HT"), taux is null ? null : taux / 100m, null, null);
        }).ToList();
        var f = await api.PostAsync<FactureDto>("/api/factures", new CreationFactureDto(SensFiltre, TypeSaisie, OrigineSaisie, client, fournisseur, NumeroTiers,
            Jour(DateSaisie), EcheanceSaisie is null ? null : Jour(EcheanceSaisie), TypeSaisie == TypeFacture.Avoir ? FactureOrigine?.Id : null, null, null, CommentaireSaisie, lignes));
        AnnulerSaisie();
        await ChargerFacturesAsync(f.Id);
        Information = $"{(f.Type == TypeFacture.Avoir ? "Avoir" : "Facture")} enregistré(e) en brouillon : {f.TotalTtc:N2} DH TTC. Vérifiez puis validez."
                      + (f.Avertissements.Count > 0 ? " " + string.Join(" ", f.Avertissements) : "");
    });
}
