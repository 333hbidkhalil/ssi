using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ssi.Client.Wpf.Services;
using Ssi.Contracts;

namespace Ssi.Client.Wpf.ViewModels;

public sealed record LignePanier(ProduitDto Produit, decimal Quantite, decimal PrixUnitaireTtc, bool PrixModifie)
{
    public decimal MontantTtc => Math.Round(Quantite * PrixUnitaireTtc, 2, MidpointRounding.AwayFromZero);
}

/// <summary>Vente comptoir de lubrifiants et articles (le carburant ne se vend que par les index).</summary>
public sealed partial class VentesViewModel(ClientApi api, Session session) : PageViewModel
{
    public override string Titre => "Ventes d'articles";

    public ObservableCollection<ProduitDto> Articles { get; } = [];
    public ObservableCollection<PositionArticleDto> Positions { get; } = [];
    public ObservableCollection<LignePanier> Panier { get; } = [];
    public ObservableCollection<VenteDto> Ventes { get; } = [];
    public ObservableCollection<ClientDto> Clients { get; } = [];
    public IReadOnlyList<ModeReglement> Modes { get; } = [ModeReglement.Especes, ModeReglement.Carte, ModeReglement.Cheque, ModeReglement.Virement, ModeReglement.Credit];

    [ObservableProperty] private ProduitDto? article;
    [ObservableProperty] private string quantite = "1";
    [ObservableProperty] private string? prix;
    [ObservableProperty] private ModeReglement mode = ModeReglement.Especes;
    [ObservableProperty] private ClientDto? client;
    [ObservableProperty] private LignePanier? lignePanier;
    [ObservableProperty] private VenteDto? venteSelectionnee;
    [ObservableProperty] private DateTime? du = DateTime.Today;

    private string? cleVente;   // conservée tant que la vente n'est pas confirmée par le serveur

    public bool PeutCreer => session.A(Droits.VentesCreer);
    public bool PeutAnnuler => session.A(Droits.VentesAnnuler);
    public bool PeutModifierPrix => session.A(Droits.PrixValider);
    public decimal TotalPanier => Panier.Sum(l => l.MontantTtc);
    public bool EstCredit => Mode == ModeReglement.Credit;
    partial void OnModeChanged(ModeReglement value) => OnPropertyChanged(nameof(EstCredit));
    partial void OnArticleChanged(ProduitDto? value) => Prix = value?.PrixVenteTtc?.ToString("N2");

    public override Task ChargerAsync() => ExecuterAsync(async () =>
    {
        Remplir(Articles, (await api.GetAsync<List<ProduitDto>>("/api/produits?actif=true")).Where(p => p.Categorie != CategorieProduit.Carburant).OrderBy(p => p.Designation));
        if (session.A(Droits.StockConsulter)) Remplir(Positions, await api.GetAsync<List<PositionArticleDto>>("/api/stocks/articles"));
        if (session.A(Droits.ClientsConsulter)) Remplir(Clients, (await api.GetAsync<List<ClientDto>>("/api/clients?actif=true")).Where(c => c.Type == TypeClient.Credit));
        await ChargerVentesAsync();
    });

    private async Task ChargerVentesAsync()
        => Remplir(Ventes, (await api.GetAsync<Page<VenteDto>>($"/api/ventes?du={Jour(Du):yyyy-MM-dd}&taille=200")).Elements);

    [RelayCommand] private Task Rechercher() => ExecuterAsync(ChargerVentesAsync);

    [RelayCommand]
    private void Ajouter()
    {
        Erreur = null;
        try
        {
            if (Article is null) { Erreur = "Choisissez un article."; return; }
            var q = Nombre(Quantite, "Quantité");
            if (q <= 0) { Erreur = "La quantité doit être positive."; return; }
            var p = NombreOptionnel(Prix, "Prix") ?? Article.PrixVenteTtc ?? throw new ErreurApi(System.Net.HttpStatusCode.BadRequest, "Prix", "Aucun prix de vente en vigueur pour cet article.", null);
            var modifie = Article.PrixVenteTtc is not { } officiel || officiel != p;
            Panier.Add(new LignePanier(Article, q, p, modifie));
            Quantite = "1"; Article = null; cleVente = null;
            OnPropertyChanged(nameof(TotalPanier));
        }
        catch (ErreurApi e) { Erreur = e.Message; }
    }

    [RelayCommand]
    private void Retirer()
    {
        if (LignePanier is null) return;
        Panier.Remove(LignePanier); cleVente = null;
        OnPropertyChanged(nameof(TotalPanier));
    }

    [RelayCommand]
    private Task Encaisser() => ExecuterAsync(async () =>
    {
        if (Panier.Count == 0) throw new ErreurApi(System.Net.HttpStatusCode.BadRequest, "Panier", "Le panier est vide.", null);
        if (EstCredit && Client is null) throw new ErreurApi(System.Net.HttpStatusCode.BadRequest, "Client", "Une vente à crédit exige un client.", null);
        if (!Confirmer($"Enregistrer la vente de {TotalPanier:N2} DH TTC ({Mode}) ?")) return;
        cleVente ??= Guid.NewGuid().ToString();
        var v = await api.PostAsync<VenteDto>("/api/ventes", new CreationVenteDto(EstCredit ? Client!.Id : null, Mode, null,
            Panier.Select(l => new LigneVenteSaisieDto(l.Produit.Id, l.Quantite, l.PrixModifie ? l.PrixUnitaireTtc : null)).ToList()), cleVente);
        cleVente = null;
        Panier.Clear(); OnPropertyChanged(nameof(TotalPanier));
        await ChargerVentesAsync();
        if (session.A(Droits.StockConsulter)) Remplir(Positions, await api.GetAsync<List<PositionArticleDto>>("/api/stocks/articles"));
        Information = $"Vente {v.Numero} enregistrée : {v.TotalTtc:N2} DH." + (v.FactureId is null ? "" : " Facture client créée (crédit).");
    });

    [RelayCommand]
    private Task Annuler() => ExecuterAsync(async () =>
    {
        if (VenteSelectionnee is null || Saisir($"Motif de l'annulation de la vente {VenteSelectionnee.Numero} (stock et encaissement seront contre-passés) :") is not { } motif) return;
        await api.PostAsync<VenteDto>($"/api/ventes/{VenteSelectionnee.Id}/annulation", new MotifDto(motif));
        await ChargerVentesAsync();
    }, "Vente annulée.");
}
