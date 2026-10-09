using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ssi.Client.Wpf.Services;
using Ssi.Contracts;

namespace Ssi.Client.Wpf.ViewModels;

/// <summary>Cuves, jaugeages et écarts, ajustements soumis à approbation, stock d'articles et inventaires.</summary>
public sealed partial class StocksViewModel(ClientApi api, Session session) : PageViewModel
{
    public override string Titre => "Stocks et jaugeages";

    public ObservableCollection<EtatCuveDto> Cuves { get; } = [];
    public ObservableCollection<JaugeageDto> Jaugeages { get; } = [];
    public ObservableCollection<AjustementDto> Ajustements { get; } = [];
    public ObservableCollection<PositionArticleDto> Articles { get; } = [];
    public ObservableCollection<MouvementStockDto> Mouvements { get; } = [];
    public ObservableCollection<ControleCoherenceDto> Controles { get; } = [];
    public IReadOnlyList<MethodeMesure> Methodes { get; } = Enum.GetValues<MethodeMesure>();
    public IReadOnlyList<CauseEcart> Causes { get; } = Enum.GetValues<CauseEcart>();

    [ObservableProperty] private EtatCuveDto? cuve;
    [ObservableProperty] private string volumeMesure = "";
    [ObservableProperty] private string? hauteur;
    [ObservableProperty] private string? temperature;
    [ObservableProperty] private MethodeMesure methode = MethodeMesure.JaugeManuelle;
    [ObservableProperty] private JaugeageDto? jaugeage;
    [ObservableProperty] private CauseEcart cause = CauseEcart.NonDeterminee;
    [ObservableProperty] private AjustementDto? ajustement;
    [ObservableProperty] private PositionArticleDto? article;
    [ObservableProperty] private string quantiteComptee = "";
    [ObservableProperty] private bool seulementEnAttente = true;

    public bool PeutSaisir => session.A(Droits.StockCreer);
    public bool PeutAjuster => session.A(Droits.StockAjuster);
    public bool PeutApprouver => session.A(Droits.StockApprouver);

    public override Task ChargerAsync() => ExecuterAsync(async () =>
    {
        Remplir(Cuves, await api.GetAsync<List<EtatCuveDto>>("/api/stocks/cuves"));
        Cuve = Cuves.FirstOrDefault(c => c.CuveId == Cuve?.CuveId) ?? Cuves.FirstOrDefault();
        Remplir(Jaugeages, (await api.GetAsync<List<JaugeageDto>>("/api/stocks/jaugeages")).OrderByDescending(j => j.Horodatage).Take(100));
        await ChargerAjustementsAsync();
        Remplir(Articles, await api.GetAsync<List<PositionArticleDto>>("/api/stocks/articles"));
        Remplir(Mouvements, (await api.GetAsync<Page<MouvementStockDto>>($"/api/stocks/mouvements?du={DateTime.Today.AddDays(-7):yyyy-MM-dd}&taille=300")).Elements);
    });

    private async Task ChargerAjustementsAsync()
        => Remplir(Ajustements, await api.GetAsync<List<AjustementDto>>(SeulementEnAttente ? "/api/stocks/ajustements?statut=Demande" : "/api/stocks/ajustements"));

    async partial void OnSeulementEnAttenteChanged(bool value) => await ExecuterAsync(ChargerAjustementsAsync);

    [RelayCommand]
    private Task SaisirJaugeage() => ExecuterAsync(async () =>
    {
        if (Cuve is null) return;
        var j = await api.PostAsync<JaugeageDto>("/api/stocks/jaugeages", new SaisieJaugeageDto(Cuve.CuveId, null, Nombre(VolumeMesure, "Volume mesuré"),
            NombreOptionnel(Hauteur, "Hauteur"), NombreOptionnel(Temperature, "Température"), Methode));
        VolumeMesure = ""; Hauteur = null; Temperature = null;
        await ChargerAsync();
        Jaugeage = Jaugeages.FirstOrDefault(x => x.Id == j.Id);
        Information = $"Jaugeage {j.Cuve} : mesuré {j.VolumeMesure:N0} L, théorique {j.StockTheorique:N0} L, écart {j.EcartLitres:N0} L"
                      + (j.HorsTolerance ? " — HORS TOLÉRANCE : expliquez l'écart." : " (dans la tolérance).");
    });

    [RelayCommand]
    private Task Expliquer() => ExecuterAsync(async () =>
    {
        if (Jaugeage is null || Saisir($"Explication de l'écart de {Jaugeage.EcartLitres:N0} L sur {Jaugeage.Cuve} (cause : {Cause}) :", "Explication d'écart") is not { } texte) return;
        await api.PostAsync<JaugeageDto>($"/api/stocks/jaugeages/{Jaugeage.Id}/explication", new ExplicationEcartDto(Cause, texte));
        await ChargerAsync();
    }, "Explication enregistrée.");

    /// <summary>Demande de régularisation du stock théorique à hauteur de l'écart mesuré ; elle n'a d'effet qu'après approbation.</summary>
    [RelayCommand]
    private Task DemanderAjustement() => ExecuterAsync(async () =>
    {
        if (Jaugeage is null) return;
        var cuveJ = Cuves.FirstOrDefault(c => c.CuveId == Jaugeage.CuveId);
        var produitId = (await api.GetAsync<List<CuveDto>>("/api/equipements/cuves")).First(c => c.Id == Jaugeage.CuveId).ProduitId;
        if (Saisir($"Régulariser {Jaugeage.EcartLitres:N0} L sur {Jaugeage.Cuve} ({cuveJ?.Produit}).\nCause retenue : {Cause}.\nMotif de la demande :", "Demande d'ajustement") is not { } motif) return;
        await api.PostAsync<AjustementDto>("/api/stocks/ajustements", new DemandeAjustementDto(produitId, Jaugeage.CuveId, Jaugeage.EcartLitres, Cause, motif, Jaugeage.Id));
        SeulementEnAttente = true;
        await ChargerAjustementsAsync();
    }, "Demande d'ajustement transmise pour approbation.");

    [RelayCommand] private Task Approuver() => Decider(true);
    [RelayCommand] private Task Rejeter() => Decider(false);

    private Task Decider(bool approuve) => ExecuterAsync(async () =>
    {
        if (Ajustement is null) return;
        var commentaire = approuve ? null : Saisir($"Motif du rejet de {Ajustement.Numero} :");
        if (!approuve && commentaire is null) return;
        if (approuve && !Confirmer($"Approuver l'ajustement {Ajustement.Numero} de {Ajustement.Quantite:N3} sur {Ajustement.Cuve ?? Ajustement.Produit} ? Le mouvement de stock sera créé.")) return;
        await api.PostAsync<AjustementDto>($"/api/stocks/ajustements/{Ajustement.Id}/decision", new DecisionDto(approuve, commentaire), Guid.NewGuid().ToString());
        await ChargerAsync();
    }, approuve ? "Ajustement approuvé et comptabilisé en stock." : "Ajustement rejeté.");

    [RelayCommand]
    private Task Inventorier() => ExecuterAsync(async () =>
    {
        if (Article is null) return;
        var q = Nombre(QuantiteComptee, "Quantité comptée");
        if (Saisir($"{Article.Designation} : stock théorique {Article.Quantite:N2}, compté {q:N2}.\nMotif / référence de l'inventaire :", "Inventaire") is not { } motif) return;
        var a = await api.PostAsync<AjustementDto?>("/api/stocks/inventaires", new InventaireArticleDto(Article.ProduitId, q, motif));
        QuantiteComptee = "";
        await ChargerAsync();
        Information = a is null ? "Inventaire conforme : aucun écart." : $"Écart constaté : demande {a.Numero} de {a.Quantite:N2} en attente d'approbation.";
    });

    [RelayCommand]
    private Task Controler() => ExecuterAsync(async () =>
    {
        Remplir(Controles, await api.GetAsync<List<ControleCoherenceDto>>("/api/stocks/controle-coherence"));
        Information = Controles.All(c => c.Coherent) ? "Stocks cohérents : chaque position est égale à la somme de ses mouvements." : null;
        if (Controles.Any(c => !c.Coherent)) Erreur = $"{Controles.Count(c => !c.Coherent)} incohérence(s) à examiner.";
    });
}
