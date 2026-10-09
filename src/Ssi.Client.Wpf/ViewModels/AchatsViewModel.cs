using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ssi.Client.Wpf.Services;
using Ssi.Contracts;

namespace Ssi.Client.Wpf.ViewModels;

/// <summary>Ligne éditable d'une commande, d'une réception ou d'une facture fournisseur.</summary>
public sealed partial class LigneAchat : ObservableObject
{
    public required ProduitDto Produit { get; init; }
    public CuveDto? Cuve { get; init; }
    public string Emplacement => Cuve?.Code ?? "Magasin";
    [ObservableProperty] private string quantiteCommandee = "";
    [ObservableProperty] private string quantiteChargee = "";
    [ObservableProperty] private string quantiteRecue = "";
    [ObservableProperty] private string quantiteAcceptee = "";
    [ObservableProperty] private string prixUnitaireHt = "";
    [ObservableProperty] private string? justification;
}

/// <summary>
/// Achats : commandes fournisseurs (cuve ou magasin), réceptions contrôlées (chargé / reçu / accepté, justification des écarts),
/// puis facture fournisseur rattachée à la réception (l'écart de prix éventuel est traité à la validation, H-07).
/// </summary>
public sealed partial class AchatsViewModel(ClientApi api, Session session) : PageViewModel
{
    public override string Titre => "Achats et réceptions";

    public ObservableCollection<CommandeFournisseurDto> Commandes { get; } = [];
    public ObservableCollection<ReceptionDto> Receptions { get; } = [];
    public ObservableCollection<FournisseurDto> Fournisseurs { get; } = [];
    public ObservableCollection<ProduitDto> Produits { get; } = [];
    public ObservableCollection<CuveDto> Cuves { get; } = [];
    public ObservableCollection<LigneAchat> Lignes { get; } = [];
    public ObservableCollection<LigneAchat> LignesFacture { get; } = [];

    [ObservableProperty] private FournisseurDto? fournisseur;
    [ObservableProperty] private CommandeFournisseurDto? commande;
    [ObservableProperty] private ReceptionDto? reception;
    [ObservableProperty] private ProduitDto? produit;
    [ObservableProperty] private CuveDto? cuve;
    [ObservableProperty] private string quantite = "";
    [ObservableProperty] private string prix = "";
    [ObservableProperty] private string? numeroBl;
    [ObservableProperty] private string? transporteur;
    [ObservableProperty] private string? vehicule;
    [ObservableProperty] private string? referenceFournisseur;
    [ObservableProperty] private string? numeroFacture;
    [ObservableProperty] private DateTime? dateFacture = DateTime.Today;
    [ObservableProperty] private bool modeReception;

    public bool PeutCreer => session.A(Droits.AchatsCreer);
    public bool PeutValider => session.A(Droits.AchatsValider);
    public bool PeutAnnuler => session.A(Droits.AchatsAnnuler);
    public bool PeutFacturer => session.A(Droits.FacturesCreer);
    public IEnumerable<CuveDto> CuvesDuProduit => Cuves.Where(c => c.ProduitId == Produit?.Id && c.Actif);
    partial void OnProduitChanged(ProduitDto? value)
    {
        OnPropertyChanged(nameof(CuvesDuProduit));
        Cuve = CuvesDuProduit.FirstOrDefault();
        Prix = value?.PrixAchatReferenceHt?.ToString("N4") ?? "";
    }

    public override Task ChargerAsync() => ExecuterAsync(async () =>
    {
        Remplir(Fournisseurs, await api.GetAsync<List<FournisseurDto>>("/api/fournisseurs?actif=true"));
        Remplir(Produits, await api.GetAsync<List<ProduitDto>>("/api/produits?actif=true"));
        Remplir(Cuves, await api.GetAsync<List<CuveDto>>("/api/equipements/cuves"));
        await ChargerListesAsync();
    });

    private async Task ChargerListesAsync()
    {
        Remplir(Commandes, (await api.GetAsync<List<CommandeFournisseurDto>>("/api/achats/commandes"))
            .Where(c => c.Destination != DestinationAchat.LivraisonDirecte).OrderByDescending(c => c.Date));
        Remplir(Receptions, (await api.GetAsync<List<ReceptionDto>>("/api/achats/receptions")).OrderByDescending(r => r.Horodatage));
    }

    // ---------- Saisie des lignes ----------
    [RelayCommand]
    private void AjouterLigne()
    {
        Erreur = null;
        try
        {
            if (Produit is null) { Erreur = "Choisissez un produit."; return; }
            if (Produit.Categorie == CategorieProduit.Carburant && Cuve is null) { Erreur = "Un carburant est reçu dans une cuve : choisissez la cuve."; return; }
            var q = Nombre(Quantite, "Quantité").ToString();
            Lignes.Add(new LigneAchat
            {
                Produit = Produit, Cuve = Produit.Categorie == CategorieProduit.Carburant ? Cuve : null, QuantiteCommandee = q, QuantiteChargee = q,
                QuantiteRecue = q, QuantiteAcceptee = q, PrixUnitaireHt = Nombre(Prix, "Prix HT").ToString()
            });
            Quantite = "";
        }
        catch (ErreurApi e) { Erreur = e.Message; }
    }

    [RelayCommand] private void ViderLignes() { Lignes.Clear(); ModeReception = false; }

    // ---------- Commandes ----------
    [RelayCommand]
    private Task CreerCommande() => ExecuterAsync(async () =>
    {
        if (Fournisseur is null || Lignes.Count == 0) throw new ErreurApi(System.Net.HttpStatusCode.BadRequest, "Commande", "Choisissez le fournisseur et ajoutez au moins une ligne.", null);
        var destination = Lignes.All(l => l.Cuve is not null) ? DestinationAchat.Cuve : DestinationAchat.Magasin;
        var c = await api.PostAsync<CommandeFournisseurDto>("/api/achats/commandes", new CreationCommandeFournisseurDto(Fournisseur.Id, DateOnly.FromDateTime(DateTime.Today),
            null, destination, ReferenceFournisseur, null, 0m,
            Lignes.Select(l => new LigneCommandeFournisseurDto(l.Produit.Id, l.Cuve?.Id, Nombre(l.QuantiteCommandee, "Quantité"), Nombre(l.PrixUnitaireHt, "Prix HT"))).ToList()));
        Lignes.Clear();
        await ChargerListesAsync();
        Commande = Commandes.FirstOrDefault(x => x.Id == c.Id);
        Information = $"Commande {c.Numero} créée ({c.TotalHt:N2} DH HT). Confirmez-la pour pouvoir la réceptionner.";
    });

    [RelayCommand]
    private Task ConfirmerCommande() => ExecuterAsync(async () =>
    {
        if (Commande is null) return;
        await api.PostAsync<CommandeFournisseurDto>($"/api/achats/commandes/{Commande.Id}/confirmation");
        await ChargerListesAsync();
    }, "Commande confirmée.");

    [RelayCommand]
    private Task AnnulerCommande() => ExecuterAsync(async () =>
    {
        if (Commande is null || Saisir($"Motif de l'annulation de la commande {Commande.Numero} :") is not { } motif) return;
        await api.PostAsync($"/api/achats/commandes/{Commande.Id}/annulation", new MotifDto(motif));
        await ChargerListesAsync();
    }, "Commande annulée.");

    /// <summary>Prépare la réception de la commande sélectionnée : les lignes reprennent le reste à recevoir, à corriger selon le BL et le jaugeage.</summary>
    [RelayCommand]
    private void PreparerReception()
    {
        if (Commande is null) return;
        Fournisseur = Fournisseurs.FirstOrDefault(f => f.Id == Commande.FournisseurId);
        Lignes.Clear();
        foreach (var l in Commande.Lignes)
        {
            var reste = Math.Max(0, l.Quantite - l.QuantiteRecue).ToString();
            Lignes.Add(new LigneAchat
            {
                Produit = Produits.First(p => p.Id == l.ProduitId), Cuve = Cuves.FirstOrDefault(c => c.Id == l.CuveId), QuantiteCommandee = reste,
                QuantiteChargee = reste, QuantiteRecue = reste, QuantiteAcceptee = reste, PrixUnitaireHt = (l.PrixUnitaireHt ?? 0).ToString()
            });
        }
        ModeReception = true;
        Information = "Corrigez les quantités chargée (BL), reçue (jaugeage) et acceptée, puis enregistrez la réception.";
    }

    // ---------- Réceptions ----------
    [RelayCommand]
    private Task EnregistrerReception() => ExecuterAsync(async () =>
    {
        if (Fournisseur is null || Lignes.Count == 0) throw new ErreurApi(System.Net.HttpStatusCode.BadRequest, "Réception", "Choisissez le fournisseur et saisissez les lignes.", null);
        var r = await api.PostAsync<ReceptionDto>("/api/achats/receptions", new CreationReceptionDto(ModeReception ? Commande?.Id : null, Fournisseur.Id, null,
            NumeroBl, Transporteur, Vehicule, Lignes.Select(l => new LigneReceptionSaisieDto(l.Produit.Id, l.Cuve?.Id, Nombre(l.QuantiteCommandee, "Quantité commandée"),
                Nombre(l.QuantiteChargee, "Quantité chargée"), Nombre(l.QuantiteRecue, "Quantité reçue"), Nombre(l.QuantiteAcceptee, "Quantité acceptée"),
                Nombre(l.PrixUnitaireHt, "Prix HT"), l.Justification)).ToList()));
        Lignes.Clear(); ModeReception = false; NumeroBl = null;
        await ChargerListesAsync();
        Reception = Receptions.FirstOrDefault(x => x.Id == r.Id);
        Information = $"Réception {r.Numero} enregistrée : le stock ne change qu'à sa validation." + (r.Controles.Count > 0 ? " Contrôles : " + string.Join(" ", r.Controles) : "");
    });

    [RelayCommand]
    private Task ValiderReception() => ExecuterAsync(async () =>
    {
        if (Reception is null || !Confirmer($"Valider la réception {Reception.Numero} ? Les quantités acceptées entrent en stock au prix de réception.")) return;
        await api.PostAsync<ReceptionDto>($"/api/achats/receptions/{Reception.Id}/validation", null, Guid.NewGuid().ToString());
        await ChargerListesAsync();
    }, "Réception validée : stock et coût moyen mis à jour.");

    [RelayCommand]
    private Task AnnulerReception() => ExecuterAsync(async () =>
    {
        if (Reception is null || Saisir($"Motif de l'annulation de la réception {Reception.Numero} (les entrées en stock seront contre-passées) :") is not { } motif) return;
        await api.PostAsync<ReceptionDto>($"/api/achats/receptions/{Reception.Id}/annulation", new MotifDto(motif));
        await ChargerListesAsync();
    }, "Réception annulée.");

    partial void OnReceptionChanged(ReceptionDto? value)
    {
        LignesFacture.Clear();
        if (value is null) return;
        foreach (var l in value.Lignes)
            LignesFacture.Add(new LigneAchat
            {
                Produit = Produits.FirstOrDefault(p => p.Id == l.ProduitId) ?? new ProduitDto(l.ProduitId, "", l.Produit, Guid.Empty, "", CategorieProduit.Annexe, "", null, null, Guid.Empty, "", 0, null, true, null, null),
                Cuve = Cuves.FirstOrDefault(c => c.Id == l.CuveId), QuantiteAcceptee = l.QuantiteAcceptee.ToString(), PrixUnitaireHt = (l.PrixUnitaireHt ?? 0).ToString()
            });
    }

    /// <summary>Facture fournisseur rattachée à la réception validée : quantités et prix modifiables selon la facture papier.</summary>
    [RelayCommand]
    private Task SaisirFacture() => ExecuterAsync(async () =>
    {
        if (Reception is null || Reception.Statut != StatutDocument.Valide)
            throw new ErreurApi(System.Net.HttpStatusCode.BadRequest, "Facture", "Sélectionnez une réception validée.", null);
        if (string.IsNullOrWhiteSpace(NumeroFacture)) throw new ErreurApi(System.Net.HttpStatusCode.BadRequest, "Facture", "Indiquez le numéro de la facture du fournisseur.", null);
        var f = await api.PostAsync<FactureDto>("/api/factures", new CreationFactureDto(SensFacture.Fournisseur, TypeFacture.Facture, OrigineFacture.AchatStock,
            null, Reception.FournisseurId, NumeroFacture.Trim(), Jour(DateFacture), null, null, null, Reception.Id, null,
            LignesFacture.Select(l => new LigneFactureSaisieDto(l.Produit.Id, null, Nombre(l.QuantiteAcceptee, "Quantité facturée"), Nombre(l.PrixUnitaireHt, "Prix HT facturé"),
                null, null, Reception.Id)).ToList()));
        var avertissements = f.Avertissements.Count > 0 ? "\n\n" + string.Join("\n", f.Avertissements) : "";
        if (Confirmer($"Facture {NumeroFacture} enregistrée en brouillon : {f.TotalTtc:N2} DH TTC.{avertissements}\n\nLa valider maintenant (dette fournisseur constatée) ?"))
        {
            f = await api.PostAsync<FactureDto>($"/api/factures/{f.Id}/validation", null, Guid.NewGuid().ToString());
            Information = $"Facture {f.Numero} validée." + (f.Avertissements.Count > 0 ? " " + string.Join(" ", f.Avertissements) : "");
        }
        else Information = "Facture enregistrée en brouillon : validez-la depuis l'écran Factures.";
        NumeroFacture = null;
    });
}
