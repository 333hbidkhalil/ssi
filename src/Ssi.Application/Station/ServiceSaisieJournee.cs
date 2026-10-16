using Microsoft.EntityFrameworkCore;
using Ssi.Application.Charges;
using Ssi.Application.Commun;
using Ssi.Application.Securite;
using Ssi.Contracts;
using Ssi.Domain.Entites;

namespace Ssi.Application.Station;

/// <summary>
/// Saisie de la journée en une fois, au format du tableau tenu par l'exploitant :
/// production (index des volucompteurs), clients (bons de crédit), charges payées en caisse, solde espèces.
/// Aucune règle n'est contournée : la saisie passe par les mêmes opérations que l'écran « Postes et relevés »
/// (poste, relevés contrôlés, bons de crédit, clôture) et que l'écran « Charges », dans une seule transaction :
/// tout est enregistré, ou rien.
/// Solde attendu = production − bons clients − charges payées en caisse ; il est comparé aux espèces remises.
/// Les espèces encaissées (remises + charges payées sur la recette) forment le règlement « Espèces » du poste ;
/// à la validation du poste, la recette entre en caisse puis chaque charge en sort : le solde de caisse correspond aux espèces remises.
/// </summary>
public class ServiceSaisieJournee(ISsiDb db, IHorloge horloge, IUtilisateurCourant courant, ServicePostes postes, ServiceCharges charges)
{
    public const string CodeConfirmation = "CONFIRMATION_REQUISE";

    public async Task<PreparationJourneeDto> PreparationAsync(DateOnly journee, CancellationToken ct)
    {
        var ouvert = await db.Postes.AsNoTracking().FirstOrDefaultAsync(p => p.Statut == StatutPoste.Ouvert, ct);
        var blocage = ouvert is null ? null
            : $"Le poste {ouvert.Numero} est ouvert : terminez-le (ou annulez ses relevés) dans « Postes et relevés » avant de saisir une journée.";
        var (debut, _) = await PeriodeAsync(journee, ct);
        return new PreparationJourneeDto(journee, await postes.PreparationAsync(debut, [], ct), blocage);
    }

    public async Task<ResultatJourneeDto> EnregistrerAsync(SaisieJourneeDto dto, CancellationToken ct)
    {
        if (dto.Journee > horloge.Aujourdhui) throw new ValidationException("La journée saisie ne peut pas être dans le futur.");
        if (dto.Index.Count == 0) throw new ValidationException("Saisissez le nouvel index de chaque pistolet (même inchangé).");
        if (dto.Index.Select(i => i.PistoletId).Distinct().Count() != dto.Index.Count) throw new ValidationException("Un pistolet figure deux fois dans la production.");
        Garde.PositifOuNul(dto.EspecesRemises, "especesRemises");
        if (dto.Charges.Count > 0) Garde.Permission(courant, P.ChargesCreer);
        foreach (var c in dto.Charges)
        {
            Garde.Positif(c.Montant, "montant de la charge");
            Garde.Requis(c.Beneficiaire, "bénéficiaire de la charge");
        }
        foreach (var b in dto.Clients)
        {
            Garde.Positif(b.Volume, "volume du bon client");
            Garde.Positif(b.PrixUnitaire, "prix unitaire du bon client");
        }

        var (debut, fin) = await PeriodeAsync(dto.Journee, ct);
        var poste = await postes.OuvrirAsync(new OuverturePosteDto(null, debut, $"Saisie de la journée du {dto.Journee:dd/MM/yyyy}"), ct);
        var posteId = poste.Poste.Id;

        // Production : chaque index passe par les contrôles habituels (continuité, retour arrière, volume inhabituel).
        var avertissements = new List<string>();
        foreach (var i in dto.Index)
        {
            var r = await postes.SaisirReleveAsync(posteId, new SaisieReleveDto(i.PistoletId, null, i.IndexFin, null, null, dto.ConfirmerAvertissements, null), ct);
            if (r.Enregistre) continue;
            var nom = await db.Pistolets.AsNoTracking().Where(p => p.Id == i.PistoletId).Select(p => p.Volucompteur!.Code + "/" + p.Code).FirstAsync(ct);
            avertissements.Add($"{nom} : {(r.Anomalies.Count > 0 ? string.Join(" ", r.Anomalies.Select(a => a.Message)) : r.Message)}");
        }
        // Rien n'est conservé tant que les avertissements ne sont pas confirmés (la transaction de la requête est annulée).
        if (avertissements.Count > 0)
            throw new RegleMetierException("À vérifier avant d'enregistrer :" + Environment.NewLine + string.Join(Environment.NewLine, avertissements), CodeConfirmation);

        // Clients : bons de crédit (le montant dû est volume × prix convenu avec le client).
        var totalClients = 0m;
        foreach (var b in dto.Clients)
        {
            var montant = Math.Round(b.Volume * b.PrixUnitaire, 2, MidpointRounding.AwayFromZero);
            totalClients += montant;
            await postes.AjouterReglementAsync(posteId, new SaisieReglementPosteDto(ModeReglement.Credit, montant, b.ClientId, b.ProduitId, b.Volume,
                Vide(b.BonLivraison), null, Vide(b.BonCommande)), ct);
        }

        // Charges payées en caisse sur la recette du jour.
        var totalCharges = dto.Charges.Sum(c => c.Montant);
        var numeros = new List<string>();
        if (dto.Charges.Count > 0)
        {
            var centre = await db.CentresCout.AsNoTracking().OrderBy(c => c.Code != "COMMUN").ThenBy(c => c.Code).FirstOrDefaultAsync(ct)
                         ?? throw new RegleMetierException("Aucun centre de coût n'est paramétré.");
            foreach (var c in dto.Charges)
            {
                var charge = await charges.CreerAsync(new CreationChargeDto(dto.Journee, null, c.Beneficiaire.Trim(), c.CategorieId, centre.Id, c.Montant, 0m,
                    null, dto.Journee, $"Payée en caisse sur la recette — journée du {dto.Journee:dd/MM/yyyy} ({poste.Poste.Numero})", null, null, null), ct);
                (await db.Charges.FirstAsync(x => x.Id == charge.Id, ct)).PosteId = posteId;   // réglée par la caisse à la validation du poste
                numeros.Add(charge.Numero);
            }
        }

        await db.SaveChangesAsync(ct);

        // Espèces encaissées = espèces remises + charges payées sur la recette.
        var especes = dto.EspecesRemises + totalCharges;
        if (especes > 0)
            await postes.AjouterReglementAsync(posteId, new SaisieReglementPosteDto(ModeReglement.Especes, especes, null, null, null,
                totalCharges > 0 ? $"Remises {dto.EspecesRemises:N2} + charges payées en caisse {totalCharges:N2}" : null, null), ct);

        var detail = await postes.CloturerAsync(posteId, fin, ct);
        var production = detail.Rapport.MontantTheoriqueTtc;
        var solde = production - totalClients - totalCharges;
        return new ResultatJourneeDto(detail, production, totalClients, totalCharges, solde, dto.EspecesRemises, dto.EspecesRemises - solde, numeros);
    }

    /// <summary>
    /// Période du poste de la journée : du début de la journée (ou de la fin du poste précédent, si elle est plus tardive)
    /// à la fin de la journée (ou à maintenant, pour la journée en cours).
    /// </summary>
    private async Task<(DateTimeOffset Debut, DateTimeOffset Fin)> PeriodeAsync(DateOnly journee, CancellationToken ct)
    {
        var debut = horloge.DebutJournee(journee);
        var finJournee = horloge.DebutJournee(journee.AddDays(1)).AddSeconds(-1);
        var fin = finJournee < horloge.Maintenant ? finJournee : horloge.Maintenant;
        var dernier = await db.Postes.Where(p => p.Fin != null).MaxAsync(p => (DateTimeOffset?)p.Fin, ct);
        if (dernier.HasValue && dernier.Value > debut)
        {
            if (dernier.Value >= fin)
                throw new RegleMetierException($"Un poste couvre déjà cette journée jusqu'au {dernier:dd/MM/yyyy HH:mm} : la journée du {journee:dd/MM/yyyy} est déjà saisie.", "JOURNEE_DEJA_SAISIE");
            debut = dernier.Value.AddSeconds(1);
        }
        return (debut, fin);
    }

    private static string? Vide(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
