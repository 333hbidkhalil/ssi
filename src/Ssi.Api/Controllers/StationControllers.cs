using Microsoft.AspNetCore.Mvc;
using Ssi.Api.Infrastructure;
using Ssi.Application.Referentiels;
using Ssi.Application.Securite;
using Ssi.Application.Station;
using Ssi.Contracts;

namespace Ssi.Api.Controllers;

[ApiController]
[Route("api/produits")]
public class ProduitsController(ServiceCatalogue catalogue) : ControllerBase
{
    [HttpGet, Autorise(P.CatalogueConsulter)]
    public Task<IReadOnlyList<ProduitDto>> Liste(string? q, bool? actif, CategorieProduit? categorie, CancellationToken ct)
        => catalogue.ProduitsAsync(q, actif, categorie, ct);

    [HttpGet("familles"), Autorise(P.CatalogueConsulter)]
    public Task<IReadOnlyList<FamilleDto>> Familles(CancellationToken ct) => catalogue.FamillesAsync(ct);

    [HttpGet("{id:guid}"), Autorise(P.CatalogueConsulter)]
    public Task<ProduitDto> Detail(Guid id, CancellationToken ct) => catalogue.ProduitAsync(id, ct);

    [HttpPost, Autorise(P.CatalogueCreer)]
    public Task<ProduitDto> Creer(EnregistrementProduitDto dto, CancellationToken ct) => catalogue.EnregistrerAsync(null, dto, ct);

    [HttpPut("{id:guid}"), Autorise(P.CatalogueModifier)]
    public Task<ProduitDto> Modifier(Guid id, EnregistrementProduitDto dto, CancellationToken ct) => catalogue.EnregistrerAsync(id, dto, ct);

    [HttpGet("{id:guid}/prix"), Autorise(P.PrixConsulter)]
    public Task<IReadOnlyList<PrixDto>> Prix(Guid id, CancellationToken ct) => catalogue.HistoriquePrixAsync(id, ct);

    [HttpPost("{id:guid}/prix"), Autorise(P.PrixValider), Idempotent]
    public Task<IReadOnlyList<PrixDto>> NouveauPrix(Guid id, NouveauPrixDto dto, CancellationToken ct) => catalogue.NouveauPrixAsync(id, dto, ct);
}

[ApiController]
[Route("api/equipements")]
public class EquipementsController(ServiceEquipements equipements) : ControllerBase
{
    [HttpGet("cuves"), Autorise(P.StationConsulter, P.StockConsulter)]
    public Task<IReadOnlyList<CuveDto>> Cuves(CancellationToken ct) => equipements.CuvesAsync(ct);

    [HttpPost("cuves"), Autorise(P.StationParametrer)]
    public Task<CuveDto> CreerCuve(EnregistrementCuveDto dto, CancellationToken ct) => equipements.EnregistrerCuveAsync(null, dto, ct);

    [HttpPut("cuves/{id:guid}"), Autorise(P.StationParametrer)]
    public Task<CuveDto> ModifierCuve(Guid id, EnregistrementCuveDto dto, CancellationToken ct) => equipements.EnregistrerCuveAsync(id, dto, ct);

    [HttpGet("volucompteurs"), Autorise(P.StationConsulter)]
    public Task<IReadOnlyList<VolucompteurDto>> Volucompteurs(CancellationToken ct) => equipements.VolucompteursAsync(ct);

    [HttpPost("volucompteurs"), Autorise(P.StationParametrer)]
    public Task<VolucompteurDto> CreerVolucompteur(EnregistrementVolucompteurDto dto, CancellationToken ct) => equipements.EnregistrerVolucompteurAsync(null, dto, ct);

    [HttpPut("volucompteurs/{id:guid}"), Autorise(P.StationParametrer)]
    public Task<VolucompteurDto> ModifierVolucompteur(Guid id, EnregistrementVolucompteurDto dto, CancellationToken ct) => equipements.EnregistrerVolucompteurAsync(id, dto, ct);

    [HttpPost("volucompteurs/{id:guid}/pistolets"), Autorise(P.StationParametrer)]
    public Task<PistoletDto> CreerPistolet(Guid id, EnregistrementPistoletDto dto, CancellationToken ct) => equipements.EnregistrerPistoletAsync(id, null, dto, ct);

    [HttpPut("volucompteurs/{id:guid}/pistolets/{pistoletId:guid}"), Autorise(P.StationParametrer)]
    public Task<PistoletDto> ModifierPistolet(Guid id, Guid pistoletId, EnregistrementPistoletDto dto, CancellationToken ct)
        => equipements.EnregistrerPistoletAsync(id, pistoletId, dto, ct);
}

[ApiController]
[Route("api/postes")]
public class PostesController(ServicePostes postes) : ControllerBase
{
    [HttpGet, Autorise(P.StationConsulter)]
    public Task<Page<PosteResumeDto>> Liste(DateOnly? du, DateOnly? au, StatutPoste? statut, int page = 1, int taille = 50, CancellationToken ct = default)
        => postes.PostesAsync(du, au, statut, page, taille, ct);

    [HttpGet("{id:guid}"), Autorise(P.StationConsulter)]
    public Task<PosteDetailDto> Detail(Guid id, CancellationToken ct) => postes.DetailAsync(id, ct);

    [HttpPost, Autorise(P.StationCreer)]
    public Task<PosteDetailDto> Ouvrir(OuverturePosteDto dto, CancellationToken ct) => postes.OuvrirAsync(dto, ct);

    [HttpGet("{id:guid}/preparation"), Autorise(P.StationConsulter)]
    public Task<IReadOnlyList<PreparationReleveDto>> Preparation(Guid id, CancellationToken ct) => postes.PreparationAsync(id, ct);

    [HttpPost("{id:guid}/releves"), Autorise(P.StationCreer)]
    public Task<ResultatReleveDto> SaisirReleve(Guid id, SaisieReleveDto dto, CancellationToken ct) => postes.SaisirReleveAsync(id, dto, ct);

    [HttpPut("{id:guid}/releves/{releveId:guid}"), Autorise(P.StationModifier)]
    public Task<ResultatReleveDto> ModifierReleve(Guid id, Guid releveId, SaisieReleveDto dto, CancellationToken ct) => postes.ModifierReleveAsync(id, releveId, dto, ct);

    [HttpPost("{id:guid}/releves/{releveId:guid}/annulation"), Autorise(P.StationAnnuler)]
    public async Task<IActionResult> AnnulerReleve(Guid id, Guid releveId, MotifDto dto, CancellationToken ct)
    {
        await postes.AnnulerReleveAsync(id, releveId, dto.Motif, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/reglements"), Autorise(P.StationCreer)]
    public Task<ReglementPosteDto> AjouterReglement(Guid id, SaisieReglementPosteDto dto, CancellationToken ct) => postes.AjouterReglementAsync(id, dto, ct);

    [HttpDelete("{id:guid}/reglements/{reglementId:guid}"), Autorise(P.StationCreer)]
    public async Task<IActionResult> SupprimerReglement(Guid id, Guid reglementId, CancellationToken ct)
    {
        await postes.SupprimerReglementAsync(id, reglementId, ct);
        return NoContent();
    }

    public sealed record ClotureDto(DateTimeOffset? Fin);

    [HttpPost("{id:guid}/cloture"), Autorise(P.StationValider, P.StationCreer)]
    public Task<PosteDetailDto> Cloturer(Guid id, ClotureDto? dto, CancellationToken ct) => postes.CloturerAsync(id, dto?.Fin, ct);

    [HttpPost("{id:guid}/validation"), Autorise(P.StationValider), Idempotent]
    public Task<PosteDetailDto> Valider(Guid id, CancellationToken ct) => postes.ValiderAsync(id, ct);

    [HttpPost("{id:guid}/reouverture"), Autorise(P.StationRouvrir)]
    public Task<PosteDetailDto> Rouvrir(Guid id, MotifDto dto, CancellationToken ct) => postes.RouvrirAsync(id, dto.Motif, ct);

    [HttpGet("evenements-compteur"), Autorise(P.StationConsulter)]
    public Task<IReadOnlyList<EvenementCompteurDto>> Evenements(Guid? pistoletId, CancellationToken ct) => postes.EvenementsAsync(pistoletId, ct);

    [HttpPost("pistolets/{pistoletId:guid}/evenements-compteur"), Autorise(P.StationCreer)]
    public Task<EvenementCompteurDto> DemanderEvenement(Guid pistoletId, SaisieEvenementCompteurDto dto, CancellationToken ct)
        => postes.DemanderEvenementAsync(pistoletId, dto, ct);

    [HttpPost("evenements-compteur/{id:guid}/decision"), Autorise(P.StationCorrigerIndex)]
    public Task<EvenementCompteurDto> DeciderEvenement(Guid id, DecisionDto dto, CancellationToken ct) => postes.DeciderEvenementAsync(id, dto, ct);
}
