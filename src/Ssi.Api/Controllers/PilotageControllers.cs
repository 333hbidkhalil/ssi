using Microsoft.AspNetCore.Mvc;
using Ssi.Api.Infrastructure;
using Ssi.Application.Finance;
using Ssi.Application.Pilotage;
using Ssi.Application.Securite;
using Ssi.Application.Tiers;
using Ssi.Contracts;
using Ssi.Domain.Commun;

namespace Ssi.Api.Controllers;

[ApiController]
[Route("api/pilotage")]
public class PilotageController(ServiceRentabilite rentabilite, ServicePilotage pilotage) : ControllerBase
{
    [HttpGet("tableau-de-bord"), Autorise(P.TableauBordConsulter)]
    public Task<TableauBordDto> TableauBord(CancellationToken ct) => pilotage.TableauBordAsync(ct);

    [HttpGet("alertes"), Autorise(P.TableauBordConsulter)]
    public Task<IReadOnlyList<AlerteDto>> Alertes(CancellationToken ct) => pilotage.AlertesAsync(ct);

    [HttpGet("rentabilite"), Autorise(P.RentabiliteConsulter)]
    public Task<RentabiliteDto> Rentabilite(DateOnly du, DateOnly au, CancellationToken ct) => rentabilite.RentabiliteAsync(du, au, ct);

    [HttpGet("seuil-rentabilite"), Autorise(P.RentabiliteConsulter)]
    public Task<SeuilRentabiliteDto> Seuil(DateOnly du, DateOnly au, CancellationToken ct) => rentabilite.SeuilAsync(du, au, ct);

    /// <summary>Simulation sans écriture : POST pour transporter les hypothèses, mais aucune donnée n'est modifiée.</summary>
    [HttpPost("simulations"), Autorise(P.RentabiliteSimuler)]
    public Task<SimulationDto> Simuler(SimulationDemandeDto dto, CancellationToken ct) => rentabilite.SimulerAsync(dto, ct);

    [HttpGet("controles"), Autorise(P.AuditConsulter, P.StockConsulter, P.RapportsConsulter)]
    public Task<IReadOnlyList<ResultatControleDto>> Controles(CancellationToken ct) => pilotage.ControlesAsync(ct);
}

[ApiController]
[Route("api/rapports")]
public class RapportsController(ServiceRapports rapports, ServiceSecurite securite, ServiceFactures factures, ServiceTiers tiers) : ControllerBase
{
    [HttpGet, Autorise(P.RapportsConsulter)]
    public IReadOnlyDictionary<string, string> Catalogue() => ServiceRapports.Catalogue;

    /// <summary>Rapport à l'écran (format=json) ou export (csv, xlsx, pdf) : même calcul, mêmes filtres (R-40).</summary>
    [HttpGet("{code}"), Autorise(P.RapportsConsulter)]
    public async Task<IActionResult> Rapport(string code, DateOnly? du, DateOnly? au, Guid? produitId, Guid? compteId, string format = "json", CancellationToken ct = default)
    {
        var r = await rapports.GenererAsync(code, new FiltreRapport(du, au, produitId, compteId), ct);
        if (format == "json") return Ok(r);
        if (!HttpContext.RequestServices.GetRequiredService<Application.Commun.IUtilisateurCourant>().APermission(P.RapportsExporter))
            throw new AccesRefuseException("L'export de rapports nécessite la permission rapports.exporter.");
        var nom = $"ssi-{code}-{DateTime.Now:yyyyMMdd-HHmm}";
        return format switch
        {
            "csv" => File(Exports.Csv(r), "text/csv; charset=utf-8", nom + ".csv"),
            "xlsx" => File(Exports.Excel(r), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nom + ".xlsx"),
            "pdf" => File(Exports.Pdf(r, (await securite.SocieteAsync(ct)).RaisonSociale), "application/pdf", nom + ".pdf"),
            _ => throw new ValidationException("Format inconnu : json, csv, xlsx ou pdf.")
        };
    }

    [HttpGet("factures/{id:guid}/pdf"), Autorise(P.FacturesConsulter)]
    public async Task<IActionResult> FacturePdf(Guid id, CancellationToken ct)
    {
        var f = await factures.FactureAsync(id, ct);
        if (f.Statut != StatutDocument.Valide) throw new RegleMetierException("Seule une facture validée peut être imprimée.");
        if (f.Sens != SensFacture.Client) throw new RegleMetierException("Seules les factures et avoirs émis par la station sont imprimés.");
        var s = await securite.SocieteAsync(ct);
        var c = f.TiersId is { } cid ? await tiers.ClientAsync(cid, ct) : null;
        var pdf = Exports.Facture(f, new Exports.InfosSociete(s.RaisonSociale, s.FormeJuridique, s.Ice, s.IdentifiantFiscal, s.RegistreCommerce, s.Patente, s.Cnss,
            s.Adresse, s.Ville, s.Telephone, s.Email, s.MentionsLegales), c is null ? null : new Exports.InfosTiers(c.Nom, c.Ice, c.Adresse, c.Ville));
        return File(pdf, "application/pdf", $"{f.Numero}.pdf");
    }
}
