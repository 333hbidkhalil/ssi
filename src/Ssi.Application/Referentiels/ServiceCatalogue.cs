using Microsoft.EntityFrameworkCore;
using Ssi.Application.Commun;
using Ssi.Application.Securite;
using Ssi.Contracts;
using Ssi.Domain.Commun;
using Ssi.Domain.Entites;

namespace Ssi.Application.Referentiels;

/// <summary>Produits, familles et prix datés (§8, §11).</summary>
public class ServiceCatalogue(ISsiDb db, IHorloge horloge, Audit audit, IUtilisateurCourant courant)
{
    public async Task<IReadOnlyList<FamilleDto>> FamillesAsync(CancellationToken ct)
        => await db.FamillesProduits.AsNoTracking().OrderBy(f => f.Code)
            .Select(f => new FamilleDto(f.Id, f.Code, f.Libelle, f.Categorie)).ToListAsync(ct);

    public async Task<IReadOnlyList<ProduitDto>> ProduitsAsync(string? q, bool? actif, CategorieProduit? categorie, CancellationToken ct)
    {
        var maintenant = horloge.Maintenant;
        var requete = db.Produits.AsNoTracking().Include(p => p.Famille).Include(p => p.Taxe).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var terme = q.Trim().ToLower();
            requete = requete.Where(p => p.Code.ToLower().Contains(terme) || p.Designation.ToLower().Contains(terme) || (p.Marque != null && p.Marque.ToLower().Contains(terme)));
        }
        if (actif.HasValue) requete = requete.Where(p => p.Actif == actif);
        if (categorie.HasValue) requete = requete.Where(p => p.Famille!.Categorie == categorie.Value);
        var produits = await requete.OrderBy(p => p.Code).ToListAsync(ct);
        var ids = produits.Select(p => p.Id).ToList();
        var prix = await db.PrixProduits.AsNoTracking()
            .Where(x => ids.Contains(x.ProduitId) && x.Debut <= maintenant && (x.Fin == null || x.Fin > maintenant)).ToListAsync(ct);
        var voirAchat = courant.APermission(P.AchatsSensible) || courant.APermission(P.VracSensible);
        return produits.Select(p => new ProduitDto(p.Id, p.Code, p.Designation, p.FamilleId, p.Famille!.Libelle,
            p.Famille.Categorie, p.Unite, p.Marque, p.Conditionnement, p.TaxeId, p.Taxe!.Libelle,
            p.SeuilAlerte, p.TypeCarburant, p.Actif,
            prix.FirstOrDefault(x => x.ProduitId == p.Id && x.Type == TypePrix.Vente)?.Prix,
            voirAchat ? prix.FirstOrDefault(x => x.ProduitId == p.Id && x.Type == TypePrix.AchatReference)?.Prix : null)).ToList();
    }

    public async Task<ProduitDto> ProduitAsync(Guid id, CancellationToken ct)
        => (await ProduitsAsync(null, null, null, ct)).FirstOrDefault(p => p.Id == id) ?? throw new NonTrouveException("Produit introuvable.");

    public async Task<ProduitDto> EnregistrerAsync(Guid? id, EnregistrementProduitDto dto, CancellationToken ct)
    {
        Garde.Requis(dto.Code, "code");
        Garde.Requis(dto.Designation, "designation");
        Garde.Requis(dto.Unite, "unite");
        Garde.PositifOuNul(dto.SeuilAlerte, "seuilAlerte");
        var famille = Garde.Trouve(await db.FamillesProduits.FindAsync([dto.FamilleId], ct), "Famille");
        if (dto.TypeCarburant.HasValue && famille.Categorie != CategorieProduit.Carburant)
            throw new ValidationException("Seul un produit de la famille Carburant peut avoir un type de carburant.");
        if (famille.Categorie == CategorieProduit.Carburant && !dto.TypeCarburant.HasValue)
            throw new ValidationException("Un carburant doit préciser son type (Gasoil ou Sans plomb) pour l'analyse de rentabilité.");
        _ = Garde.Trouve(await db.Taxes.FindAsync([dto.TaxeId], ct), "Taxe");
        Produit p;
        object? avant = null;
        Guid? taxeAvant = null;
        if (id is null) { p = new Produit(); db.Produits.Add(p); }
        else
        {
            p = Garde.Trouve(await db.Produits.FindAsync([id.Value], ct), "Produit");
            avant = new { p.Code, p.Designation, p.TaxeId, p.Actif, p.SeuilAlerte };
            taxeAvant = p.TaxeId;
        }
        p.Code = dto.Code.Trim().ToUpperInvariant(); p.Designation = dto.Designation.Trim(); p.FamilleId = dto.FamilleId; p.Unite = dto.Unite;
        p.Marque = dto.Marque; p.Conditionnement = dto.Conditionnement; p.TaxeId = dto.TaxeId; p.SeuilAlerte = dto.SeuilAlerte;
        p.TypeCarburant = dto.TypeCarburant; p.Actif = dto.Actif;
        if (taxeAvant.HasValue && taxeAvant != dto.TaxeId)
            audit.Enregistrer("MODIFICATION_PARAMETRE", "Produit", p.Id, p.Code, "Changement de traitement fiscal", avant, dto);
        await db.SaveChangesAsync(ct);
        return await ProduitAsync(p.Id, ct);
    }

    public async Task<IReadOnlyList<PrixDto>> HistoriquePrixAsync(Guid produitId, CancellationToken ct)
    {
        var voirAchat = courant.APermission(P.AchatsSensible) || courant.APermission(P.VracSensible);
        var prix = await db.PrixProduits.AsNoTracking().Where(p => p.ProduitId == produitId && (voirAchat || p.Type == TypePrix.Vente))
            .OrderByDescending(p => p.Debut).ToListAsync(ct);
        var ids = prix.Where(p => p.ValidePar.HasValue).Select(p => p.ValidePar!.Value).Distinct().ToList();
        var noms = await db.Utilisateurs.Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Login, ct);
        return prix.Select(p => new PrixDto(p.Id, p.Type == TypePrix.Vente ? "Vente TTC" : "Achat de référence HT", p.Prix, p.Debut, p.Fin, p.Motif,
            p.ValidePar.HasValue ? noms.GetValueOrDefault(p.ValidePar.Value) : null)).ToList();
    }

    /// <summary>
    /// Nouveau prix daté : la période en cours est close à la date de début du nouveau prix.
    /// Les ventes déjà enregistrées conservent leur prix (R-08). Un prix ne peut pas être antidaté
    /// avant une vente déjà validée au prix précédent.
    /// </summary>
    public async Task<IReadOnlyList<PrixDto>> NouveauPrixAsync(Guid produitId, NouveauPrixDto dto, CancellationToken ct)
    {
        var motif = Garde.Motif(dto.Motif, "un changement de prix");
        Garde.Positif(dto.Prix, "prix");
        var produit = Garde.Trouve(await db.Produits.FindAsync([produitId], ct), "Produit");
        var type = dto.AchatReference ? TypePrix.AchatReference : TypePrix.Vente;
        await db.VerrouillerAsync("produits", produitId, ct);

        var futurs = await db.PrixProduits.Where(p => p.ProduitId == produitId && p.Type == type && p.Debut >= dto.Debut).AnyAsync(ct);
        if (futurs) throw new RegleMetierException("Un prix débute déjà à cette date ou après. Les prix existants ne sont pas réécrits.");
        if (type == TypePrix.Vente)
        {
            var releveApres = await db.Releves.AnyAsync(r => r.Pistolet!.ProduitId == produitId && r.Statut != StatutReleve.Annule && r.Horodatage > dto.Debut, ct);
            var venteApres = await db.LignesVenteArticle.AnyAsync(l => l.ProduitId == produitId &&
                db.VentesArticles.Any(v => v.Id == l.VenteId && v.Statut == StatutDocument.Valide && v.Horodatage > dto.Debut), ct);
            if (releveApres || venteApres)
                throw new RegleMetierException("Des ventes ont déjà été enregistrées après cette date au prix précédent : choisissez une date de début postérieure.");
        }
        var enCours = await db.PrixProduits.Where(p => p.ProduitId == produitId && p.Type == type && (p.Fin == null || p.Fin > dto.Debut)).ToListAsync(ct);
        foreach (var p in enCours) p.Fin = dto.Debut;
        await db.SaveChangesAsync(ct);
        var nouveau = new PrixProduit { ProduitId = produitId, Type = type, Prix = dto.Prix, Debut = dto.Debut, Motif = motif, ValidePar = courant.Id };
        db.PrixProduits.Add(nouveau);
        audit.Enregistrer("MODIFICATION_PRIX", "Produit", produitId, produit.Code, motif,
            enCours.Select(p => new { p.Prix, p.Debut }).ToList(), new { Type = type.ToString(), dto.Prix, dto.Debut });
        await db.SaveChangesAsync(ct);
        return await HistoriquePrixAsync(produitId, ct);
    }
}
