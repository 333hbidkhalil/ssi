using Microsoft.EntityFrameworkCore;
using Ssi.Application.Commun;
using Ssi.Domain.Commun;
using Ssi.Domain.Entites;

namespace Ssi.Application.Finance;

/// <summary>Seul point d'écriture des mouvements financiers (append-only). Solde = Σ mouvements (R-26).</summary>
public class MoteurTresorerie(ISsiDb db, IUtilisateurCourant utilisateur, IHorloge horloge)
{
    public async Task<MouvementFinancier> EcrireAsync(Guid compteId, decimal montantSigne, NatureMouvementFinancier nature,
        string libelle, DateOnly? date = null, Action<MouvementFinancier>? completer = null, CancellationToken ct = default)
    {
        if (montantSigne == 0) throw new RegleMetierException("Un mouvement financier ne peut pas être nul.");
        var compte = await db.ComptesFinanciers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == compteId, ct)
                     ?? throw new NonTrouveException("Compte financier introuvable.");
        if (!compte.Actif) throw new RegleMetierException($"Le compte {compte.Code} est inactif.");
        var mvt = new MouvementFinancier
        {
            CompteId = compteId,
            DateOperation = date ?? horloge.Aujourdhui,
            Horodatage = horloge.Maintenant,
            Montant = montantSigne,
            Nature = nature,
            Libelle = libelle,
            CreePar = utilisateur.Id
        };
        completer?.Invoke(mvt);
        // Tout mouvement d'une caisse est rattaché à la session ouverte, s'il y en a une (R-27).
        if (compte.Type == TypeCompteFinancier.Caisse && mvt.SessionCaisseId is null)
            mvt.SessionCaisseId = await db.SessionsCaisse.Where(s => s.CompteId == compteId && s.Statut == StatutSessionCaisse.Ouverte)
                .Select(s => (Guid?)s.Id).FirstOrDefaultAsync(ct);
        db.MouvementsFinanciers.Add(mvt);
        await db.SaveChangesAsync(ct);
        return mvt;
    }

    public async Task<MouvementFinancier> ContrePasserAsync(MouvementFinancier origine, string motif, CancellationToken ct = default)
        => await EcrireAsync(origine.CompteId, -origine.Montant, NatureMouvementFinancier.ContrePassation,
            $"Contre-passation : {origine.Libelle} — {motif}", null, m =>
            {
                m.MouvementAnnuleId = origine.Id;
                m.ReglementId = origine.ReglementId;
                m.TransfertId = origine.TransfertId;
                m.ChargeId = origine.ChargeId;
                m.PosteId = origine.PosteId;
                m.VenteId = origine.VenteId;
                m.DocumentRef = origine.DocumentRef;
            }, ct);

    public Task<decimal> SoldeAsync(Guid compteId, DateOnly? jusqua = null, CancellationToken ct = default)
        => db.MouvementsFinanciers.Where(m => m.CompteId == compteId && (jusqua == null || m.DateOperation <= jusqua))
            .SumAsync(m => m.Montant, ct);

    /// <summary>Compte par défaut d'un type (premier compte actif), utilisé pour les encaissements de poste.</summary>
    public async Task<CompteFinancier> CompteParDefautAsync(TypeCompteFinancier type, CancellationToken ct = default)
        => await db.ComptesFinanciers.AsNoTracking().Where(c => c.Type == type && c.Actif).OrderBy(c => c.Code).FirstOrDefaultAsync(ct)
           ?? throw new RegleMetierException($"Aucun compte financier actif de type {type} n'est paramétré.", "COMPTE_ABSENT");
}
