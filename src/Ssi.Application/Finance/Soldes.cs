using Microsoft.EntityFrameworkCore;
using Ssi.Application.Commun;
using Ssi.Domain.Calculs;
using Ssi.Domain.Entites;

namespace Ssi.Application.Finance;

public sealed record SituationFacture(Guid FactureId, decimal TotalTtc, decimal Regle, decimal AvoirsImputes, decimal ResteDu, EtatReglementFacture Etat);

public sealed record SituationCharge(Guid ChargeId, decimal TotalTtc, decimal Regle, decimal ResteDu);

/// <summary>
/// Créances, dettes et restes dus recalculés à partir des documents et des règlements (jamais d'un solde saisi).
/// </summary>
public class Soldes(ISsiDb db, IHorloge horloge)
{
    /// <summary>R-21 pour un ensemble de factures (type Facture). Les avoirs ont un reste dû nul : ils sont imputés.</summary>
    public async Task<Dictionary<Guid, SituationFacture>> FacturesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        var factures = await db.Factures.AsNoTracking().Where(f => ids.Contains(f.Id)).Select(f => new { f.Id, f.Type, f.TotalTtc, f.Statut }).ToListAsync(ct);
        var regle = await db.AffectationsReglement.AsNoTracking()
            .Where(a => a.FactureId != null && ids.Contains(a.FactureId.Value) && db.Reglements.Any(r => r.Id == a.ReglementId && r.Statut == StatutReglement.Valide))
            .GroupBy(a => a.FactureId!.Value).Select(g => new { Id = g.Key, Montant = g.Sum(a => a.Montant) }).ToDictionaryAsync(x => x.Id, x => x.Montant, ct);
        var avoirs = await db.Factures.AsNoTracking()
            .Where(a => a.Type == TypeFacture.Avoir && a.Statut == StatutDocument.Valide && a.FactureOrigineId != null && ids.Contains(a.FactureOrigineId.Value))
            .GroupBy(a => a.FactureOrigineId!.Value).Select(g => new { Id = g.Key, Montant = g.Sum(a => a.TotalTtc) }).ToDictionaryAsync(x => x.Id, x => x.Montant, ct);
        return factures.ToDictionary(f => f.Id, f =>
        {
            if (f.Type == TypeFacture.Avoir || f.Statut != StatutDocument.Valide)
                return new SituationFacture(f.Id, f.TotalTtc, 0, 0, 0, EtatReglementFacture.Soldee);
            var r = regle.GetValueOrDefault(f.Id);
            var a = avoirs.GetValueOrDefault(f.Id);
            var reste = CalculsFinance.ResteDu(f.TotalTtc, r, a);
            return new SituationFacture(f.Id, f.TotalTtc, r, a, reste, CalculsFinance.Etat(f.TotalTtc, reste));
        });
    }

    public async Task<SituationFacture> FactureAsync(Guid id, CancellationToken ct) => (await FacturesAsync([id], ct))[id];

    public async Task<Dictionary<Guid, SituationCharge>> ChargesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        var charges = await db.Charges.AsNoTracking().Where(c => ids.Contains(c.Id)).Select(c => new { c.Id, c.MontantTtc, c.Statut }).ToListAsync(ct);
        var regle = await db.AffectationsReglement.AsNoTracking()
            .Where(a => a.ChargeId != null && ids.Contains(a.ChargeId.Value) && db.Reglements.Any(r => r.Id == a.ReglementId && r.Statut == StatutReglement.Valide))
            .GroupBy(a => a.ChargeId!.Value).Select(g => new { Id = g.Key, Montant = g.Sum(a => a.Montant) }).ToDictionaryAsync(x => x.Id, x => x.Montant, ct);
        return charges.ToDictionary(c => c.Id, c =>
        {
            if (c.Statut != StatutDocument.Valide) return new SituationCharge(c.Id, c.MontantTtc, 0, 0);
            var r = regle.GetValueOrDefault(c.Id);
            return new SituationCharge(c.Id, c.MontantTtc, r, c.MontantTtc - r);
        });
    }

    /// <summary>R-22 : créance d'un client (factures validées).</summary>
    public async Task<decimal> CreanceClientAsync(Guid clientId, CancellationToken ct)
    {
        var ids = await db.Factures.Where(f => f.ClientId == clientId && f.Statut == StatutDocument.Valide && f.Type == TypeFacture.Facture).Select(f => f.Id).ToListAsync(ct);
        return (await FacturesAsync(ids, ct)).Values.Sum(s => s.ResteDu) - await AvoirsNonImputesAsync(clientId, null, ct)
               - await AcomptesAsync(clientId, null, ct);
    }

    /// <summary>Part non affectée des règlements validés d'un tiers (acompte ou avance) : vient en déduction de la créance ou de la dette.</summary>
    public async Task<decimal> AcomptesAsync(Guid? clientId, Guid? fournisseurId, CancellationToken ct)
    {
        var sens = clientId != null ? SensReglement.Encaissement : SensReglement.Decaissement;
        var q = db.Reglements.Where(r => r.Statut == StatutReglement.Valide && r.Sens == sens && (clientId == null || r.ClientId == clientId) && (fournisseurId == null || r.FournisseurId == fournisseurId));
        var montant = await q.SumAsync(r => r.Montant, ct);
        var affecte = await db.AffectationsReglement.Where(a => q.Any(r => r.Id == a.ReglementId)).SumAsync(a => a.Montant, ct);
        return montant - affecte;
    }

    /// <summary>Avoirs sans facture d'origine : crédit disponible du tiers.</summary>
    private async Task<decimal> AvoirsNonImputesAsync(Guid? clientId, Guid? fournisseurId, CancellationToken ct)
        => await db.Factures.Where(f => f.Type == TypeFacture.Avoir && f.Statut == StatutDocument.Valide && f.FactureOrigineId == null
                                        && (clientId == null || f.ClientId == clientId) && (fournisseurId == null || f.FournisseurId == fournisseurId))
            .SumAsync(f => f.TotalTtc, ct);

    /// <summary>Bons de crédit station non encore facturés (y compris postes en cours) : comptés dans l'encours.</summary>
    public async Task<decimal> BonsNonFacturesAsync(Guid clientId, CancellationToken ct)
        => await db.ReglementsPoste.Where(r => r.ClientId == clientId && r.Mode == ModeReglement.Credit && r.FactureId == null).SumAsync(r => r.Montant, ct);

    /// <summary>R-23 : dette envers un fournisseur (factures validées + charges à payer non soldées).</summary>
    public async Task<decimal> DetteFournisseurAsync(Guid fournisseurId, CancellationToken ct)
    {
        var ids = await db.Factures.Where(f => f.FournisseurId == fournisseurId && f.Statut == StatutDocument.Valide && f.Type == TypeFacture.Facture).Select(f => f.Id).ToListAsync(ct);
        var charges = await db.Charges.Where(c => c.FournisseurId == fournisseurId && c.Statut == StatutDocument.Valide).Select(c => c.Id).ToListAsync(ct);
        return (await FacturesAsync(ids, ct)).Values.Sum(s => s.ResteDu) + (await ChargesAsync(charges, ct)).Values.Sum(s => s.ResteDu)
               - await AvoirsNonImputesAsync(null, fournisseurId, ct) - await AcomptesAsync(null, fournisseurId, ct);
    }

    public int JoursRetard(DateOnly echeance, decimal resteDu) => CalculsFinance.JoursRetard(echeance, horloge.Aujourdhui, resteDu);
}
