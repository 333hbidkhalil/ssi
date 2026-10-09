using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ssi.Client.Wpf.Services;
using Ssi.Contracts;

namespace Ssi.Client.Wpf.ViewModels;

/// <summary>
/// Livraisons directes (vrac) : commande client, commande fournisseur liée, livraison avec BL et numéro de réception client,
/// facturation sur la quantité livrée, frais directs et marge par opération. Aucun mouvement de stock station.
/// </summary>
public sealed partial class VracViewModel(ClientApi api, Session session) : PageViewModel
{
    public override string Titre => "Livraisons directes (vrac)";

    public ObservableCollection<CommandeClientDto> Commandes { get; } = [];
    public ObservableCollection<ClientDto> Clients { get; } = [];
    public ObservableCollection<FournisseurDto> Fournisseurs { get; } = [];
    public ObservableCollection<ProduitDto> Carburants { get; } = [];

    [ObservableProperty] private CommandeClientDto? commandeSelectionnee;
    [ObservableProperty] private CommandeClientDto? detail;
    [ObservableProperty] private LivraisonDto? livraison;
    [ObservableProperty] private CommandeFournisseurDto? commandeFournisseur;

    // Nouvelle commande client
    [ObservableProperty] private ClientDto? client;
    [ObservableProperty] private ProduitDto? produit;
    [ObservableProperty] private string volume = "";
    [ObservableProperty] private string prixVente = "";
    [ObservableProperty] private DateTime? datePrevue = DateTime.Today.AddDays(1);
    [ObservableProperty] private string? lieu;

    // Commande fournisseur liée
    [ObservableProperty] private FournisseurDto? fournisseur;
    [ObservableProperty] private string volumeFournisseur = "";
    [ObservableProperty] private string prixAchat = "";
    [ObservableProperty] private string fraisPrevus = "0";

    // Livraison
    [ObservableProperty] private string quantiteChargee = "";
    [ObservableProperty] private string quantiteLivree = "";
    [ObservableProperty] private string? numeroBl;
    [ObservableProperty] private string? numeroReception;
    [ObservableProperty] private string? vehicule;
    [ObservableProperty] private string? chauffeur;
    [ObservableProperty] private string? justification;

    // Frais directs
    [ObservableProperty] private string montantFrais = "";
    [ObservableProperty] private string libelleFrais = "Transport";

    public bool PeutCreer => session.A(Droits.VracCreer);
    public bool PeutValider => session.A(Droits.VracValider);
    public bool PeutAnnuler => session.A(Droits.VracAnnuler);
    public bool PeutFacturer => session.A(Droits.FacturesCreer);

    public override Task ChargerAsync() => ExecuterAsync(async () =>
    {
        if (session.A(Droits.ClientsConsulter)) Remplir(Clients, await api.GetAsync<List<ClientDto>>("/api/clients?actif=true"));
        if (session.A(Droits.FournisseursConsulter)) Remplir(Fournisseurs, await api.GetAsync<List<FournisseurDto>>("/api/fournisseurs?actif=true"));
        Remplir(Carburants, await api.GetAsync<List<ProduitDto>>("/api/produits?actif=true&categorie=Carburant"));
        await ChargerCommandesAsync(CommandeSelectionnee?.Id);
    });

    private async Task ChargerCommandesAsync(Guid? garder)
    {
        Remplir(Commandes, await api.GetAsync<List<CommandeClientDto>>("/api/vrac/commandes"));
        CommandeSelectionnee = Commandes.FirstOrDefault(c => c.Id == garder) ?? Commandes.FirstOrDefault();
        Detail = CommandeSelectionnee is null ? null : await api.GetAsync<CommandeClientDto>($"/api/vrac/commandes/{CommandeSelectionnee.Id}");
        CommandeFournisseur = Detail?.CommandesFournisseur.FirstOrDefault();
    }

    async partial void OnCommandeSelectionneeChanged(CommandeClientDto? value)
    {
        if (value is null || Detail?.Id == value.Id) return;
        await ExecuterAsync(async () =>
        {
            Detail = await api.GetAsync<CommandeClientDto>($"/api/vrac/commandes/{value.Id}");
            CommandeFournisseur = Detail.CommandesFournisseur.FirstOrDefault();
        });
    }

    private void Afficher(CommandeClientDto c)
    {
        Detail = c;
        CommandeFournisseur ??= c.CommandesFournisseur.FirstOrDefault();
    }

    [RelayCommand]
    private Task CreerCommande() => ExecuterAsync(async () =>
    {
        if (Client is null || Produit is null) throw new ErreurApi(System.Net.HttpStatusCode.BadRequest, "Saisie", "Choisissez le client et le produit.", null);
        var c = await api.PostAsync<CommandeClientDto>("/api/vrac/commandes", new CreationCommandeClientDto(Client.Id, Produit.Id, Nombre(Volume, "Volume"),
            Nombre(PrixVente, "Prix de vente HT"), DatePrevue is null ? null : Jour(DatePrevue), Lieu, null, null, Fournisseur?.Id));
        Volume = ""; PrixVente = "";
        await ChargerCommandesAsync(c.Id);
    }, "Commande client créée (brouillon). Ajoutez la commande fournisseur puis confirmez.");

    [RelayCommand]
    private Task AjouterCommandeFournisseur() => ExecuterAsync(async () =>
    {
        if (Detail is null || Fournisseur is null) throw new ErreurApi(System.Net.HttpStatusCode.BadRequest, "Saisie", "Choisissez la commande et le fournisseur.", null);
        var volumeCf = NombreOptionnel(VolumeFournisseur, "Volume") ?? Detail.Volume;
        CommandeFournisseur = null;
        Afficher(await api.PostAsync<CommandeClientDto>($"/api/vrac/commandes/{Detail.Id}/commandes-fournisseur",
            new CommandeFournisseurVracDto(Fournisseur.Id, volumeCf, Nombre(PrixAchat, "Prix d'achat HT"), Nombre(FraisPrevus, "Frais prévus"), null, null, null)));
        VolumeFournisseur = ""; PrixAchat = "";
    }, "Commande fournisseur liée.");

    [RelayCommand]
    private Task Confirmer() => ExecuterAsync(async () =>
    {
        var id = Detail!.Id;
        try
        {
            Afficher(await api.PostAsync<CommandeClientDto>($"/api/vrac/commandes/{id}/confirmation", new ConfirmationCommandeClientDto(null)));
        }
        catch (ErreurApi e) when (e.Code == "MARGE_INSUFFISANTE" && session.A(Droits.VracDerogation))
        {
            if (Saisir($"{e.Message}\n\nMotif de la dérogation (tracé dans le journal d'audit) :", "Dérogation de marge") is not { } motif) throw;
            Afficher(await api.PostAsync<CommandeClientDto>($"/api/vrac/commandes/{id}/confirmation", new ConfirmationCommandeClientDto(motif)));
        }
        await ChargerCommandesAsync(id);
    }, "Commande confirmée.");

    [RelayCommand]
    private Task AnnulerCommande() => ExecuterAsync(async () =>
    {
        if (Saisir($"Motif de l'annulation de la commande {Detail!.Numero} :") is not { } motif) return;
        Afficher(await api.PostAsync<CommandeClientDto>($"/api/vrac/commandes/{Detail.Id}/annulation", new MotifDto(motif)));
        await ChargerCommandesAsync(Detail.Id);
    }, "Commande annulée.");

    [RelayCommand]
    private Task EnregistrerLivraison() => ExecuterAsync(async () =>
    {
        if (Detail is null || CommandeFournisseur is null) throw new ErreurApi(System.Net.HttpStatusCode.BadRequest, "Saisie", "La livraison doit être rattachée à une commande fournisseur.", null);
        var chargee = Nombre(QuantiteChargee, "Quantité chargée");
        var livree = NombreOptionnel(QuantiteLivree, "Quantité livrée") ?? chargee;
        Afficher(await api.PostAsync<CommandeClientDto>($"/api/vrac/commandes/{Detail.Id}/livraisons", new SaisieLivraisonDto(CommandeFournisseur.Id, null,
            chargee, livree, NumeroBl, NumeroReception, Vehicule, null, Chauffeur, Justification, null)));
        QuantiteChargee = ""; QuantiteLivree = ""; NumeroBl = null; NumeroReception = null; Justification = null;
    }, "Livraison enregistrée. Validez-la quand le BL et le bon de réception client sont en main.");

    [RelayCommand]
    private Task ValiderLivraison() => ExecuterAsync(async () =>
    {
        if (Livraison is null || !Confirmer($"Valider la livraison {Livraison.Numero} de {Livraison.QuantiteLivree:N0} L ? Elle deviendra facturable.")) return;
        Afficher(await api.PostAsync<CommandeClientDto>($"/api/vrac/livraisons/{Livraison.Id}/validation", null, Guid.NewGuid().ToString()));
        await ChargerCommandesAsync(Detail!.Id);
    }, "Livraison validée (aucun mouvement de stock station).");

    [RelayCommand]
    private Task AnnulerLivraison() => ExecuterAsync(async () =>
    {
        if (Livraison is null || Saisir($"Motif de l'annulation de la livraison {Livraison.Numero} :") is not { } motif) return;
        Afficher(await api.PostAsync<CommandeClientDto>($"/api/vrac/livraisons/{Livraison.Id}/annulation", new MotifDto(motif)));
    }, "Livraison annulée.");

    [RelayCommand]
    private Task Facturer() => ExecuterAsync(async () =>
    {
        if (!Confirmer($"Créer et valider la facture client de la commande {Detail!.Numero} sur les quantités livrées non facturées ?")) return;
        var f = await api.PostAsync<FactureDto>($"/api/vrac/commandes/{Detail.Id}/facturation", new { Date = DateOnly.FromDateTime(DateTime.Today), Valider = true },
            Guid.NewGuid().ToString());
        await ChargerCommandesAsync(Detail.Id);
        Information = $"Facture {f.Numero} validée : {f.TotalTtc:N2} DH TTC.";
    });

    /// <summary>Frais de transport réparti sur les livraisons validées de la commande, au prorata des volumes.</summary>
    [RelayCommand]
    private Task SaisirFrais() => ExecuterAsync(async () =>
    {
        var livraisons = Detail!.Livraisons.Where(l => l.Statut == StatutLivraison.Validee).Select(l => l.Id).ToList();
        if (livraisons.Count == 0) throw new ErreurApi(System.Net.HttpStatusCode.BadRequest, "Frais", "Aucune livraison validée sur cette commande.", null);
        await api.PostAsync<FraisDirectDto>("/api/vrac/frais", new SaisieFraisDirectDto(TypeFrais.Transport, LibelleFrais, Fournisseur?.Id, null,
            DateOnly.FromDateTime(DateTime.Today), Nombre(MontantFrais, "Montant HT"), MethodeRepartitionFrais.Volume, livraisons, null));
        MontantFrais = "";
        await ChargerCommandesAsync(Detail.Id);
    }, "Frais enregistrés et répartis ; la marge est recalculée.");
}
