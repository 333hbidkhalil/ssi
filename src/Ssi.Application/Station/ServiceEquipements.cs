using Microsoft.EntityFrameworkCore;
using Ssi.Application.Commun;
using Ssi.Contracts;
using Ssi.Domain.Commun;
using Ssi.Domain.Entites;

namespace Ssi.Application.Station;

/// <summary>Paramétrage des cuves, volucompteurs et pistolets.</summary>
public class ServiceEquipements(ISsiDb db, Audit audit)
{
    public async Task<IReadOnlyList<CuveDto>> CuvesAsync(CancellationToken ct)
        => await db.Cuves.AsNoTracking().Include(c => c.Produit).OrderBy(c => c.Code)
            .Select(c => new CuveDto(c.Id, c.Code, c.ProduitId, c.Produit!.Designation, c.Capacite, c.SeuilMinimal,
                c.ToleranceEcartLitres, c.ToleranceEcartPct, c.Actif, c.Observations)).ToListAsync(ct);

    public async Task<CuveDto> EnregistrerCuveAsync(Guid? id, EnregistrementCuveDto dto, CancellationToken ct)
    {
        Garde.Requis(dto.Code, "code");
        Garde.Positif(dto.Capacite, "capacite");
        Garde.PositifOuNul(dto.SeuilMinimal, "seuilMinimal");
        Garde.PositifOuNul(dto.ToleranceEcartLitres, "toleranceEcartLitres");
        Garde.PositifOuNul(dto.ToleranceEcartPct, "toleranceEcartPct");
        var produit = Garde.Trouve(await db.Produits.Include(p => p.Famille).FirstOrDefaultAsync(p => p.Id == dto.ProduitId, ct), "Produit");
        if (produit.Famille!.Categorie != CategorieProduit.Carburant) throw new ValidationException("Une cuve ne peut contenir qu'un carburant.");
        Cuve c;
        if (id is null) { c = new Cuve(); db.Cuves.Add(c); }
        else
        {
            c = Garde.Trouve(await db.Cuves.FindAsync([id.Value], ct), "Cuve");
            if (c.ProduitId != dto.ProduitId && await db.MouvementsStock.AnyAsync(m => m.CuveId == c.Id, ct))
                throw new RegleMetierException("Le produit d'une cuve ayant des mouvements ne peut pas être changé.");
            if (c.ToleranceEcartLitres != dto.ToleranceEcartLitres || c.ToleranceEcartPct != dto.ToleranceEcartPct)
                audit.Enregistrer("MODIFICATION_PARAMETRE", "Cuve", c.Id, c.Code, "Tolérances d'écart",
                    new { c.ToleranceEcartLitres, c.ToleranceEcartPct }, new { dto.ToleranceEcartLitres, dto.ToleranceEcartPct });
        }
        c.Code = dto.Code.Trim().ToUpperInvariant(); c.ProduitId = dto.ProduitId; c.Capacite = dto.Capacite; c.SeuilMinimal = dto.SeuilMinimal;
        c.ToleranceEcartLitres = dto.ToleranceEcartLitres; c.ToleranceEcartPct = dto.ToleranceEcartPct; c.Actif = dto.Actif; c.Observations = dto.Observations;
        await db.SaveChangesAsync(ct);
        return (await CuvesAsync(ct)).First(x => x.Id == c.Id);
    }

    public async Task<IReadOnlyList<VolucompteurDto>> VolucompteursAsync(CancellationToken ct)
    {
        var vols = await db.Volucompteurs.AsNoTracking().Include(v => v.Pistolets).ThenInclude(p => p.Produit)
            .Include(v => v.Pistolets).ThenInclude(p => p.Cuve).OrderBy(v => v.Code).ToListAsync(ct);
        var derniers = await DerniersIndexAsync(ct);
        return vols.Select(v => new VolucompteurDto(v.Id, v.Code, v.Libelle, v.Actif,
            v.Pistolets.OrderBy(p => p.Code).Select(p => VersDto(p, v, derniers)).ToList())).ToList();
    }

    private static PistoletDto VersDto(Pistolet p, Volucompteur v, Dictionary<(Guid, int), decimal> derniers)
        => new(p.Id, v.Id, v.Code, p.Code, p.ProduitId, p.Produit!.Designation, p.CuveId, p.Cuve!.Code, p.IndexMaximum,
            p.IndexInitialCycle, p.Cycle, p.Actif, derniers.TryGetValue((p.Id, p.Cycle), out var d) ? d : null);

    /// <summary>Dernier index de fin par pistolet et par cycle (relevés non annulés).</summary>
    public async Task<Dictionary<(Guid, int), decimal>> DerniersIndexAsync(CancellationToken ct)
        => (await db.Releves.AsNoTracking().Where(r => r.Statut != StatutReleve.Annule)
                .GroupBy(r => new { r.PistoletId, r.Cycle }).Select(g => new { g.Key.PistoletId, g.Key.Cycle, Max = g.Max(r => r.IndexFin) })
                .ToListAsync(ct))
            .ToDictionary(x => (x.PistoletId, x.Cycle), x => x.Max);

    public async Task<VolucompteurDto> EnregistrerVolucompteurAsync(Guid? id, EnregistrementVolucompteurDto dto, CancellationToken ct)
    {
        Garde.Requis(dto.Code, "code");
        Volucompteur v;
        if (id is null) { v = new Volucompteur(); db.Volucompteurs.Add(v); }
        else v = Garde.Trouve(await db.Volucompteurs.FindAsync([id.Value], ct), "Volucompteur");
        v.Code = dto.Code.Trim().ToUpperInvariant(); v.Libelle = dto.Libelle; v.Actif = dto.Actif;
        await db.SaveChangesAsync(ct);
        return (await VolucompteursAsync(ct)).First(x => x.Id == v.Id);
    }

    public async Task<PistoletDto> EnregistrerPistoletAsync(Guid volucompteurId, Guid? id, EnregistrementPistoletDto dto, CancellationToken ct)
    {
        Garde.Requis(dto.Code, "code");
        Garde.Positif(dto.IndexMaximum, "indexMaximum");
        Garde.PositifOuNul(dto.IndexInitial, "indexInitial");
        if (dto.IndexInitial > dto.IndexMaximum) throw new ValidationException("L'index initial dépasse l'index maximum du totalisateur.");
        var v = Garde.Trouve(await db.Volucompteurs.FindAsync([volucompteurId], ct), "Volucompteur");
        var cuve = Garde.Trouve(await db.Cuves.FindAsync([dto.CuveId], ct), "Cuve");
        if (cuve.ProduitId != dto.ProduitId) throw new ValidationException("Le produit du pistolet doit être celui de la cuve qui l'alimente.");
        Pistolet p;
        if (id is null)
        {
            p = new Pistolet { VolucompteurId = v.Id, IndexInitialCycle = dto.IndexInitial };
            db.Pistolets.Add(p);
        }
        else
        {
            p = Garde.Trouve(await db.Pistolets.FirstOrDefaultAsync(x => x.Id == id && x.VolucompteurId == volucompteurId, ct), "Pistolet");
            var aDesReleves = await db.Releves.AnyAsync(r => r.PistoletId == p.Id, ct);
            if (aDesReleves && (p.ProduitId != dto.ProduitId || p.IndexInitialCycle != dto.IndexInitial))
                throw new RegleMetierException("Le produit et l'index initial d'un pistolet déjà relevé ne se modifient que par un événement de compteur validé.");
            if (!aDesReleves) p.IndexInitialCycle = dto.IndexInitial;
        }
        p.Code = dto.Code.Trim().ToUpperInvariant(); p.ProduitId = dto.ProduitId; p.CuveId = dto.CuveId; p.IndexMaximum = dto.IndexMaximum; p.Actif = dto.Actif;
        await db.SaveChangesAsync(ct);
        return (await VolucompteursAsync(ct)).SelectMany(x => x.Pistolets).First(x => x.Id == p.Id);
    }
}
