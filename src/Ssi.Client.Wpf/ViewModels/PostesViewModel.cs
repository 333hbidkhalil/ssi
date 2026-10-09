using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ssi.Client.Wpf.Services;
using Ssi.Contracts;

namespace Ssi.Client.Wpf.ViewModels;

/// <summary>Ligne de saisie d'un index de fin pour un pistolet.</summary>
public sealed partial class LigneSaisieIndex : ObservableObject
{
    public required PreparationReleveDto Preparation { get; init; }
    [ObservableProperty] private string indexFin = "";
    [ObservableProperty] private string? message;
    [ObservableProperty] private bool enregistre;
    public decimal? VolumeCalcule => decimal.TryParse(IndexFin.Replace('.', ','), NumberStyles.Number, CultureInfo.CurrentCulture, out var f) ? f - Preparation.IndexDebutPropose : null;
    partial void OnIndexFinChanged(string value) => OnPropertyChanged(nameof(VolumeCalcule));
}

public sealed partial class PostesViewModel(ClientApi api, Session session) : PageViewModel
{
    public override string Titre => "Postes et relevés d'index";

    public ObservableCollection<PosteResumeDto> Postes { get; } = [];
    public ObservableCollection<LigneSaisieIndex> Saisies { get; } = [];
    public ObservableCollection<ClientDto> ClientsCredit { get; } = [];
    public ObservableCollection<ProduitDto> Carburants { get; } = [];
    public IReadOnlyList<ModeReglement> Modes { get; } = Enum.GetValues<ModeReglement>().Where(m => m != ModeReglement.Autre).ToList();

    [ObservableProperty] private PosteResumeDto? posteSelectionne;
    [ObservableProperty] private PosteDetailDto? detail;
    [ObservableProperty] private ModeReglement nouveauMode = ModeReglement.Especes;
    [ObservableProperty] private string nouveauMontant = "";
    [ObservableProperty] private ClientDto? nouveauClient;
    [ObservableProperty] private ProduitDto? nouveauProduit;
    [ObservableProperty] private string nouveauVolume = "";
    [ObservableProperty] private string? nouvelleReference;
    [ObservableProperty] private ReglementPosteDto? reglementSelectionne;
    [ObservableProperty] private ReleveDto? releveSelectionne;

    // Une clé d'idempotence par validation de poste, conservée jusqu'au succès (une nouvelle tentative après coupure ne double rien).
    private readonly Dictionary<Guid, string> clesValidation = [];

    public bool PeutCreer => session.A(Droits.StationCreer);
    public bool PeutValider => session.A(Droits.StationValider);
    public bool PeutRouvrir => session.A(Droits.StationRouvrir);
    public bool EstCredit => NouveauMode == ModeReglement.Credit;
    partial void OnNouveauModeChanged(ModeReglement value) => OnPropertyChanged(nameof(EstCredit));

    public override Task ChargerAsync() => ExecuterAsync(async () =>
    {
        var page = await api.GetAsync<Page<PosteResumeDto>>($"/api/postes?du={DateTime.Today.AddDays(-7):yyyy-MM-dd}&taille=100");
        Remplir(Postes, page.Elements);
        if (session.A(Droits.ClientsConsulter))
            Remplir(ClientsCredit, (await api.GetAsync<List<ClientDto>>("/api/clients?actif=true")).Where(c => c.Type == TypeClient.Credit));
        Remplir(Carburants, await api.GetAsync<List<ProduitDto>>("/api/produits?actif=true&categorie=Carburant"));
        var courant = PosteSelectionne?.Id ?? Postes.FirstOrDefault(p => p.Statut != StatutPoste.Valide)?.Id;
        PosteSelectionne = Postes.FirstOrDefault(p => p.Id == courant);
        if (PosteSelectionne is not null) await ChargerDetailAsync(PosteSelectionne.Id);
    });

    async partial void OnPosteSelectionneChanged(PosteResumeDto? value)
    {
        if (value is not null && Detail?.Poste.Id != value.Id) await ExecuterAsync(() => ChargerDetailAsync(value.Id));
    }

    private async Task ChargerDetailAsync(Guid id)
    {
        Detail = await api.GetAsync<PosteDetailDto>($"/api/postes/{id}");
        Saisies.Clear();
        if (Detail.Poste.Statut == StatutPoste.Ouvert)
            foreach (var p in await api.GetAsync<List<PreparationReleveDto>>($"/api/postes/{id}/preparation"))
                Saisies.Add(new LigneSaisieIndex { Preparation = p, Enregistre = p.DejaReleve, Message = p.DejaReleve ? "Relevé saisi" : null });
    }

    private async Task RafraichirAsync()
    {
        var id = Detail!.Poste.Id;
        await ChargerDetailAsync(id);
        var page = await api.GetAsync<Page<PosteResumeDto>>($"/api/postes?du={DateTime.Today.AddDays(-7):yyyy-MM-dd}&taille=100");
        Remplir(Postes, page.Elements);
        PosteSelectionne = Postes.FirstOrDefault(p => p.Id == id);
    }

    [RelayCommand]
    private Task OuvrirPoste() => ExecuterAsync(async () =>
    {
        var d = await api.PostAsync<PosteDetailDto>("/api/postes", new OuverturePosteDto(null, null, null));
        Detail = d;
        await RafraichirAsync();
    }, "Poste ouvert.");

    [RelayCommand]
    private Task EnregistrerReleve(LigneSaisieIndex ligne) => ExecuterAsync(async () =>
    {
        var fin = Nombre(ligne.IndexFin, $"{ligne.Preparation.Pistolet} — index de fin");
        var url = $"/api/postes/{Detail!.Poste.Id}/releves";
        var r = await api.PostAsync<ResultatReleveDto>(url, new SaisieReleveDto(ligne.Preparation.PistoletId, null, fin, null, null, false, null));
        if (!r.Enregistre && r.Anomalies.Count > 0 && r.Anomalies.All(a => !a.Bloquante)
            && Confirmer($"{ligne.Preparation.Pistolet} : {string.Join(Environment.NewLine, r.Anomalies.Select(a => a.Message))}\n\nConfirmez-vous l'index saisi ?", "Contrôle de l'index"))
            r = await api.PostAsync<ResultatReleveDto>(url, new SaisieReleveDto(ligne.Preparation.PistoletId, null, fin, null, null, true, null));
        ligne.Message = r.Enregistre ? $"Enregistré : {r.Releve!.Volume:N3} L — {r.Releve.MontantTtc:N2} DH" : r.Message;
        ligne.Enregistre = r.Enregistre;
        if (r.Enregistre) Detail = await api.GetAsync<PosteDetailDto>($"/api/postes/{Detail.Poste.Id}");
    });

    [RelayCommand]
    private Task AnnulerReleve() => ExecuterAsync(async () =>
    {
        if (ReleveSelectionne is null || Saisir("Motif de l'annulation du relevé (obligatoire) :") is not { } motif) return;
        await api.PostAsync($"/api/postes/{Detail!.Poste.Id}/releves/{ReleveSelectionne.Id}/annulation", new MotifDto(motif));
        await RafraichirAsync();
    }, "Relevé annulé (conservé dans l'historique).");

    [RelayCommand]
    private Task AjouterReglement() => ExecuterAsync(async () =>
    {
        var montant = Nombre(NouveauMontant, "Montant");
        var volume = NombreOptionnel(NouveauVolume, "Volume");
        await api.PostAsync<ReglementPosteDto>($"/api/postes/{Detail!.Poste.Id}/reglements",
            new SaisieReglementPosteDto(NouveauMode, montant, EstCredit ? NouveauClient?.Id : null, EstCredit ? NouveauProduit?.Id : null, EstCredit ? volume : null, NouvelleReference, null));
        NouveauMontant = ""; NouveauVolume = ""; NouvelleReference = null;
        await RafraichirAsync();
    });

    [RelayCommand]
    private Task SupprimerReglement() => ExecuterAsync(async () =>
    {
        if (ReglementSelectionne is null || !Confirmer($"Supprimer le règlement {ReglementSelectionne.Mode} de {ReglementSelectionne.Montant:N2} DH ?")) return;
        await api.DeleteAsync($"/api/postes/{Detail!.Poste.Id}/reglements/{ReglementSelectionne.Id}");
        await RafraichirAsync();
    });

    [RelayCommand]
    private Task Cloturer() => ExecuterAsync(async () =>
    {
        var r = Detail!.Rapport;
        var avertissement = r.PistoletsNonReleves.Count > 0 ? $"\n\nPistolets non relevés : {string.Join(", ", r.PistoletsNonReleves)}" : "";
        if (!Confirmer($"Clôturer le poste {Detail.Poste.Numero} ?\nVentes théoriques : {r.MontantTheoriqueTtc:N2} DH\nRèglements : {r.TotalReglements:N2} DH\nÉcart : {r.Ecart:N2} DH{avertissement}")) return;
        await api.PostAsync<PosteDetailDto>($"/api/postes/{Detail.Poste.Id}/cloture", new { });
        await RafraichirAsync();
    }, "Poste clôturé.");

    [RelayCommand]
    private Task Valider() => ExecuterAsync(async () =>
    {
        var id = Detail!.Poste.Id;
        if (!Confirmer($"Valider le poste {Detail.Poste.Numero} ? Les ventes, sorties de stock et encaissements seront comptabilisés. Toute correction ultérieure passera par une réouverture motivée.")) return;
        if (!clesValidation.TryGetValue(id, out var cle)) clesValidation[id] = cle = Guid.NewGuid().ToString();
        await api.PostAsync<PosteDetailDto>($"/api/postes/{id}/validation", null, cle);
        clesValidation.Remove(id);
        await RafraichirAsync();
    }, "Poste validé : ventes, stocks et trésorerie mis à jour.");

    [RelayCommand]
    private Task Rouvrir() => ExecuterAsync(async () =>
    {
        if (Saisir($"Motif de la réouverture du poste {Detail!.Poste.Numero} (les écritures seront contre-passées et tracées) :") is not { } motif) return;
        await api.PostAsync<PosteDetailDto>($"/api/postes/{Detail.Poste.Id}/reouverture", new MotifDto(motif));
        await RafraichirAsync();
    }, "Poste rouvert.");
}
