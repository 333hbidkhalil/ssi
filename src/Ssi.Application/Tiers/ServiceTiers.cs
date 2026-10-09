using Microsoft.EntityFrameworkCore;
using Ssi.Application.Commun;
using Ssi.Application.Finance;
using Ssi.Domain.Calculs;
using Ssi.Domain.Entites;

namespace Ssi.Application.Tiers;

/// <summary>Clients et fournisseurs. Créances, encours et dettes sont toujours recalculés (jamais saisis).</summary>
public class ServiceTiers(ISsiDb db, IHorloge horloge, Audit audit, Soldes soldes, IUtilisateurCourant courant)
{
    // ---------- Clients ----------

    public async Task<IReadOnlyList<ClientDto>> ClientsAsync(string? q, bool? actif, CancellationToken ct)
    {
        var filtre = q?.Trim().ToLower();
        var clients = await db.Clients.AsNoTracking()
            .Where(c => (actif == null || c.Actif == actif) && (filtre == null || c.Nom.ToLower().Contains(filtre) || c.Code.ToLower().Contains(filtre)))
            .OrderBy(c => c.Nom).Take(500).ToListAsync(ct);
        var res = new List<ClientDto>();
        foreach (var c in clients) res.Add(await VersDtoAsync(c, ct));
        return res;
    }

    public async Task<ClientDto> ClientAsync(Guid id, CancellationToken ct)
        => await VersDtoAsync(Garde.Trouve(await db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct), "Client"), ct);

    private async Task<ClientDto> VersDtoAsync(Client c, CancellationToken ct)
    {
        var creance = await soldes.CreanceClientAsync(c.Id, ct);
        var bons = await soldes.BonsNonFacturesAsync(c.Id, ct);
        var encours = creance + bons;
        return new ClientDto(c.Id, c.Code, c.Nom, c.Type, c.Ice, c.IdentifiantFiscal, c.RegistreCommerce, c.Adresse, c.Ville, c.Telephone, c.Email,
            c.Contact, c.DelaiPaiementJours, c.PlafondCredit, c.Actif, c.Notes, creance, bons, encours, c.PlafondCredit > 0 && encours > c.PlafondCredit);
    }

    public async Task<ClientDto> EnregistrerClientAsync(Guid? id, EnregistrementClientDto dto, CancellationToken ct)
    {
        Garde.Requis(dto.Code, "code");
        Garde.Requis(dto.Nom, "nom");
        Garde.PositifOuNul(dto.PlafondCredit, "plafondCredit");
        if (dto.DelaiPaiementJours < 0) throw new ValidationException("Le délai de paiement ne peut pas être négatif.");
        Client c;
        object? avant = null;
        if (id is null)
        {
            c = new Client();
            db.Clients.Add(c);
        }
        else
        {
            c = Garde.Trouve(await db.Clients.FindAsync([id.Value], ct), "Client");
            avant = new { c.Type, c.PlafondCredit, c.DelaiPaiementJours, c.Actif };
            if (dto.Type == TypeClient.Comptant && c.Type == TypeClient.Credit && await soldes.BonsNonFacturesAsync(c.Id, ct) > 0)
                throw new RegleMetierException("Ce client a des bons de crédit non facturés : facturez-les avant de le passer au comptant.");
        }
        c.Code = dto.Code.Trim().ToUpperInvariant(); c.Nom = dto.Nom.Trim(); c.Type = dto.Type; c.Ice = dto.Ice; c.IdentifiantFiscal = dto.IdentifiantFiscal;
        c.RegistreCommerce = dto.RegistreCommerce; c.Adresse = dto.Adresse; c.Ville = dto.Ville; c.Telephone = dto.Telephone; c.Email = dto.Email;
        c.Contact = dto.Contact; c.DelaiPaiementJours = dto.DelaiPaiementJours; c.PlafondCredit = dto.PlafondCredit; c.Actif = dto.Actif; c.Notes = dto.Notes;
        if (avant is not null) audit.Enregistrer("MODIFICATION_CLIENT", "Client", c.Id, c.Code, null, avant, new { c.Type, c.PlafondCredit, c.DelaiPaiementJours, c.Actif });
        await db.SaveChangesAsync(ct);
        return await ClientAsync(c.Id, ct);
    }

    /// <summary>Relevé de compte client : débit = factures, crédit = avoirs et encaissements. Les bons non facturés sont signalés à part.</summary>
    public async Task<ReleveCompteDto> ReleveClientAsync(Guid id, DateOnly? du, DateOnly? au, CancellationToken ct)
    {
        var c = Garde.Trouve(await db.Clients.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct), "Client");
        var lignes = new List<(DateOnly Date, string Piece, string Libelle, decimal Debit, decimal Credit, DateOnly? Echeance)>();
        foreach (var f in await db.Factures.AsNoTracking().Where(f => f.ClientId == id && f.Statut == StatutDocument.Valide).ToListAsync(ct))
            lignes.Add(f.Type == TypeFacture.Facture
                ? (f.Date, f.Numero, $"Facture {f.Origine}", f.TotalTtc, 0m, f.Echeance)
                : (f.Date, f.Numero, "Avoir", 0m, f.TotalTtc, null));
        foreach (var r in await db.Reglements.AsNoTracking().Where(r => r.ClientId == id && r.Statut == StatutReglement.Valide).ToListAsync(ct))
            lignes.Add(r.Sens == SensReglement.Encaissement
                ? (r.Date, r.Numero, $"Règlement {r.Mode} {r.Reference}".Trim(), 0m, r.Montant, null)
                : (r.Date, r.Numero, $"Remboursement {r.Mode}", r.Montant, 0m, null));
        return Releve(c.Nom, du, au, lignes, true);
    }

    private static ReleveCompteDto Releve(string tiers, DateOnly? du, DateOnly? au,
        List<(DateOnly Date, string Piece, string Libelle, decimal Debit, decimal Credit, DateOnly? Echeance)> lignes, bool client)
    {
        var tri = lignes.OrderBy(l => l.Date).ThenBy(l => l.Piece).ToList();
        var ouverture = tri.Where(l => du.HasValue && l.Date < du).Sum(l => client ? l.Debit - l.Credit : l.Credit - l.Debit);
        var solde = ouverture;
        var res = new List<LigneReleveCompteDto>();
        foreach (var l in tri.Where(l => (!du.HasValue || l.Date >= du) && (!au.HasValue || l.Date <= au)))
        {
            solde += client ? l.Debit - l.Credit : l.Credit - l.Debit;
            res.Add(new LigneReleveCompteDto(l.Date, l.Piece, l.Libelle, l.Debit, l.Credit, solde, l.Echeance));
        }
        var sens = solde == 0 ? "Soldé" : client ? (solde > 0 ? "Le client doit à la station" : "La station doit au client")
            : (solde > 0 ? "La station doit au fournisseur" : "Le fournisseur doit à la station");
        return new ReleveCompteDto(tiers, du, au, ouverture, res, res.Sum(l => l.Debit), res.Sum(l => l.Credit), solde, sens);
    }

    public async Task<IReadOnlyList<RelanceDto>> RelancesAsync(Guid clientId, CancellationToken ct)
    {
        var noms = await db.Utilisateurs.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.NomComplet, ct);
        return (await db.Relances.AsNoTracking().Where(r => r.ClientId == clientId).OrderByDescending(r => r.Date).ToListAsync(ct))
            .Select(r => new RelanceDto(r.Id, r.ClientId, r.FactureId, r.Date, r.Moyen, r.Commentaire, r.CreePar is { } p ? noms.GetValueOrDefault(p, "?") : "?")).ToList();
    }

    public async Task<IReadOnlyList<RelanceDto>> AjouterRelanceAsync(Guid clientId, SaisieRelanceDto dto, CancellationToken ct)
    {
        Garde.Trouve(await db.Clients.FindAsync([clientId], ct), "Client");
        Garde.Requis(dto.Moyen, "moyen");
        if (dto.FactureId is { } fid && !await db.Factures.AnyAsync(f => f.Id == fid && f.ClientId == clientId, ct))
            throw new ValidationException("La facture relancée n'appartient pas à ce client.");
        db.Relances.Add(new Relance { ClientId = clientId, FactureId = dto.FactureId, Date = horloge.Aujourdhui, Moyen = dto.Moyen, Commentaire = dto.Commentaire });
        await db.SaveChangesAsync(ct);
        return await RelancesAsync(clientId, ct);
    }

    // ---------- Fournisseurs ----------

    public async Task<IReadOnlyList<FournisseurDto>> FournisseursAsync(string? q, bool? actif, CancellationToken ct)
    {
        var filtre = q?.Trim().ToLower();
        var fs = await db.Fournisseurs.AsNoTracking()
            .Where(c => (actif == null || c.Actif == actif) && (filtre == null || c.Nom.ToLower().Contains(filtre) || c.Code.ToLower().Contains(filtre)))
            .OrderBy(c => c.Nom).Take(500).ToListAsync(ct);
        var res = new List<FournisseurDto>();
        foreach (var f in fs) res.Add(await VersDtoAsync(f, ct));
        return res;
    }

    public async Task<FournisseurDto> FournisseurAsync(Guid id, CancellationToken ct)
        => await VersDtoAsync(Garde.Trouve(await db.Fournisseurs.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct), "Fournisseur"), ct);

    private async Task<FournisseurDto> VersDtoAsync(Fournisseur f, CancellationToken ct)
        => new(f.Id, f.Code, f.Nom, f.Ice, f.IdentifiantFiscal, f.RegistreCommerce, f.Adresse, f.Ville, f.Telephone, f.Email, f.Contact,
            f.DelaiPaiementJours, f.ConditionsAchat, f.ProduitsFournis, f.Actif, f.Notes, await soldes.DetteFournisseurAsync(f.Id, ct));

    public async Task<FournisseurDto> EnregistrerFournisseurAsync(Guid? id, EnregistrementFournisseurDto dto, CancellationToken ct)
    {
        Garde.Requis(dto.Code, "code");
        Garde.Requis(dto.Nom, "nom");
        if (dto.DelaiPaiementJours < 0) throw new ValidationException("Le délai de paiement ne peut pas être négatif.");
        Fournisseur f;
        if (id is null) { f = new Fournisseur(); db.Fournisseurs.Add(f); }
        else f = Garde.Trouve(await db.Fournisseurs.FindAsync([id.Value], ct), "Fournisseur");
        f.Code = dto.Code.Trim().ToUpperInvariant(); f.Nom = dto.Nom.Trim(); f.Ice = dto.Ice; f.IdentifiantFiscal = dto.IdentifiantFiscal;
        f.RegistreCommerce = dto.RegistreCommerce; f.Adresse = dto.Adresse; f.Ville = dto.Ville; f.Telephone = dto.Telephone; f.Email = dto.Email;
        f.Contact = dto.Contact; f.DelaiPaiementJours = dto.DelaiPaiementJours; f.ConditionsAchat = dto.ConditionsAchat; f.ProduitsFournis = dto.ProduitsFournis;
        f.Actif = dto.Actif; f.Notes = dto.Notes;
        await db.SaveChangesAsync(ct);
        return await FournisseurAsync(f.Id, ct);
    }

    /// <summary>Relevé fournisseur : crédit = factures et charges, débit = avoirs et paiements.</summary>
    public async Task<ReleveCompteDto> ReleveFournisseurAsync(Guid id, DateOnly? du, DateOnly? au, CancellationToken ct)
    {
        var f = Garde.Trouve(await db.Fournisseurs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct), "Fournisseur");
        var lignes = new List<(DateOnly Date, string Piece, string Libelle, decimal Debit, decimal Credit, DateOnly? Echeance)>();
        foreach (var x in await db.Factures.AsNoTracking().Where(x => x.FournisseurId == id && x.Statut == StatutDocument.Valide).ToListAsync(ct))
            lignes.Add(x.Type == TypeFacture.Facture
                ? (x.Date, x.NumeroTiers ?? x.Numero, $"Facture {x.Numero}", 0m, x.TotalTtc, x.Echeance)
                : (x.Date, x.NumeroTiers ?? x.Numero, $"Avoir {x.Numero}", x.TotalTtc, 0m, null));
        foreach (var c in await db.Charges.AsNoTracking().Where(c => c.FournisseurId == id && c.Statut == StatutDocument.Valide).ToListAsync(ct))
            lignes.Add((c.Date, c.Numero, c.Description ?? "Charge", 0m, c.MontantTtc, c.Echeance));
        foreach (var r in await db.Reglements.AsNoTracking().Where(r => r.FournisseurId == id && r.Statut == StatutReglement.Valide).ToListAsync(ct))
            lignes.Add(r.Sens == SensReglement.Decaissement
                ? (r.Date, r.Numero, $"Paiement {r.Mode} {r.Reference}".Trim(), r.Montant, 0m, null)
                : (r.Date, r.Numero, $"Remboursement reçu {r.Mode}", 0m, r.Montant, null));
        return Releve(f.Nom, du, au, lignes, false);
    }

    public async Task<IncidentFournisseurDto> AjouterIncidentAsync(Guid fournisseurId, IncidentFournisseurDto dto, CancellationToken ct)
    {
        Garde.Trouve(await db.Fournisseurs.FindAsync([fournisseurId], ct), "Fournisseur");
        Garde.Requis(dto.Type, "type");
        Garde.Requis(dto.Description, "description");
        var i = new IncidentFournisseur { FournisseurId = fournisseurId, Date = dto.Date, Type = dto.Type, Description = dto.Description, DocumentRef = dto.DocumentRef };
        db.IncidentsFournisseur.Add(i);
        await db.SaveChangesAsync(ct);
        return new IncidentFournisseurDto(i.Id, i.Date, i.Type, i.Description, i.DocumentRef);
    }

    /// <summary>Évaluation factuelle : écarts de quantité, délais de livraison par rapport à la date prévue, prix moyen, incidents.</summary>
    public async Task<EvaluationFournisseurDto> EvaluationAsync(Guid id, CancellationToken ct)
    {
        var f = Garde.Trouve(await db.Fournisseurs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct), "Fournisseur");
        var receptions = await db.LignesReception.AsNoTracking()
            .Join(db.Receptions.AsNoTracking().Where(r => r.FournisseurId == id && r.Statut == StatutDocument.Valide), l => l.ReceptionId, r => r.Id,
                (l, r) => new { l.QuantiteChargee, l.QuantiteRecue, l.PrixUnitaireHt, r.Horodatage, r.CommandeId }).ToListAsync(ct);
        var livraisons = await db.LivraisonsDirectes.AsNoTracking().Where(l => l.FournisseurId == id && l.Statut == StatutLivraison.Validee)
            .Select(l => new { l.QuantiteChargee, QuantiteRecue = l.QuantiteLivree, PrixUnitaireHt = l.PrixAchatHt, l.Horodatage, CommandeId = (Guid?)l.CommandeFournisseurId }).ToListAsync(ct);
        var toutes = receptions.Select(r => (r.QuantiteChargee, r.QuantiteRecue, r.PrixUnitaireHt, r.Horodatage, r.CommandeId))
            .Concat(livraisons.Select(l => (l.QuantiteChargee, l.QuantiteRecue, l.PrixUnitaireHt, l.Horodatage, l.CommandeId))).ToList();
        var ecarts = toutes.Where(x => x.QuantiteChargee > 0).Select(x => Math.Abs(x.QuantiteRecue - x.QuantiteChargee) / x.QuantiteChargee * 100).ToList();
        var cmdIds = toutes.Where(x => x.CommandeId.HasValue).Select(x => x.CommandeId!.Value).Distinct().ToList();
        var prevues = await db.CommandesFournisseur.AsNoTracking().Where(c => cmdIds.Contains(c.Id) && c.DatePrevue != null).ToDictionaryAsync(c => c.Id, c => c.DatePrevue!.Value, ct);
        var delais = toutes.Where(x => x.CommandeId.HasValue && prevues.ContainsKey(x.CommandeId.Value))
            .Select(x => (decimal)(horloge.JourneeDe(x.Horodatage).DayNumber - prevues[x.CommandeId!.Value].DayNumber)).ToList();
        var volume = toutes.Sum(x => x.QuantiteRecue);
        decimal? prix = courant.APermission(Securite.P.AchatsSensible) && volume > 0 ? Math.Round(toutes.Sum(x => x.QuantiteRecue * x.PrixUnitaireHt) / volume, 4) : null;
        var incidents = await db.IncidentsFournisseur.AsNoTracking().Where(i => i.FournisseurId == id).OrderByDescending(i => i.Date).ToListAsync(ct);
        return new EvaluationFournisseurDto(f.Id, f.Nom, toutes.Count, volume, ecarts.Count > 0 ? Arrondi.Pourcentage(ecarts.Average()) : 0,
            delais.Count > 0 ? Math.Round(delais.Average(), 1) : null, prix, incidents.Count,
            incidents.Take(10).Select(i => new IncidentFournisseurDto(i.Id, i.Date, i.Type, i.Description, i.DocumentRef)).ToList());
    }
}
