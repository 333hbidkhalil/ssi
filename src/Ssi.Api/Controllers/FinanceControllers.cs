using Microsoft.AspNetCore.Mvc;
using Ssi.Api.Infrastructure;
using Ssi.Application.Charges;
using Ssi.Application.Finance;
using Ssi.Application.Securite;
using Ssi.Application.Tiers;
using Ssi.Application.Ventes;
using Ssi.Contracts;

namespace Ssi.Api.Controllers;

[ApiController]
[Route("api/clients")]
public class ClientsController(ServiceTiers tiers) : ControllerBase
{
    [HttpGet, Autorise(P.ClientsConsulter)]
    public Task<IReadOnlyList<ClientDto>> Liste(string? q, bool? actif, CancellationToken ct) => tiers.ClientsAsync(q, actif, ct);

    [HttpGet("{id:guid}"), Autorise(P.ClientsConsulter)]
    public Task<ClientDto> Detail(Guid id, CancellationToken ct) => tiers.ClientAsync(id, ct);

    [HttpPost, Autorise(P.ClientsCreer)]
    public Task<ClientDto> Creer(EnregistrementClientDto dto, CancellationToken ct) => tiers.EnregistrerClientAsync(null, dto, ct);

    [HttpPut("{id:guid}"), Autorise(P.ClientsModifier)]
    public Task<ClientDto> Modifier(Guid id, EnregistrementClientDto dto, CancellationToken ct) => tiers.EnregistrerClientAsync(id, dto, ct);

    [HttpGet("{id:guid}/releve"), Autorise(P.ClientsConsulter)]
    public Task<ReleveCompteDto> Releve(Guid id, DateOnly? du, DateOnly? au, CancellationToken ct) => tiers.ReleveClientAsync(id, du, au, ct);

    [HttpGet("{id:guid}/relances"), Autorise(P.ClientsConsulter)]
    public Task<IReadOnlyList<RelanceDto>> Relances(Guid id, CancellationToken ct) => tiers.RelancesAsync(id, ct);

    [HttpPost("{id:guid}/relances"), Autorise(P.ClientsModifier)]
    public Task<IReadOnlyList<RelanceDto>> Relancer(Guid id, SaisieRelanceDto dto, CancellationToken ct) => tiers.AjouterRelanceAsync(id, dto, ct);
}

[ApiController]
[Route("api/fournisseurs")]
public class FournisseursController(ServiceTiers tiers) : ControllerBase
{
    [HttpGet, Autorise(P.FournisseursConsulter)]
    public Task<IReadOnlyList<FournisseurDto>> Liste(string? q, bool? actif, CancellationToken ct) => tiers.FournisseursAsync(q, actif, ct);

    [HttpGet("{id:guid}"), Autorise(P.FournisseursConsulter)]
    public Task<FournisseurDto> Detail(Guid id, CancellationToken ct) => tiers.FournisseurAsync(id, ct);

    [HttpPost, Autorise(P.FournisseursCreer)]
    public Task<FournisseurDto> Creer(EnregistrementFournisseurDto dto, CancellationToken ct) => tiers.EnregistrerFournisseurAsync(null, dto, ct);

    [HttpPut("{id:guid}"), Autorise(P.FournisseursModifier)]
    public Task<FournisseurDto> Modifier(Guid id, EnregistrementFournisseurDto dto, CancellationToken ct) => tiers.EnregistrerFournisseurAsync(id, dto, ct);

    [HttpGet("{id:guid}/releve"), Autorise(P.FournisseursConsulter)]
    public Task<ReleveCompteDto> Releve(Guid id, DateOnly? du, DateOnly? au, CancellationToken ct) => tiers.ReleveFournisseurAsync(id, du, au, ct);

    [HttpPost("{id:guid}/incidents"), Autorise(P.FournisseursModifier)]
    public Task<IncidentFournisseurDto> Incident(Guid id, IncidentFournisseurDto dto, CancellationToken ct) => tiers.AjouterIncidentAsync(id, dto, ct);

    [HttpGet("{id:guid}/evaluation"), Autorise(P.FournisseursConsulter)]
    public Task<EvaluationFournisseurDto> Evaluation(Guid id, CancellationToken ct) => tiers.EvaluationAsync(id, ct);
}

[ApiController]
[Route("api/ventes")]
public class VentesController(ServiceVentes ventes) : ControllerBase
{
    [HttpGet, Autorise(P.VentesConsulter)]
    public Task<Page<VenteDto>> Liste(DateOnly? du, DateOnly? au, Guid? clientId, int page = 1, int taille = 50, CancellationToken ct = default)
        => ventes.VentesAsync(du, au, clientId, page, taille, ct);

    [HttpGet("{id:guid}"), Autorise(P.VentesConsulter)]
    public Task<VenteDto> Detail(Guid id, CancellationToken ct) => ventes.VenteAsync(id, ct);

    [HttpPost, Autorise(P.VentesCreer), Idempotent]
    public Task<VenteDto> Creer(CreationVenteDto dto, CancellationToken ct) => ventes.CreerAsync(dto, ct);

    [HttpPost("{id:guid}/annulation"), Autorise(P.VentesAnnuler)]
    public Task<VenteDto> Annuler(Guid id, MotifDto dto, CancellationToken ct) => ventes.AnnulerAsync(id, dto.Motif, ct);
}

[ApiController]
[Route("api/tresorerie")]
public class TresorerieController(ServiceTresorerie tresorerie) : ControllerBase
{
    [HttpGet("situation"), Autorise(P.BanqueConsulter, P.TableauBordConsulter)]
    public Task<SituationTresorerieDto> Situation(CancellationToken ct) => tresorerie.SituationAsync(ct);

    [HttpGet("comptes"), Autorise(P.BanqueConsulter, P.CaisseConsulter)]
    public Task<IReadOnlyList<CompteFinancierDto>> Comptes(CancellationToken ct) => tresorerie.ComptesAsync(ct);

    [HttpPost("comptes"), Autorise(P.AdminParametres)]
    public Task<CompteFinancierDto> CreerCompte(EnregistrementCompteDto dto, CancellationToken ct) => tresorerie.EnregistrerCompteAsync(null, dto, ct);

    [HttpPut("comptes/{id:guid}"), Autorise(P.AdminParametres)]
    public Task<CompteFinancierDto> ModifierCompte(Guid id, EnregistrementCompteDto dto, CancellationToken ct) => tresorerie.EnregistrerCompteAsync(id, dto, ct);

    [HttpPost("comptes/solde-initial"), Autorise(P.AdminParametres), Idempotent]
    public Task<CompteFinancierDto> SoldeInitial(SoldeInitialCompteDto dto, CancellationToken ct) => tresorerie.SoldeInitialAsync(dto, ct);

    [HttpPost("comptes/{id:guid}/operations"), Autorise(P.BanqueCreer), Idempotent]
    public Task<MouvementFinancierDto> Operation(Guid id, OperationCompteDto dto, CancellationToken ct) => tresorerie.OperationAsync(id, dto, ct);

    [HttpGet("mouvements"), Autorise(P.BanqueConsulter, P.CaisseConsulter)]
    public Task<Page<MouvementFinancierDto>> Mouvements(Guid? compteId, DateOnly? du, DateOnly? au, int page = 1, int taille = 100, CancellationToken ct = default)
        => tresorerie.MouvementsAsync(compteId, du, au, page, taille, ct);

    [HttpGet("reglements"), Autorise(P.ReglementsConsulter)]
    public Task<IReadOnlyList<ReglementDto>> Reglements(SensReglement? sens, Guid? clientId, Guid? fournisseurId, DateOnly? du, DateOnly? au, CancellationToken ct)
        => tresorerie.ReglementsAsync(sens, clientId, fournisseurId, du, au, ct);

    [HttpGet("reglements/{id:guid}"), Autorise(P.ReglementsConsulter)]
    public Task<ReglementDto> Reglement(Guid id, CancellationToken ct) => tresorerie.ReglementAsync(id, ct);

    [HttpPost("reglements"), Autorise(P.ReglementsCreer), Idempotent]
    public Task<ReglementDto> CreerReglement(CreationReglementDto dto, CancellationToken ct) => tresorerie.CreerReglementAsync(dto, ct);

    [HttpPost("reglements/{id:guid}/annulation"), Autorise(P.ReglementsAnnuler)]
    public Task<ReglementDto> AnnulerReglement(Guid id, MotifDto dto, CancellationToken ct) => tresorerie.AnnulerReglementAsync(id, dto.Motif, ct);

    [HttpGet("transferts"), Autorise(P.BanqueConsulter)]
    public Task<IReadOnlyList<TransfertDto>> Transferts(CancellationToken ct) => tresorerie.TransfertsAsync(ct);

    [HttpPost("transferts"), Autorise(P.BanqueCreer), Idempotent]
    public Task<TransfertDto> Transferer(CreationTransfertDto dto, CancellationToken ct) => tresorerie.TransfererAsync(dto, ct);

    [HttpPost("transferts/{id:guid}/annulation"), Autorise(P.BanqueCreer)]
    public Task<TransfertDto> AnnulerTransfert(Guid id, MotifDto dto, CancellationToken ct) => tresorerie.AnnulerTransfertAsync(id, dto.Motif, ct);

    [HttpGet("creances"), Autorise(P.ClientsConsulter)]
    public Task<IReadOnlyList<EcheanceDto>> Creances(CancellationToken ct) => tresorerie.CreancesAsync(ct);

    [HttpGet("dettes"), Autorise(P.FournisseursConsulter)]
    public Task<IReadOnlyList<EcheanceDto>> Dettes(CancellationToken ct) => tresorerie.DettesAsync(ct);

    // Caisse
    [HttpGet("caisse/sessions"), Autorise(P.CaisseConsulter)]
    public Task<IReadOnlyList<SessionCaisseDto>> Sessions(Guid? compteId, StatutSessionCaisse? statut, CancellationToken ct) => tresorerie.SessionsAsync(compteId, statut, ct);

    [HttpGet("caisse/sessions/{id:guid}"), Autorise(P.CaisseConsulter)]
    public Task<SessionCaisseDto> Session(Guid id, CancellationToken ct) => tresorerie.SessionAsync(id, ct);

    [HttpPost("caisse/sessions"), Autorise(P.CaisseCreer)]
    public Task<SessionCaisseDto> OuvrirCaisse(OuvertureCaisseDto dto, CancellationToken ct) => tresorerie.OuvrirCaisseAsync(dto, ct);

    [HttpPost("caisse/sessions/{id:guid}/operations"), Autorise(P.CaisseCreer), Idempotent]
    public Task<SessionCaisseDto> OperationCaisse(Guid id, OperationCaisseDto dto, CancellationToken ct) => tresorerie.OperationCaisseAsync(id, dto, ct);

    [HttpPost("caisse/sessions/{id:guid}/cloture"), Autorise(P.CaisseCreer)]
    public Task<SessionCaisseDto> CloturerCaisse(Guid id, ClotureCaisseDto dto, CancellationToken ct) => tresorerie.CloturerCaisseAsync(id, dto, ct);

    [HttpPost("caisse/sessions/{id:guid}/validation"), Autorise(P.CaisseValider), Idempotent]
    public Task<SessionCaisseDto> ValiderCaisse(Guid id, MotifOptionnelDto? dto, CancellationToken ct)
        => tresorerie.ValiderCaisseAsync(id, string.IsNullOrWhiteSpace(dto?.Motif) ? null : new MotifDto(dto.Motif), ct);

    [HttpPost("caisse/sessions/{id:guid}/reouverture"), Autorise(P.CaisseRouvrir)]
    public Task<SessionCaisseDto> RouvrirCaisse(Guid id, MotifDto dto, CancellationToken ct) => tresorerie.RouvrirCaisseAsync(id, dto.Motif, ct);

    // Banque
    [HttpPost("rapprochements"), Autorise(P.BanqueRapprocher)]
    public Task<RapprochementDto> CreerRapprochement(CreationRapprochementDto dto, CancellationToken ct) => tresorerie.CreerRapprochementAsync(dto, ct);

    [HttpGet("rapprochements/{id:guid}"), Autorise(P.BanqueConsulter)]
    public Task<RapprochementDto> Rapprochement(Guid id, CancellationToken ct) => tresorerie.RapprochementAsync(id, ct);

    [HttpPost("rapprochements/{id:guid}/pointages"), Autorise(P.BanqueRapprocher)]
    public Task<RapprochementDto> Pointer(Guid id, PointageDto dto, CancellationToken ct) => tresorerie.PointerAsync(id, dto, ct);

    [HttpPost("rapprochements/{id:guid}/validation"), Autorise(P.BanqueRapprocher)]
    public Task<RapprochementDto> ValiderRapprochement(Guid id, CancellationToken ct) => tresorerie.ValiderRapprochementAsync(id, ct);
}

[ApiController]
[Route("api/charges")]
public class ChargesController(ServiceCharges charges) : ControllerBase
{
    [HttpGet, Autorise(P.ChargesConsulter)]
    public Task<IReadOnlyList<ChargeDto>> Liste(DateOnly? du, DateOnly? au, Guid? categorieId, Guid? centreId, bool nonSoldees = false, CancellationToken ct = default)
        => charges.ChargesAsync(du, au, categorieId, centreId, nonSoldees, ct);

    [HttpGet("{id:guid}"), Autorise(P.ChargesConsulter)]
    public Task<ChargeDto> Detail(Guid id, CancellationToken ct) => charges.ChargeAsync(id, ct);

    [HttpPost, Autorise(P.ChargesCreer), Idempotent]
    public Task<ChargeDto> Creer(CreationChargeDto dto, CancellationToken ct) => charges.CreerAsync(dto, ct);

    [HttpPost("{id:guid}/annulation"), Autorise(P.ChargesAnnuler)]
    public Task<ChargeDto> Annuler(Guid id, MotifDto dto, CancellationToken ct) => charges.AnnulerAsync(id, dto.Motif, ct);

    [HttpGet("categories"), Autorise(P.ChargesConsulter)]
    public Task<IReadOnlyList<CategorieChargeDto>> Categories(CancellationToken ct) => charges.CategoriesAsync(ct);

    [HttpPost("categories"), Autorise(P.ChargesParametrer)]
    public Task<CategorieChargeDto> CreerCategorie(CategorieChargeDto dto, CancellationToken ct) => charges.EnregistrerCategorieAsync(null, dto, ct);

    [HttpGet("centres"), Autorise(P.ChargesConsulter)]
    public Task<IReadOnlyList<CentreCoutDto>> Centres(CancellationToken ct) => charges.CentresAsync(ct);
}

[ApiController]
[Route("api/personnel")]
public class PersonnelController(ServiceCharges charges) : ControllerBase
{
    [HttpGet("employes"), Autorise(P.PersonnelConsulter)]
    public Task<IReadOnlyList<EmployeDto>> Employes(bool? actif, CancellationToken ct) => charges.EmployesAsync(actif, ct);

    [HttpPost("employes"), Autorise(P.PersonnelCreer)]
    public Task<EmployeDto> Creer(EnregistrementEmployeDto dto, CancellationToken ct) => charges.EnregistrerEmployeAsync(null, dto, ct);

    [HttpPut("employes/{id:guid}"), Autorise(P.PersonnelModifier)]
    public Task<EmployeDto> Modifier(Guid id, EnregistrementEmployeDto dto, CancellationToken ct) => charges.EnregistrerEmployeAsync(id, dto, ct);

    [HttpGet("remunerations"), Autorise(P.PersonnelConsulter)]
    public Task<IReadOnlyList<ElementRemunerationDto>> Elements(Guid? employeId, DateOnly? periode, CancellationToken ct) => charges.ElementsAsync(employeId, periode, ct);

    [HttpPost("remunerations"), Autorise(P.PersonnelSensible)]
    public Task<ElementRemunerationDto> AjouterElement(SaisieElementRemunerationDto dto, CancellationToken ct) => charges.AjouterElementAsync(dto, ct);

    [HttpGet("absences"), Autorise(P.PersonnelConsulter)]
    public Task<IReadOnlyList<AbsenceDto>> Absences(Guid? employeId, CancellationToken ct) => charges.AbsencesAsync(employeId, ct);

    [HttpPost("absences"), Autorise(P.PersonnelModifier)]
    public Task<AbsenceDto> AjouterAbsence(SaisieAbsenceDto dto, CancellationToken ct) => charges.AjouterAbsenceAsync(dto, ct);

    [HttpGet("masse-salariale"), Autorise(P.PersonnelConsulter)]
    public Task<MasseSalarialeDto> Masse(DateOnly mois, CancellationToken ct) => charges.MasseSalarialeAsync(mois, ct);
}
