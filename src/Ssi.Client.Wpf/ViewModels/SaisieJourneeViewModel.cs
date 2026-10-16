using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ssi.Client.Wpf.Services;
using Ssi.Contracts;

namespace Ssi.Client.Wpf.ViewModels;

/// <summary>Lecture tolérante d'un nombre en cours de saisie (virgule ou point décimal, espaces ignorés) ; null si vide ou invalide.</summary>
public static class SaisieNombre
{
    public static decimal? Lire(string? texte)
    {
        var t = (texte ?? "").Replace(" ", "").Replace(" ", "").Replace(" ", "");
        if (t.Length == 0) return null;
        var c = CultureInfo.CurrentCulture;
        if (t.Contains('.') && !t.Contains(',')) t = t.Replace(".", c.NumberFormat.NumberDecimalSeparator);
        return decimal.TryParse(t, NumberStyles.Number, c, out var v) ? v : null;
    }
}

/// <summary>Production : une ligne par volucompteur/pistolet (ancien index, nouvel index, volume, prix, total).</summary>
public sealed partial class LigneProduction : ObservableObject
{
    public required PreparationReleveDto Preparation { get; init; }
    [ObservableProperty] private string nouvelIndex = "";
    public decimal? Volume => SaisieNombre.Lire(NouvelIndex) is { } f ? f - Preparation.IndexDebutPropose : null;
    public decimal? Total => Volume is { } v ? Math.Round(v * Preparation.PrixUnitaireTtc, 2, MidpointRounding.AwayFromZero) : null;
    public bool Incoherent => Volume is < 0;
    partial void OnNouvelIndexChanged(string value)
    {
        OnPropertyChanged(nameof(Volume)); OnPropertyChanged(nameof(Total)); OnPropertyChanged(nameof(Incoherent));
    }
}

/// <summary>Sous-total d'un produit (comme les lignes de total du tableau).</summary>
public sealed record SousTotalProduit(string Produit, decimal Volume, decimal Total);

/// <summary>Clients : un bon de crédit (nom, produit, bon de commande, bon de livraison, volume, prix unitaire, total).</summary>
public sealed partial class LigneClient : ObservableObject
{
    private readonly Func<ProduitDto?, decimal?> prixPompe;
    public LigneClient(Func<ProduitDto?, decimal?> prixPompe) => this.prixPompe = prixPompe;

    [ObservableProperty] private ClientDto? client;
    [ObservableProperty] private ProduitDto? produit;
    [ObservableProperty] private string? bonCommande;
    [ObservableProperty] private string? bonLivraison;
    [ObservableProperty] private string volume = "";
    [ObservableProperty] private string prixUnitaire = "";
    public decimal? Total => SaisieNombre.Lire(Volume) is { } v && SaisieNombre.Lire(PrixUnitaire) is { } p ? Math.Round(v * p, 2, MidpointRounding.AwayFromZero) : null;
    /// <summary>Prix convenu différent du prix à la pompe : la différence se retrouvera dans l'écart du solde.</summary>
    public bool PrixDifferent => SaisieNombre.Lire(PrixUnitaire) is { } p && prixPompe(Produit) is { } pp && p != pp;

    partial void OnProduitChanged(ProduitDto? value)
    {
        if (prixPompe(value) is { } p) PrixUnitaire = p.ToString("0.00##", CultureInfo.CurrentCulture);
        OnPropertyChanged(nameof(PrixDifferent));
    }
    partial void OnVolumeChanged(string value) => OnPropertyChanged(nameof(Total));
    partial void OnPrixUnitaireChanged(string value) { OnPropertyChanged(nameof(Total)); OnPropertyChanged(nameof(PrixDifferent)); }
}

/// <summary>Charges payées en caisse : catégorie, bénéficiaire, montant.</summary>
public sealed partial class LigneCharge : ObservableObject
{
    [ObservableProperty] private CategorieChargeDto? categorie;
    [ObservableProperty] private string? beneficiaire;
    [ObservableProperty] private string montant = "";
}

/// <summary>
/// Saisie de la journée au format du tableau de l'exploitant (production, clients, charges, solde de la période).
/// L'enregistrement est unique et complet côté serveur : poste, relevés contrôlés, bons, charges et espèces, ou rien.
/// </summary>
public sealed partial class SaisieJourneeViewModel(ClientApi api, Session session) : PageViewModel
{
    public override string Titre => "Saisie du jour";

    public ObservableCollection<LigneProduction> Production { get; } = [];
    public ObservableCollection<SousTotalProduit> SousTotaux { get; } = [];
    public ObservableCollection<LigneClient> Clients { get; } = [];
    public ObservableCollection<LigneCharge> Charges { get; } = [];
    public ObservableCollection<ClientDto> ClientsCredit { get; } = [];
    public ObservableCollection<ProduitDto> Carburants { get; } = [];
    public ObservableCollection<CategorieChargeDto> Categories { get; } = [];

    [ObservableProperty] private DateTime? journee = DateTime.Today;
    [ObservableProperty] private string especesRemises = "";
    [ObservableProperty] private string? blocage;
    [ObservableProperty] private ResultatJourneeDto? resultat;
    [ObservableProperty] private decimal totalProduction;
    [ObservableProperty] private decimal totalClients;
    [ObservableProperty] private decimal totalCharges;

    // Une clé d'idempotence par journée enregistrée, conservée jusqu'au succès (un renvoi après coupure ne double rien).
    private string? cle;
    private bool chargementJournee;

    public bool PeutSaisirCharges => session.A(Droits.ChargesCreer);
    public bool PeutValider => session.A(Droits.StationValider);
    public bool Modifiable => Resultat is null && Blocage is null;
    public decimal SoldeAttendu => TotalProduction - TotalClients - TotalCharges;
    public decimal? Ecart => SaisieNombre.Lire(EspecesRemises) is { } r ? r - SoldeAttendu : null;

    partial void OnEspecesRemisesChanged(string value) => OnPropertyChanged(nameof(Ecart));
    partial void OnResultatChanged(ResultatJourneeDto? value) => OnPropertyChanged(nameof(Modifiable));
    partial void OnBlocageChanged(string? value) => OnPropertyChanged(nameof(Modifiable));
    partial void OnTotalProductionChanged(decimal value) => SoldeModifie();
    partial void OnTotalClientsChanged(decimal value) => SoldeModifie();
    partial void OnTotalChargesChanged(decimal value) => SoldeModifie();
    private void SoldeModifie() { OnPropertyChanged(nameof(SoldeAttendu)); OnPropertyChanged(nameof(Ecart)); }

    async partial void OnJourneeChanged(DateTime? value)
    {
        if (!chargementJournee && Resultat is null) await ExecuterAsync(PreparerAsync);
    }

    public override Task ChargerAsync() => ExecuterAsync(async () =>
    {
        if (session.A(Droits.ClientsConsulter))
            Remplir(ClientsCredit, (await api.GetAsync<List<ClientDto>>("/api/clients?actif=true")).Where(c => c.Type == TypeClient.Credit));
        Remplir(Carburants, await api.GetAsync<List<ProduitDto>>("/api/produits?actif=true&categorie=Carburant"));
        if (session.A(Droits.ChargesConsulter)) Remplir(Categories, await api.GetAsync<List<CategorieChargeDto>>("/api/charges/categories"));
        if (Resultat is null) await PreparerAsync();
    });

    private async Task PreparerAsync()
    {
        var p = await api.GetAsync<PreparationJourneeDto>($"/api/journees/preparation?journee={Jour(Journee):yyyy-MM-dd}");
        Blocage = p.Blocage;
        foreach (var l in Production) l.PropertyChanged -= LigneModifiee;
        Production.Clear();
        foreach (var i in p.Index)
        {
            var l = new LigneProduction { Preparation = i };
            l.PropertyChanged += LigneModifiee;
            Production.Add(l);
        }
        if (Clients.Count == 0) AjouterClient();
        if (Charges.Count == 0 && PeutSaisirCharges) AjouterCharge();
        Recalculer();
    }

    private decimal? PrixPompe(ProduitDto? produit)
        => produit is null ? null : Production.FirstOrDefault(l => l.Preparation.Produit == produit.Designation)?.Preparation.PrixUnitaireTtc ?? produit.PrixVenteTtc;

    private void LigneModifiee(object? sender, PropertyChangedEventArgs e) => Recalculer();

    private void Recalculer()
    {
        TotalProduction = Production.Sum(l => l.Total ?? 0);
        Remplir(SousTotaux, Production.GroupBy(l => l.Preparation.Produit)
            .Select(g => new SousTotalProduit(g.Key, g.Sum(l => l.Volume ?? 0), g.Sum(l => l.Total ?? 0))));
        TotalClients = Clients.Sum(l => l.Total ?? 0);
        TotalCharges = Charges.Sum(l => SaisieNombre.Lire(l.Montant) ?? 0);
    }

    [RelayCommand]
    private void AjouterClient()
    {
        var l = new LigneClient(PrixPompe);
        l.PropertyChanged += LigneModifiee;
        Clients.Add(l);
    }

    [RelayCommand]
    private void SupprimerClient(LigneClient? l)
    {
        if (l is null || !Modifiable) return;
        l.PropertyChanged -= LigneModifiee;
        Clients.Remove(l);
        Recalculer();
    }

    [RelayCommand]
    private void AjouterCharge()
    {
        var l = new LigneCharge();
        l.PropertyChanged += LigneModifiee;
        Charges.Add(l);
    }

    [RelayCommand]
    private void SupprimerCharge(LigneCharge? l)
    {
        if (l is null || !Modifiable) return;
        l.PropertyChanged -= LigneModifiee;
        Charges.Remove(l);
        Recalculer();
    }

    private SaisieJourneeDto Construire(bool confirmer)
    {
        var index = Production.Select(l => new IndexJourneeDto(l.Preparation.PistoletId,
            Nombre(l.NouvelIndex, $"{l.Preparation.Volucompteur} / {l.Preparation.Pistolet} — nouvel index (même inchangé)"))).ToList();
        // Lignes entièrement vides : ignorées ; lignes commencées : complètes, sinon erreur explicite.
        var bons = Clients.Where(l => l.Client is not null || l.Produit is not null || !string.IsNullOrWhiteSpace(l.Volume)).Select((l, n) =>
        {
            if (l.Client is null || l.Produit is null) throw ErreurSaisie($"Client ligne {n + 1} : choisissez le client et le produit.");
            return new BonClientJourneeDto(l.Client.Id, l.Produit.Id, l.BonCommande, l.BonLivraison,
                Nombre(l.Volume, $"Client {l.Client.Nom} — volume"), Nombre(l.PrixUnitaire, $"Client {l.Client.Nom} — prix unitaire"));
        }).ToList();
        var charges = Charges.Where(l => l.Categorie is not null || !string.IsNullOrWhiteSpace(l.Beneficiaire) || !string.IsNullOrWhiteSpace(l.Montant)).Select((l, n) =>
        {
            if (l.Categorie is null || string.IsNullOrWhiteSpace(l.Beneficiaire)) throw ErreurSaisie($"Charge ligne {n + 1} : choisissez la catégorie et indiquez le bénéficiaire.");
            return new ChargeJourneeDto(l.Categorie.Id, l.Beneficiaire, Nombre(l.Montant, $"Charge {l.Beneficiaire} — montant"));
        }).ToList();
        var remises = Nombre(EspecesRemises, "Espèces remises (comptées en fin de journée)");
        return new SaisieJourneeDto(Jour(Journee), index, bons, charges, remises, confirmer);
    }

    private static ErreurApi ErreurSaisie(string message) => new(System.Net.HttpStatusCode.BadRequest, "Saisie", message, null);

    [RelayCommand]
    private Task Enregistrer() => ExecuterAsync(async () =>
    {
        var saisie = Construire(false);
        if (!Confirmer($"Enregistrer la journée du {saisie.Journee:dd/MM/yyyy} ?\n\nProduction : {TotalProduction:N2} DH\nClients (bons) : {TotalClients:N2} DH\n"
                       + $"Charges payées en caisse : {TotalCharges:N2} DH\nSolde attendu : {SoldeAttendu:N2} DH\nEspèces remises : {saisie.EspecesRemises:N2} DH\n"
                       + $"Écart : {saisie.EspecesRemises - SoldeAttendu:N2} DH")) return;
        cle ??= Guid.NewGuid().ToString();
        try
        {
            Resultat = await api.PostAsync<ResultatJourneeDto>("/api/journees", saisie, cle);
        }
        catch (ErreurApi ex) when (ex.Code == "CONFIRMATION_REQUISE")
        {
            if (!Confirmer(ex.Message + "\n\nConfirmez-vous ces index ?", "Contrôle des index")) return;
            Resultat = await api.PostAsync<ResultatJourneeDto>("/api/journees", saisie with { ConfirmerAvertissements = true }, cle);
        }
        cle = null;
        Information = $"Journée enregistrée (poste {Resultat.Poste.Poste.Numero}, clôturé)."
                      + (PeutValider ? " Vérifiez puis validez-la pour mettre à jour stocks et trésorerie." : " Un responsable doit la valider.");
    });

    [RelayCommand]
    private Task ValiderJournee() => ExecuterAsync(async () =>
    {
        if (Resultat is null) return;
        var id = Resultat.Poste.Poste.Id;
        if (!Confirmer($"Valider la journée (poste {Resultat.Poste.Poste.Numero}) ? Ventes, sorties de stock, encaissements et paiement des charges seront comptabilisés. "
                       + "Toute correction ultérieure passera par une réouverture motivée.")) return;
        cle ??= Guid.NewGuid().ToString();
        var poste = await api.PostAsync<PosteDetailDto>($"/api/postes/{id}/validation", null, cle);
        cle = null;
        Resultat = Resultat with { Poste = poste };
    }, "Journée validée : ventes, stocks et trésorerie mis à jour.");

    [RelayCommand]
    private Task NouvelleJournee() => ExecuterAsync(async () =>
    {
        foreach (var l in Clients) l.PropertyChanged -= LigneModifiee;
        foreach (var l in Charges) l.PropertyChanged -= LigneModifiee;
        Clients.Clear(); Charges.Clear();
        EspecesRemises = ""; cle = null;
        Resultat = null;
        chargementJournee = true;
        Journee = DateTime.Today;
        chargementJournee = false;
        await PreparerAsync();
    });
}
