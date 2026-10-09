using Microsoft.EntityFrameworkCore;
using Ssi.Application.Commun;
using Ssi.Application.Finance;
using Ssi.Application.Securite;
using Ssi.Domain.Entites;

namespace Ssi.Application.Charges;

/// <summary>Charges d'exploitation (§20) et personnel (§21). Une charge à payer crée une dette ; une charge payée crée aussi son règlement.</summary>
public class ServiceCharges(ISsiDb db, IHorloge horloge, Audit audit, IUtilisateurCourant courant, INumerotation numerotation, Soldes soldes, ServiceTresorerie tresorerie)
{
    public async Task<IReadOnlyList<CategorieChargeDto>> CategoriesAsync(CancellationToken ct)
        => await db.CategoriesCharges.AsNoTracking().OrderBy(c => c.Libelle).Select(c => new CategorieChargeDto(c.Id, c.Code, c.Libelle, c.FixeParDefaut)).ToListAsync(ct);

    public async Task<CategorieChargeDto> EnregistrerCategorieAsync(Guid? id, CategorieChargeDto dto, CancellationToken ct)
    {
        Garde.Requis(dto.Code, "code");
        Garde.Requis(dto.Libelle, "libelle");
        CategorieCharge c;
        if (id is null) { c = new CategorieCharge(); db.CategoriesCharges.Add(c); }
        else c = Garde.Trouve(await db.CategoriesCharges.FindAsync([id.Value], ct), "Catégorie");
        c.Code = dto.Code.Trim().ToUpperInvariant(); c.Libelle = dto.Libelle; c.FixeParDefaut = dto.FixeParDefaut;
        await db.SaveChangesAsync(ct);
        return new CategorieChargeDto(c.Id, c.Code, c.Libelle, c.FixeParDefaut);
    }

    public async Task<IReadOnlyList<CentreCoutDto>> CentresAsync(CancellationToken ct)
        => await db.CentresCout.AsNoTracking().Include(c => c.CleRepartition).OrderBy(c => c.Code)
            .Select(c => new CentreCoutDto(c.Id, c.Code, c.Libelle, c.Activite, c.CleRepartitionId, c.CleRepartition != null ? c.CleRepartition.Libelle : null)).ToListAsync(ct);

    public async Task<IReadOnlyList<ChargeDto>> ChargesAsync(DateOnly? du, DateOnly? au, Guid? categorieId, Guid? centreId, bool nonSoldees, CancellationToken ct)
    {
        var liste = await db.Charges.AsNoTracking().Include(c => c.Categorie).Include(c => c.CentreCout)
            .Where(c => (du == null || c.Date >= du) && (au == null || c.Date <= au) && (categorieId == null || c.CategorieId == categorieId) && (centreId == null || c.CentreCoutId == centreId))
            .OrderByDescending(c => c.Date).Take(1000).ToListAsync(ct);
        var s = await soldes.ChargesAsync(liste.Select(c => c.Id).ToList(), ct);
        var noms = await db.Fournisseurs.AsNoTracking().ToDictionaryAsync(f => f.Id, f => f.Nom, ct);
        return liste.Where(c => !nonSoldees || s[c.Id].ResteDu > 0).Select(c => new ChargeDto(c.Id, c.Numero, c.Date, c.FournisseurId,
            c.FournisseurId is { } f ? noms.GetValueOrDefault(f) : c.Beneficiaire, c.CategorieId, c.Categorie!.Libelle, c.CentreCoutId, c.CentreCout!.Libelle,
            c.MontantHt, c.MontantTva, c.MontantTtc, c.Fixe, c.Echeance, c.Description, c.ReferencePiece, c.Statut, s[c.Id].Regle, s[c.Id].ResteDu)).ToList();
    }

    public async Task<ChargeDto> ChargeAsync(Guid id, CancellationToken ct)
    {
        var c = Garde.Trouve(await db.Charges.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct), "Charge");
        return (await ChargesAsync(c.Date, c.Date, c.CategorieId, c.CentreCoutId, false, ct)).First(x => x.Id == id);
    }

    public async Task<ChargeDto> CreerAsync(CreationChargeDto dto, CancellationToken ct)
    {
        Garde.Positif(dto.MontantHt, "montantHt");
        Garde.PositifOuNul(dto.MontantTva, "montantTva");
        var cat = Garde.Trouve(await db.CategoriesCharges.FindAsync([dto.CategorieId], ct), "Catégorie de charge");
        Garde.Trouve(await db.CentresCout.FindAsync([dto.CentreCoutId], ct), "Centre de coût");
        if (dto.FournisseurId is null && string.IsNullOrWhiteSpace(dto.Beneficiaire)) throw new ValidationException("Précisez le fournisseur ou le bénéficiaire.");
        var delai = 0;
        if (dto.FournisseurId is { } fid) delai = Garde.Trouve(await db.Fournisseurs.FindAsync([fid], ct), "Fournisseur").DelaiPaiementJours;
        if (dto.PayeeImmediatement == ModeReglement.Credit) throw new ValidationException("Le crédit n'est pas un mode de paiement.");
        if (!string.IsNullOrWhiteSpace(dto.ReferencePiece) && dto.FournisseurId is not null
            && await db.Charges.AnyAsync(c => c.FournisseurId == dto.FournisseurId && c.ReferencePiece == dto.ReferencePiece && c.Statut == StatutDocument.Valide, ct))
            throw new ConflitException("Une charge de ce fournisseur avec la même référence de pièce existe déjà (doublon).");
        var c = new Charge
        {
            Numero = await numerotation.ProchainAsync("CHARGE", ct), Date = dto.Date, FournisseurId = dto.FournisseurId, Beneficiaire = dto.Beneficiaire,
            CategorieId = cat.Id, CentreCoutId = dto.CentreCoutId, MontantHt = dto.MontantHt, MontantTva = dto.MontantTva, MontantTtc = dto.MontantHt + dto.MontantTva,
            Fixe = dto.Fixe ?? cat.FixeParDefaut, Echeance = dto.Echeance ?? dto.Date.AddDays(delai), Description = dto.Description, ReferencePiece = dto.ReferencePiece,
            ValideLe = horloge.Maintenant, ValidePar = courant.Id
        };
        if (c.Echeance < c.Date) throw new ValidationException("L'échéance ne peut pas précéder la date.");
        db.Charges.Add(c);
        await db.SaveChangesAsync(ct);
        if (dto.PayeeImmediatement is { } mode)
            await tresorerie.CreerReglementAsync(new CreationReglementDto(SensReglement.Decaissement, null, dto.FournisseurId, dto.Beneficiaire, mode, dto.CompteId,
                dto.Date, c.MontantTtc, dto.ReferencePiece, [new AffectationSaisieDto(null, c.Id, c.MontantTtc)]), ct);
        return await ChargeAsync(c.Id, ct);
    }

    public async Task<ChargeDto> AnnulerAsync(Guid id, string? motif, CancellationToken ct)
    {
        var m = Garde.Motif(motif, "l'annulation d'une charge");
        await db.VerrouillerAsync("charges", id, ct);
        var c = Garde.Trouve(await db.Charges.FirstOrDefaultAsync(x => x.Id == id, ct), "Charge");
        await db.Entry(c).ReloadAsync(ct);
        if (c.Statut == StatutDocument.Annule) throw new ConflitException("Charge déjà annulée.");
        if ((await soldes.ChargesAsync([id], ct))[id].Regle > 0) throw new RegleMetierException("Cette charge est réglée : annulez d'abord le règlement.");
        c.Statut = StatutDocument.Annule; c.AnnuleLe = horloge.Maintenant; c.AnnulePar = courant.Id; c.MotifAnnulation = m;
        audit.Enregistrer("ANNULATION_CHARGE", "Charge", c.Id, c.Numero, m, new { c.MontantTtc }, new { Statut = "Annule" });
        await db.SaveChangesAsync(ct);
        return await ChargeAsync(id, ct);
    }

    // ---------- Personnel ----------

    private bool Sensible => courant.APermission(P.PersonnelSensible);

    public async Task<IReadOnlyList<EmployeDto>> EmployesAsync(bool? actif, CancellationToken ct)
    {
        var s = Sensible;
        return (await db.Employes.AsNoTracking().Where(e => actif == null || e.Actif == actif).OrderBy(e => e.Nom).ToListAsync(ct))
            .Select(e => new EmployeDto(e.Id, e.Matricule, e.Nom, e.Prenom, e.Cin, e.Fonction, e.DateEmbauche, e.Affectation, s ? e.SalaireBase : null, e.Telephone, e.Actif)).ToList();
    }

    public async Task<EmployeDto> EnregistrerEmployeAsync(Guid? id, EnregistrementEmployeDto dto, CancellationToken ct)
    {
        Garde.Requis(dto.Matricule, "matricule");
        Garde.Requis(dto.Nom, "nom");
        Garde.Requis(dto.Fonction, "fonction");
        Employe e;
        if (id is null) { e = new Employe(); db.Employes.Add(e); }
        else e = Garde.Trouve(await db.Employes.FindAsync([id.Value], ct), "Employé");
        // Salaire non transmis : inchangé (fiche modifiée par un utilisateur qui ne voit pas les salaires).
        var salaire = dto.SalaireBase ?? (id is null ? 0m : e.SalaireBase);
        Garde.PositifOuNul(salaire, "salaireBase");
        if (e.Fonction != dto.Fonction || e.Affectation != dto.Affectation)
            db.AffectationsEmploye.Add(new AffectationEmploye { EmployeId = e.Id, Fonction = dto.Fonction, Affectation = dto.Affectation, Debut = horloge.Aujourdhui });
        if (id is not null && e.SalaireBase != salaire)
        {
            if (!Sensible) throw new AccesRefuseException("La modification du salaire nécessite la permission personnel.sensible.");
            audit.Enregistrer("MODIFICATION_SALAIRE", "Employe", e.Id, e.Matricule, null, new { Ancien = e.SalaireBase }, new { Nouveau = salaire });
        }
        else if (id is null && salaire > 0 && !Sensible) throw new AccesRefuseException("La saisie du salaire nécessite la permission personnel.sensible.");
        e.Matricule = dto.Matricule.Trim(); e.Nom = dto.Nom; e.Prenom = dto.Prenom; e.Cin = dto.Cin; e.Fonction = dto.Fonction; e.DateEmbauche = dto.DateEmbauche;
        e.Affectation = dto.Affectation; e.SalaireBase = salaire; e.Telephone = dto.Telephone; e.Actif = dto.Actif;
        await db.SaveChangesAsync(ct);
        return (await EmployesAsync(null, ct)).First(x => x.Id == e.Id);
    }

    public async Task<IReadOnlyList<ElementRemunerationDto>> ElementsAsync(Guid? employeId, DateOnly? periode, CancellationToken ct)
    {
        var s = Sensible;
        var noms = await db.Employes.AsNoTracking().ToDictionaryAsync(e => e.Id, e => e.NomComplet, ct);
        return (await db.ElementsRemuneration.AsNoTracking().Where(x => (employeId == null || x.EmployeId == employeId) && (periode == null || x.Periode == periode))
                .OrderByDescending(x => x.Periode).ToListAsync(ct))
            .Select(x => new ElementRemunerationDto(x.Id, x.EmployeId, noms.GetValueOrDefault(x.EmployeId, "?"), x.Periode, x.Type, s ? x.Montant : null, x.Commentaire)).ToList();
    }

    public async Task<ElementRemunerationDto> AjouterElementAsync(SaisieElementRemunerationDto dto, CancellationToken ct)
    {
        Garde.Permission(courant, P.PersonnelSensible);
        Garde.Positif(dto.Montant, "montant");
        Garde.Trouve(await db.Employes.FindAsync([dto.EmployeId], ct), "Employé");
        var periode = new DateOnly(dto.Periode.Year, dto.Periode.Month, 1);
        var x = new ElementRemuneration { EmployeId = dto.EmployeId, Periode = periode, Type = dto.Type, Montant = dto.Montant, Commentaire = dto.Commentaire };
        db.ElementsRemuneration.Add(x);
        await db.SaveChangesAsync(ct);
        return (await ElementsAsync(dto.EmployeId, periode, ct)).First(e => e.Id == x.Id);
    }

    public async Task<IReadOnlyList<AbsenceDto>> AbsencesAsync(Guid? employeId, CancellationToken ct)
        => await db.Absences.AsNoTracking().Where(a => employeId == null || a.EmployeId == employeId).OrderByDescending(a => a.Debut)
            .Select(a => new AbsenceDto(a.Id, a.EmployeId, a.Debut, a.Fin, a.Type, a.Justifiee)).ToListAsync(ct);

    public async Task<AbsenceDto> AjouterAbsenceAsync(SaisieAbsenceDto dto, CancellationToken ct)
    {
        Garde.Trouve(await db.Employes.FindAsync([dto.EmployeId], ct), "Employé");
        Garde.Requis(dto.Type, "type");
        if (dto.Fin < dto.Debut) throw new ValidationException("La fin de l'absence précède son début.");
        var a = new Absence { EmployeId = dto.EmployeId, Debut = dto.Debut, Fin = dto.Fin, Type = dto.Type, Justifiee = dto.Justifiee };
        db.Absences.Add(a);
        await db.SaveChangesAsync(ct);
        return new AbsenceDto(a.Id, a.EmployeId, a.Debut, a.Fin, a.Type, a.Justifiee);
    }

    /// <summary>Masse salariale d'un mois : somme des éléments saisis (salaires, primes, charges sociales) ; avances et retenues n'en font pas partie.</summary>
    public async Task<MasseSalarialeDto> MasseSalarialeAsync(DateOnly mois, CancellationToken ct)
    {
        var periode = new DateOnly(mois.Year, mois.Month, 1);
        var effectif = await db.Employes.CountAsync(e => e.Actif, ct);
        if (!Sensible) return new MasseSalarialeDto(periode, null, null, effectif);
        var parType = await db.ElementsRemuneration.AsNoTracking().Where(x => x.Periode == periode).GroupBy(x => x.Type)
            .Select(g => new { g.Key, M = g.Sum(x => x.Montant) }).ToDictionaryAsync(x => x.Key, x => x.M, ct);
        var total = parType.Where(p => p.Key is TypeElementRemuneration.Salaire or TypeElementRemuneration.Prime or TypeElementRemuneration.ChargeSociale).Sum(p => p.Value);
        return new MasseSalarialeDto(periode, total, parType, effectif);
    }
}
