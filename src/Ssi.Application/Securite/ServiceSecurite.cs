using Microsoft.EntityFrameworkCore;
using Ssi.Application.Commun;
using Ssi.Contracts;
using Ssi.Domain.Commun;
using Ssi.Domain.Entites;

namespace Ssi.Application.Securite;

public sealed record ResultatConnexion(Utilisateur Utilisateur, IReadOnlyList<string> Permissions);

/// <summary>Authentification, utilisateurs, rôles, permissions, paramètres et audit.</summary>
public class ServiceSecurite(ISsiDb db, IHachageMotDePasse hachage, IHorloge horloge, Audit audit, IUtilisateurCourant courant)
{
    public const int EchecsMax = 5;
    public static readonly TimeSpan DureeVerrouillage = TimeSpan.FromMinutes(15);

    public async Task<ResultatConnexion> ConnecterAsync(ConnexionDto dto, Func<Guid, Task<HashSet<string>>> permissions, CancellationToken ct)
    {
        var login = (dto.Login ?? "").Trim().ToLowerInvariant();
        var u = await db.Utilisateurs.FirstOrDefaultAsync(x => x.Login == login, ct);
        const string refus = "Identifiant ou mot de passe incorrect.";
        if (u is null) throw new AccesRefuseException(refus);
        if (!u.Actif) throw new AccesRefuseException("Ce compte est désactivé.");
        if (u.VerrouilleJusquA > horloge.Maintenant)
            throw new AccesRefuseException($"Compte temporairement verrouillé après {EchecsMax} échecs. Réessayez après {u.VerrouilleJusquA:HH:mm} (UTC) ou contactez l'administrateur.");
        if (!hachage.Verifier(u.HashMotDePasse, dto.MotDePasse ?? ""))
        {
            u.EchecsConnexion++;
            if (u.EchecsConnexion >= EchecsMax) { u.VerrouilleJusquA = horloge.Maintenant.Add(DureeVerrouillage); u.EchecsConnexion = 0; }
            db.JournalAudit.Add(new JournalAudit { Horodatage = horloge.Maintenant, UtilisateurId = u.Id, Login = u.Login, Action = "ECHEC_CONNEXION", ObjetType = "Utilisateur", ObjetId = u.Id.ToString(), AdresseIp = courant.AdresseIp });
            await db.SaveChangesAsync(ct);
            throw new AccesRefuseException(refus);
        }
        u.EchecsConnexion = 0;
        u.VerrouilleJusquA = null;
        u.DerniereConnexion = horloge.Maintenant;
        db.JournalAudit.Add(new JournalAudit { Horodatage = horloge.Maintenant, UtilisateurId = u.Id, Login = u.Login, Action = "CONNEXION", ObjetType = "Utilisateur", ObjetId = u.Id.ToString(), AdresseIp = courant.AdresseIp });
        await db.SaveChangesAsync(ct);
        return new ResultatConnexion(u, (await permissions(u.Id)).OrderBy(x => x).ToList());
    }

    public static void ControlerRobustesse(string motDePasse)
    {
        if (string.IsNullOrEmpty(motDePasse) || motDePasse.Length < 8 || !motDePasse.Any(char.IsDigit) || !motDePasse.Any(char.IsLetter))
            throw new ValidationException("Le mot de passe doit contenir au moins 8 caractères, dont au moins une lettre et un chiffre.");
    }

    public async Task ChangerMotDePasseAsync(ChangementMotDePasseDto dto, CancellationToken ct)
    {
        var u = Garde.Trouve(await db.Utilisateurs.FirstOrDefaultAsync(x => x.Id == courant.Id, ct), "Utilisateur");
        if (!hachage.Verifier(u.HashMotDePasse, dto.AncienMotDePasse)) throw new AccesRefuseException("Mot de passe actuel incorrect.");
        ControlerRobustesse(dto.NouveauMotDePasse);
        u.HashMotDePasse = hachage.Hacher(dto.NouveauMotDePasse);
        u.DoitChangerMotDePasse = false;
        u.HorodatageSecurite = Guid.NewGuid();
        audit.Enregistrer("CHANGEMENT_MOT_DE_PASSE", "Utilisateur", u.Id, u.Login);
        await db.SaveChangesAsync(ct);
    }

    // ---------- Utilisateurs ----------

    public async Task<IReadOnlyList<UtilisateurDto>> UtilisateursAsync(CancellationToken ct)
    {
        var liste = await db.Utilisateurs.AsNoTracking().Include(u => u.Roles).ThenInclude(r => r.Role).Include(u => u.Permissions)
            .OrderBy(u => u.Login).ToListAsync(ct);
        return liste.Select(VersDto).ToList();
    }

    private UtilisateurDto VersDto(Utilisateur u) => new(u.Id, u.Login, u.NomComplet, u.Actif, u.EmployeId,
        u.Roles.Select(r => r.Role?.Code ?? "").OrderBy(x => x).ToList(),
        u.Permissions.Select(p => new PermissionIndividuelleDto(p.PermissionCode, p.Accorde)).ToList(),
        u.DerniereConnexion, u.VerrouilleJusquA > horloge.Maintenant);

    public async Task<UtilisateurDto> CreerUtilisateurAsync(CreationUtilisateurDto dto, CancellationToken ct)
    {
        Garde.Requis(dto.Login, "login");
        Garde.Requis(dto.NomComplet, "nomComplet");
        ControlerRobustesse(dto.MotDePasse);
        var u = new Utilisateur
        {
            Login = dto.Login.Trim().ToLowerInvariant(), NomComplet = dto.NomComplet.Trim(),
            HashMotDePasse = hachage.Hacher(dto.MotDePasse), DoitChangerMotDePasse = true, EmployeId = dto.EmployeId
        };
        foreach (var r in await RolesParCodesAsync(dto.Roles, ct)) u.Roles.Add(new UtilisateurRole { UtilisateurId = u.Id, RoleId = r.Id });
        db.Utilisateurs.Add(u);
        audit.Enregistrer("CREATION_UTILISATEUR", "Utilisateur", u.Id, u.Login, null, null, new { u.Login, u.NomComplet, dto.Roles });
        await db.SaveChangesAsync(ct);
        return (await UtilisateursAsync(ct)).First(x => x.Id == u.Id);
    }

    public async Task<UtilisateurDto> ModifierUtilisateurAsync(Guid id, ModificationUtilisateurDto dto, CancellationToken ct)
    {
        var motif = Garde.Motif(dto.Motif, "une modification de droits");
        var u = Garde.Trouve(await db.Utilisateurs.Include(x => x.Roles).ThenInclude(r => r.Role).Include(x => x.Permissions)
            .FirstOrDefaultAsync(x => x.Id == id, ct), "Utilisateur");
        var avant = VersDto(u);
        if (u.Id == courant.Id && !dto.Actif) throw new RegleMetierException("Vous ne pouvez pas désactiver votre propre compte.");
        u.NomComplet = dto.NomComplet.Trim();
        u.Actif = dto.Actif;
        u.EmployeId = dto.EmployeId;
        var roles = await RolesParCodesAsync(dto.Roles, ct);
        db.UtilisateurRoles.RemoveRange(u.Roles);
        u.Roles = roles.Select(r => new UtilisateurRole { UtilisateurId = u.Id, RoleId = r.Id }).ToList();
        var codesConnus = await db.Permissions.Select(p => p.Code).ToListAsync(ct);
        var inconnues = dto.PermissionsIndividuelles.Select(p => p.Code).Except(codesConnus).ToList();
        if (inconnues.Count > 0) throw new ValidationException($"Permissions inconnues : {string.Join(", ", inconnues)}");
        db.UtilisateurPermissions.RemoveRange(u.Permissions);
        u.Permissions = dto.PermissionsIndividuelles.DistinctBy(p => p.Code)
            .Select(p => new UtilisateurPermission { UtilisateurId = u.Id, PermissionCode = p.Code, Accorde = p.Accorde }).ToList();
        u.HorodatageSecurite = Guid.NewGuid();
        await db.SaveChangesAsync(ct);
        var apres = (await UtilisateursAsync(ct)).First(x => x.Id == id);
        audit.Enregistrer("MODIFICATION_DROITS", "Utilisateur", u.Id, u.Login, motif, avant, apres);
        await db.SaveChangesAsync(ct);
        return apres;
    }

    public async Task ReinitialiserMotDePasseAsync(Guid id, ReinitialisationMotDePasseDto dto, CancellationToken ct)
    {
        var motif = Garde.Motif(dto.Motif, "une réinitialisation de mot de passe");
        ControlerRobustesse(dto.NouveauMotDePasse);
        var u = Garde.Trouve(await db.Utilisateurs.FirstOrDefaultAsync(x => x.Id == id, ct), "Utilisateur");
        u.HashMotDePasse = hachage.Hacher(dto.NouveauMotDePasse);
        u.DoitChangerMotDePasse = true;
        u.VerrouilleJusquA = null;
        u.EchecsConnexion = 0;
        u.HorodatageSecurite = Guid.NewGuid();
        audit.Enregistrer("REINITIALISATION_MOT_DE_PASSE", "Utilisateur", u.Id, u.Login, motif);
        await db.SaveChangesAsync(ct);
    }

    private async Task<List<Role>> RolesParCodesAsync(IReadOnlyList<string> codes, CancellationToken ct)
    {
        var roles = await db.Roles.Where(r => codes.Contains(r.Code)).ToListAsync(ct);
        var manquants = codes.Except(roles.Select(r => r.Code)).ToList();
        if (manquants.Count > 0) throw new ValidationException($"Rôles inconnus : {string.Join(", ", manquants)}");
        return roles;
    }

    // ---------- Rôles ----------

    public async Task<IReadOnlyList<RoleDto>> RolesAsync(CancellationToken ct)
        => (await db.Roles.AsNoTracking().Include(r => r.Permissions).OrderBy(r => r.Code).ToListAsync(ct))
            .Select(r => new RoleDto(r.Id, r.Code, r.Libelle, r.Systeme, r.Permissions.Select(p => p.PermissionCode).OrderBy(x => x).ToList())).ToList();

    public async Task<IReadOnlyList<PermissionDto>> PermissionsAsync(CancellationToken ct)
        => await db.Permissions.AsNoTracking().OrderBy(p => p.Module).ThenBy(p => p.Code)
            .Select(p => new PermissionDto(p.Code, p.Module, p.Libelle, p.Sensible)).ToListAsync(ct);

    public async Task<RoleDto> EnregistrerRoleAsync(Guid? id, EnregistrementRoleDto dto, CancellationToken ct)
    {
        var motif = Garde.Motif(dto.Motif, "une modification de rôle");
        Garde.Requis(dto.Code, "code");
        var codesConnus = await db.Permissions.Select(p => p.Code).ToListAsync(ct);
        var inconnues = dto.Permissions.Except(codesConnus).ToList();
        if (inconnues.Count > 0) throw new ValidationException($"Permissions inconnues : {string.Join(", ", inconnues)}");
        Role role;
        object? avant = null;
        if (id is null)
        {
            role = new Role { Code = dto.Code.Trim().ToUpperInvariant(), Libelle = dto.Libelle };
            db.Roles.Add(role);
        }
        else
        {
            role = Garde.Trouve(await db.Roles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Id == id, ct), "Rôle");
            avant = new { role.Libelle, Permissions = role.Permissions.Select(p => p.PermissionCode).OrderBy(x => x).ToList() };
            role.Libelle = dto.Libelle;
            db.RolePermissions.RemoveRange(role.Permissions);
            role.Permissions = [];
        }
        foreach (var c in dto.Permissions.Distinct()) role.Permissions.Add(new RolePermission { RoleId = role.Id, PermissionCode = c });
        // Invalide les sessions des titulaires du rôle
        var titulaires = await db.UtilisateurRoles.Where(ur => ur.RoleId == role.Id).Select(ur => ur.UtilisateurId).ToListAsync(ct);
        foreach (var u in await db.Utilisateurs.Where(u => titulaires.Contains(u.Id)).ToListAsync(ct)) u.HorodatageSecurite = Guid.NewGuid();
        audit.Enregistrer("MODIFICATION_DROITS", "Role", role.Id, role.Code, motif, avant, new { role.Libelle, Permissions = dto.Permissions.OrderBy(x => x).ToList() });
        await db.SaveChangesAsync(ct);
        return (await RolesAsync(ct)).First(r => r.Id == role.Id);
    }

    // ---------- Paramètres et fiscalité ----------

    public async Task<IReadOnlyList<ParametreDto>> ParametresAsync(CancellationToken ct)
        => await db.Parametres.AsNoTracking().OrderBy(p => p.Cle).Select(p => new ParametreDto(p.Cle, p.Valeur, p.Type, p.Description)).ToListAsync(ct);

    public async Task<ParametreDto> ModifierParametreAsync(string cle, ModificationParametreDto dto, CancellationToken ct)
    {
        var motif = Garde.Motif(dto.Motif, "la modification d'un paramètre");
        var p = Garde.Trouve(await db.Parametres.FirstOrDefaultAsync(x => x.Cle == cle, ct), "Paramètre");
        if (p.Type is "decimal" or "entier" && dto.Valeur != "" &&
            !decimal.TryParse(dto.Valeur, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out _))
            throw new ValidationException("Valeur numérique attendue (séparateur décimal : point).");
        var avant = p.Valeur;
        p.Valeur = dto.Valeur;
        p.ModifieLe = horloge.Maintenant;
        p.ModifiePar = courant.Id;
        audit.Enregistrer("MODIFICATION_PARAMETRE", "Parametre", cle, cle, motif, new { Valeur = avant }, new { dto.Valeur });
        await db.SaveChangesAsync(ct);
        return new ParametreDto(p.Cle, p.Valeur, p.Type, p.Description);
    }

    public async Task<SocieteDto> SocieteAsync(CancellationToken ct)
    {
        var s = await db.Societes.AsNoTracking().FirstAsync(ct);
        return new SocieteDto(s.RaisonSociale, s.FormeJuridique, s.Ice, s.IdentifiantFiscal, s.RegistreCommerce, s.Patente, s.Cnss,
            s.Adresse, s.Ville, s.Telephone, s.Email, s.MentionsLegales, s.Logo is { Length: > 0 });
    }

    public async Task<SocieteDto> ModifierSocieteAsync(SocieteDto dto, byte[]? logo, CancellationToken ct)
    {
        var s = await db.Societes.FirstAsync(ct);
        var avant = await SocieteAsync(ct);
        s.RaisonSociale = dto.RaisonSociale; s.FormeJuridique = dto.FormeJuridique; s.Ice = dto.Ice; s.IdentifiantFiscal = dto.IdentifiantFiscal;
        s.RegistreCommerce = dto.RegistreCommerce; s.Patente = dto.Patente; s.Cnss = dto.Cnss; s.Adresse = dto.Adresse; s.Ville = dto.Ville;
        s.Telephone = dto.Telephone; s.Email = dto.Email; s.MentionsLegales = dto.MentionsLegales;
        if (logo is not null) s.Logo = logo;
        audit.Enregistrer("MODIFICATION_PARAMETRE", "Societe", s.Id, s.RaisonSociale, "Informations légales", avant, dto);
        await db.SaveChangesAsync(ct);
        return await SocieteAsync(ct);
    }

    public async Task<IReadOnlyList<TaxeDto>> TaxesAsync(CancellationToken ct)
        => (await db.Taxes.AsNoTracking().Include(t => t.Taux).OrderBy(t => t.Code).ToListAsync(ct))
            .Select(t => new TaxeDto(t.Id, t.Code, t.Libelle, t.Taux.OrderBy(x => x.DateDebut)
                .Select(x => new TauxTaxeDto(x.Id, x.Taux, x.DateDebut, x.DateFin, x.Commentaire)).ToList())).ToList();

    /// <summary>Ajoute un taux daté : clôt la période en cours la veille du nouveau taux (historique conservé).</summary>
    public async Task<TaxeDto> AjouterTauxAsync(Guid taxeId, NouveauTauxDto dto, CancellationToken ct)
    {
        var motif = Garde.Motif(dto.Motif, "un changement de taux fiscal");
        if (dto.Taux < 0 || dto.Taux >= 1) throw new ValidationException("Le taux s'exprime en fraction (ex. 0.20 pour 20 %).");
        var taxe = Garde.Trouve(await db.Taxes.Include(t => t.Taux).FirstOrDefaultAsync(t => t.Id == taxeId, ct), "Taxe");
        if (taxe.Taux.Any(t => t.DateDebut >= dto.DateDebut))
            throw new RegleMetierException("Un taux débute déjà à cette date ou après : les taux passés ne sont pas réécrits.");
        var enCours = taxe.Taux.Where(t => t.DateFin == null || t.DateFin >= dto.DateDebut).ToList();
        foreach (var t in enCours) t.DateFin = dto.DateDebut.AddDays(-1);
        var nouveau = new TauxTaxe { TaxeId = taxe.Id, Taux = dto.Taux, DateDebut = dto.DateDebut, Commentaire = motif };
        db.TauxTaxes.Add(nouveau);
        audit.Enregistrer("MODIFICATION_PARAMETRE", "Taxe", taxe.Id, taxe.Code, motif,
            enCours.Select(t => new { t.Taux, t.DateDebut }).ToList(), new { dto.Taux, dto.DateDebut });
        await db.SaveChangesAsync(ct);
        return (await TaxesAsync(ct)).First(t => t.Id == taxeId);
    }

    public async Task<Page<EntreeAuditDto>> AuditAsync(string? objetType, string? objetId, string? action, DateTimeOffset? du, DateTimeOffset? au,
        int page, int taille, CancellationToken ct)
    {
        var q = db.JournalAudit.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(objetType)) q = q.Where(a => a.ObjetType == objetType);
        if (!string.IsNullOrWhiteSpace(objetId)) q = q.Where(a => a.ObjetId == objetId);
        if (!string.IsNullOrWhiteSpace(action)) q = q.Where(a => a.Action == action);
        if (du.HasValue) q = q.Where(a => a.Horodatage >= du);
        if (au.HasValue) q = q.Where(a => a.Horodatage <= au);
        var total = await q.CountAsync(ct);
        taille = Math.Clamp(taille, 1, 500);
        var elements = await q.OrderByDescending(a => a.Id).Skip((Math.Max(page, 1) - 1) * taille).Take(taille)
            .Select(a => new EntreeAuditDto(a.Id, a.Horodatage, a.Login, a.Action, a.ObjetType, a.ObjetId, a.ObjetRef, a.Motif, a.Avant, a.Apres, a.AdresseIp))
            .ToListAsync(ct);
        return new Page<EntreeAuditDto>(elements, total, page, taille);
    }

    public async Task<IReadOnlyList<SauvegardeDto>> SauvegardesAsync(CancellationToken ct)
        => await db.Sauvegardes.AsNoTracking().OrderByDescending(s => s.Horodatage).Take(100)
            .Select(s => new SauvegardeDto(s.Id, s.Horodatage, s.Fichier, s.Taille, s.Sha256, s.Statut, s.Message)).ToListAsync(ct);
}
