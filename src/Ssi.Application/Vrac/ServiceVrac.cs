using Microsoft.EntityFrameworkCore;
using Ssi.Application.Achats;
using Ssi.Application.Commun;
using Ssi.Application.Finance;
using Ssi.Application.Securite;
using Ssi.Domain.Calculs;
using Ssi.Domain.Entites;

namespace Ssi.Application.Vrac;

/// <summary>
/// Livraisons directes fournisseur → client (§12-§16). Aucune écriture de stock n'est jamais générée :
/// le carburant ne transite pas par les cuves de la station.
/// </summary>
public class ServiceVrac(ISsiDb db, IHorloge horloge, Audit audit, IUtilisateurCourant courant, INumerotation numerotation,
    Parametres parametres, Soldes soldes, ServiceAchats achats, ServiceFactures factures)
{
    private bool Sensible => courant.APermission(P.VracSensible);

    public async Task<IReadOnlyList<CommandeClientDto>> CommandesAsync(Guid? clientId, StatutCommande? statut, CancellationToken ct)
    {
        var ids = await db.CommandesClient.AsNoTracking().Where(c => (clientId == null || c.ClientId == clientId) && (statut == null || c.Statut == statut))
            .OrderByDescending(c => c.CreeLe).Take(300).Select(c => c.Id).ToListAsync(ct);
        var res = new List<CommandeClientDto>();
        foreach (var id in ids) res.Add(await CommandeAsync(id, ct));
        return res;
    }

    public async Task<CommandeClientDto> CommandeAsync(Guid id, CancellationToken ct)
    {
        var c = Garde.Trouve(await db.CommandesClient.AsNoTracking().Include(x => x.Client).Include(x => x.Produit).FirstOrDefaultAsync(x => x.Id == id, ct), "Commande client");
        var cfs = await achats.CommandesAsync(null, null, id, ct);
        var livraisons = await LivraisonsAsync(id, ct);
        var volumeLivre = livraisons.Where(l => l.Statut == StatutLivraison.Validee).Sum(l => l.QuantiteLivree);
        return new CommandeClientDto(c.Id, c.Numero, c.ClientId, c.Client!.Nom, c.ProduitId, c.Produit!.Designation, c.Volume, c.PrixVenteHt,
            c.DatePrevue, c.LieuLivraison, c.ConditionsReglement, c.Transporteur, c.FournisseurPrevuId, c.Statut, volumeLivre,
            Sensible ? await MargeAsync(c, ct) : null, cfs, livraisons, c.DerogationMotif);
    }

    private async Task<List<LivraisonDto>> LivraisonsAsync(Guid commandeClientId, CancellationToken ct)
    {
        var livs = await db.LivraisonsDirectes.AsNoTracking().Where(l => l.CommandeClientId == commandeClientId).OrderBy(l => l.Horodatage).ToListAsync(ct);
        var fournisseurs = await db.Fournisseurs.AsNoTracking().ToDictionaryAsync(f => f.Id, f => f.Nom, ct);
        var ids = livs.Select(l => l.Id).ToList();
        var facturees = await db.LignesFacture.AsNoTracking().Where(l => l.LivraisonId != null && ids.Contains(l.LivraisonId.Value)
                && db.Factures.Any(f => f.Id == l.FactureId && f.Sens == SensFacture.Client && f.Type == TypeFacture.Facture && f.Statut != StatutDocument.Annule))
            .Select(l => l.LivraisonId!.Value).Distinct().ToListAsync(ct);
        var frais = await db.RepartitionsFrais.AsNoTracking().Where(r => ids.Contains(r.LivraisonId))
            .GroupBy(r => r.LivraisonId).Select(g => new { g.Key, Montant = g.Sum(r => r.MontantHt) }).ToDictionaryAsync(x => x.Key, x => x.Montant, ct);
        var s = Sensible;
        return livs.Select(l =>
        {
            var (ecart, pct, _) = CalculsVrac.EcartQuantite(l.QuantiteChargee, l.QuantiteLivree, 0);
            return new LivraisonDto(l.Id, l.Numero, l.CommandeClientId, l.CommandeFournisseurId, l.FournisseurId, fournisseurs.GetValueOrDefault(l.FournisseurId, "?"),
                l.ClientId, l.Horodatage, l.QuantiteCommandee, l.QuantiteChargee, l.QuantiteLivree, ecart, pct, l.NumeroBlFournisseur, l.NumeroReceptionClient,
                l.Vehicule, l.Transporteur, l.Chauffeur, s ? l.PrixAchatHt : null, l.PrixVenteHt, l.JustificationEcart, l.Reserves, l.Statut,
                facturees.Contains(l.Id), s ? frais.GetValueOrDefault(l.Id) : null);
        }).ToList();
    }

    public async Task<CommandeClientDto> CreerCommandeAsync(CreationCommandeClientDto dto, CancellationToken ct)
    {
        var client = Garde.Trouve(await db.Clients.FindAsync([dto.ClientId], ct), "Client");
        if (!client.Actif) throw new RegleMetierException("Client inactif.");
        var produit = Garde.Trouve(await db.Produits.Include(p => p.Famille).FirstOrDefaultAsync(p => p.Id == dto.ProduitId, ct), "Produit");
        if (produit.Famille!.Categorie != CategorieProduit.Carburant) throw new ValidationException("La livraison en vrac concerne le gasoil ou l'essence sans plomb.");
        Garde.Positif(dto.Volume, "volume");
        Garde.Positif(dto.PrixVenteHt, "prixVenteHt");
        var c = new CommandeClient
        {
            Numero = await numerotation.ProchainAsync("COMMANDE_CLIENT", ct), ClientId = dto.ClientId, ProduitId = dto.ProduitId, Volume = dto.Volume,
            PrixVenteHt = dto.PrixVenteHt, DatePrevue = dto.DatePrevue, LieuLivraison = dto.LieuLivraison, ConditionsReglement = dto.ConditionsReglement,
            Transporteur = dto.Transporteur, FournisseurPrevuId = dto.FournisseurPrevuId
        };
        db.CommandesClient.Add(c);
        await db.SaveChangesAsync(ct);
        return await CommandeAsync(c.Id, ct);
    }

    /// <summary>Commande fournisseur liée (une commande client peut être servie par plusieurs fournisseurs).</summary>
    public async Task<CommandeClientDto> AjouterCommandeFournisseurAsync(Guid commandeClientId, CommandeFournisseurVracDto dto, CancellationToken ct)
    {
        await db.VerrouillerAsync("commandes_client", commandeClientId, ct);
        var cc = Garde.Trouve(await db.CommandesClient.FirstOrDefaultAsync(c => c.Id == commandeClientId, ct), "Commande client");
        if (cc.Statut is StatutCommande.Annulee or StatutCommande.Terminee) throw new RegleMetierException("Commande client clôturée ou annulée.");
        var f = Garde.Trouve(await db.Fournisseurs.FindAsync([dto.FournisseurId], ct), "Fournisseur");
        if (!f.Actif) throw new RegleMetierException("Fournisseur inactif.");
        Garde.Positif(dto.Volume, "volume");
        Garde.Positif(dto.PrixAchatHt, "prixAchatHt");
        Garde.PositifOuNul(dto.FraisPrevusHt, "fraisPrevusHt");
        var cf = new CommandeFournisseur
        {
            Numero = await numerotation.ProchainAsync("COMMANDE_FOURNISSEUR", ct), FournisseurId = f.Id, Date = horloge.Aujourdhui, DatePrevue = dto.DatePrevue ?? cc.DatePrevue,
            Destination = DestinationAchat.LivraisonDirecte, CommandeClientId = cc.Id, ReferenceFournisseur = dto.ReferenceFournisseur,
            Conditions = dto.Conditions, FraisPrevusHt = dto.FraisPrevusHt,
            Statut = cc.Statut == StatutCommande.Brouillon ? StatutCommande.Brouillon : StatutCommande.Confirmee
        };
        cf.Lignes.Add(new LigneCommandeFournisseur { CommandeId = cf.Id, ProduitId = cc.ProduitId, Quantite = dto.Volume, PrixUnitaireHt = dto.PrixAchatHt });
        db.CommandesFournisseur.Add(cf);
        await db.SaveChangesAsync(ct);
        return await CommandeAsync(cc.Id, ct);
    }

    private sealed record Previsionnel(MargePrevue Marge, decimal PrixAchatMoyen, decimal Frais);

    private async Task<Previsionnel?> PrevisionnelAsync(CommandeClient cc, CancellationToken ct)
    {
        var cfs = await db.CommandesFournisseur.AsNoTracking().Include(c => c.Lignes)
            .Where(c => c.CommandeClientId == cc.Id && c.Statut != StatutCommande.Annulee).ToListAsync(ct);
        var lignes = cfs.SelectMany(c => c.Lignes).ToList();
        if (lignes.Count == 0) return null;
        var volumeAchat = lignes.Sum(l => l.Quantite);
        var prixMoyen = Math.Round(lignes.Sum(l => l.Quantite * l.PrixUnitaireHt) / volumeAchat, 4, MidpointRounding.AwayFromZero);
        var frais = cfs.Sum(c => c.FraisPrevusHt);
        return new Previsionnel(CalculsVrac.Prevue(cc.Volume, cc.PrixVenteHt, prixMoyen, frais), prixMoyen, frais);
    }

    /// <summary>R-14 : confirmation après comparaison prix d'achat / prix de vente ; dérogation habilitée et motivée sinon.</summary>
    public async Task<CommandeClientDto> ConfirmerAsync(Guid id, ConfirmationCommandeClientDto dto, CancellationToken ct)
    {
        await db.VerrouillerAsync("commandes_client", id, ct);
        var cc = Garde.Trouve(await db.CommandesClient.FirstOrDefaultAsync(c => c.Id == id, ct), "Commande client");
        await db.Entry(cc).ReloadAsync(ct);
        if (cc.Statut != StatutCommande.Brouillon) throw new ConflitException("Seule une commande en brouillon peut être confirmée.");
        var prev = await PrevisionnelAsync(cc, ct) ?? throw new RegleMetierException("Créez au moins une commande fournisseur liée avant de confirmer (comparaison des prix obligatoire).");
        var seuil = await parametres.DecimalAsync(Parametres.SeuilMargeVracPct, 2m, ct);
        if (!CalculsVrac.MargeAcceptable(prev.Marge, seuil))
        {
            var explication = prev.Marge.Marge < 0
                ? $"Marge prévisionnelle négative ({prev.Marge.Marge:N2} DH)."
                : $"Taux de marge prévisionnel {prev.Marge.TauxMarque.Valeur:N2} % inférieur au seuil de {seuil:N2} %.";
            if (!courant.APermission(P.VracDerogation))
                throw new RegleMetierException($"{explication} Une dérogation par un utilisateur habilité est nécessaire.", "MARGE_INSUFFISANTE");
            if (string.IsNullOrWhiteSpace(dto.MotifDerogation))
                throw new RegleMetierException($"{explication} Indiquez le motif de la dérogation.", "MARGE_INSUFFISANTE");
            cc.DerogationPar = courant.Id;
            cc.DerogationMotif = Garde.Motif(dto.MotifDerogation, "une dérogation de marge");
            audit.Enregistrer("DEROGATION_MARGE_VRAC", "CommandeClient", cc.Id, cc.Numero, cc.DerogationMotif, null, new { prev.Marge.Marge, prev.Marge.TauxMarque.Valeur, seuil });
        }
        cc.MargePrevueHt = prev.Marge.Marge;
        cc.PrixAchatPrevuHt = prev.PrixAchatMoyen;
        cc.FraisPrevusHt = prev.Frais;
        cc.Statut = StatutCommande.Confirmee;
        cc.ValideLe = horloge.Maintenant;
        cc.ValidePar = courant.Id;
        foreach (var cf in await db.CommandesFournisseur.Where(c => c.CommandeClientId == id && c.Statut == StatutCommande.Brouillon).ToListAsync(ct))
        {
            cf.Statut = StatutCommande.Confirmee;
            cf.Confirmee = true;
        }
        await db.SaveChangesAsync(ct);
        return await CommandeAsync(id, ct);
    }

    public async Task<CommandeClientDto> AnnulerCommandeAsync(Guid id, string? motif, CancellationToken ct)
    {
        var m = Garde.Motif(motif, "l'annulation d'une commande vrac");
        await db.VerrouillerAsync("commandes_client", id, ct);
        var cc = Garde.Trouve(await db.CommandesClient.FirstOrDefaultAsync(c => c.Id == id, ct), "Commande client");
        if (await db.LivraisonsDirectes.AnyAsync(l => l.CommandeClientId == id && l.Statut == StatutLivraison.Validee, ct))
            throw new RegleMetierException("Des livraisons validées existent : la commande ne peut plus être annulée.");
        cc.Statut = StatutCommande.Annulee;
        cc.AnnuleLe = horloge.Maintenant; cc.AnnulePar = courant.Id; cc.MotifAnnulation = m;
        foreach (var cf in await db.CommandesFournisseur.Where(c => c.CommandeClientId == id).ToListAsync(ct)) cf.Statut = StatutCommande.Annulee;
        audit.Enregistrer("ANNULATION_COMMANDE", "CommandeClient", cc.Id, cc.Numero, m);
        await db.SaveChangesAsync(ct);
        return await CommandeAsync(id, ct);
    }

    // ---------- Livraisons ----------

    public async Task<CommandeClientDto> EnregistrerLivraisonAsync(Guid commandeClientId, SaisieLivraisonDto dto, CancellationToken ct)
    {
        await db.VerrouillerAsync("commandes_client", commandeClientId, ct);
        var cc = Garde.Trouve(await db.CommandesClient.FirstOrDefaultAsync(c => c.Id == commandeClientId, ct), "Commande client");
        if (cc.Statut is not (StatutCommande.Confirmee or StatutCommande.EnCours)) throw new RegleMetierException("La commande client doit être confirmée.");
        var cf = Garde.Trouve(await db.CommandesFournisseur.Include(c => c.Lignes).FirstOrDefaultAsync(c => c.Id == dto.CommandeFournisseurId && c.CommandeClientId == cc.Id, ct),
            "Commande fournisseur liée");
        if (cf.Statut == StatutCommande.Annulee) throw new RegleMetierException("Commande fournisseur annulée.");
        Garde.PositifOuNul(dto.QuantiteChargee, "quantiteChargee");
        Garde.Positif(dto.QuantiteLivree, "quantiteLivree");
        var l = new LivraisonDirecte
        {
            Numero = await numerotation.ProchainAsync("LIVRAISON", ct), CommandeClientId = cc.Id, CommandeFournisseurId = cf.Id, FournisseurId = cf.FournisseurId,
            ClientId = cc.ClientId, ProduitId = cc.ProduitId, Horodatage = dto.Horodatage ?? horloge.Maintenant, QuantiteCommandee = cf.Lignes.Sum(x => x.Quantite),
            QuantiteChargee = dto.QuantiteChargee, QuantiteLivree = dto.QuantiteLivree, NumeroBlFournisseur = dto.NumeroBlFournisseur,
            NumeroReceptionClient = dto.NumeroReceptionClient, Vehicule = dto.Vehicule, Transporteur = dto.Transporteur ?? cc.Transporteur, Chauffeur = dto.Chauffeur,
            PrixAchatHt = cf.Lignes.First().PrixUnitaireHt, PrixVenteHt = cc.PrixVenteHt, JustificationEcart = dto.JustificationEcart, Reserves = dto.Reserves
        };
        db.LivraisonsDirectes.Add(l);
        await db.SaveChangesAsync(ct);
        return await CommandeAsync(cc.Id, ct);
    }

    /// <summary>Validation sur preuve de livraison (BL fournisseur et confirmation client). Aucun mouvement de stock.</summary>
    public async Task<CommandeClientDto> ValiderLivraisonAsync(Guid livraisonId, CancellationToken ct)
    {
        await db.VerrouillerAsync("livraisons_directes", livraisonId, ct);
        var l = Garde.Trouve(await db.LivraisonsDirectes.FirstOrDefaultAsync(x => x.Id == livraisonId, ct), "Livraison");
        await db.Entry(l).ReloadAsync(ct);
        if (l.Statut == StatutLivraison.Validee) throw new ConflitException($"La livraison {l.Numero} est déjà validée.");
        if (l.Statut == StatutLivraison.Annulee) throw new ConflitException("Livraison annulée.");
        if (string.IsNullOrWhiteSpace(l.NumeroBlFournisseur) || string.IsNullOrWhiteSpace(l.NumeroReceptionClient))
            throw new RegleMetierException("La preuve de livraison est incomplète : numéro du BL fournisseur et numéro de réception client obligatoires.");
        var tol = await parametres.DecimalAsync(Parametres.ToleranceVracPct, 0.5m, ct);
        var (ecart, pct, justif) = CalculsVrac.EcartQuantite(l.QuantiteChargee, l.QuantiteLivree, tol);
        if (justif && string.IsNullOrWhiteSpace(l.JustificationEcart))
            throw new RegleMetierException($"Écart chargé/livré de {ecart:N3} L ({pct:N3} %) supérieur à la tolérance de {tol} % : une justification est obligatoire. L'écart n'est pas imputé automatiquement à la station.");
        l.Statut = StatutLivraison.Validee;
        l.ValideLe = horloge.Maintenant;
        l.ValidePar = courant.Id;
        await db.SaveChangesAsync(ct);
        var cc = await db.CommandesClient.FirstAsync(c => c.Id == l.CommandeClientId, ct);
        var livre = await db.LivraisonsDirectes.Where(x => x.CommandeClientId == cc.Id && x.Statut == StatutLivraison.Validee).SumAsync(x => x.QuantiteLivree, ct);
        cc.Statut = livre >= cc.Volume ? StatutCommande.Terminee : StatutCommande.EnCours;
        var cf = await db.CommandesFournisseur.FirstAsync(c => c.Id == l.CommandeFournisseurId, ct);
        cf.Statut = StatutCommande.EnCours;
        audit.Enregistrer("VALIDATION_LIVRAISON_DIRECTE", "LivraisonDirecte", l.Id, l.Numero, l.JustificationEcart, null,
            new { l.QuantiteChargee, l.QuantiteLivree, l.NumeroBlFournisseur, l.NumeroReceptionClient });
        await db.SaveChangesAsync(ct);
        return await CommandeAsync(cc.Id, ct);
    }

    public async Task<CommandeClientDto> AnnulerLivraisonAsync(Guid livraisonId, string? motif, CancellationToken ct)
    {
        var m = Garde.Motif(motif, "l'annulation d'une livraison");
        await db.VerrouillerAsync("livraisons_directes", livraisonId, ct);
        var l = Garde.Trouve(await db.LivraisonsDirectes.FirstOrDefaultAsync(x => x.Id == livraisonId, ct), "Livraison");
        if (await db.LignesFacture.AnyAsync(x => x.LivraisonId == l.Id && db.Factures.Any(f => f.Id == x.FactureId && f.Statut != StatutDocument.Annule), ct))
            throw new RegleMetierException("Cette livraison est facturée : annulez d'abord la facture ou établissez un avoir.");
        if (await db.RepartitionsFrais.AnyAsync(r => r.LivraisonId == l.Id, ct))
            throw new RegleMetierException("Des frais sont répartis sur cette livraison.");
        var avant = l.Statut.ToString();
        l.Statut = StatutLivraison.Annulee;
        l.AnnuleLe = horloge.Maintenant; l.AnnulePar = courant.Id; l.MotifAnnulation = m;
        audit.Enregistrer("ANNULATION_LIVRAISON", "LivraisonDirecte", l.Id, l.Numero, m, new { Statut = avant }, new { Statut = "Annulee" });
        await db.SaveChangesAsync(ct);
        return await CommandeAsync(l.CommandeClientId, ct);
    }

    /// <summary>Facture client des livraisons validées non encore facturées (H-08 : sur la quantité livrée).</summary>
    public async Task<FactureDto> FacturerClientAsync(Guid commandeClientId, DateOnly date, bool valider, CancellationToken ct)
    {
        var cc = Garde.Trouve(await db.CommandesClient.FirstOrDefaultAsync(c => c.Id == commandeClientId, ct), "Commande client");
        var livraisons = (await LivraisonsAsync(cc.Id, ct)).Where(l => l.Statut == StatutLivraison.Validee && !l.Facturee).ToList();
        if (livraisons.Count == 0) throw new RegleMetierException("Aucune livraison validée à facturer.");
        var f = await factures.CreerAsync(new CreationFactureDto(SensFacture.Client, TypeFacture.Facture, OrigineFacture.Vrac, cc.ClientId, null, null, date, null,
            null, cc.Id, null, $"Livraison directe — commande {cc.Numero}",
            livraisons.Select(l => new LigneFactureSaisieDto(cc.ProduitId, $"Livraison {l.Numero} — BL {l.NumeroBlFournisseur}", l.QuantiteLivree, cc.PrixVenteHt, null, l.Id, null)).ToList()), ct);
        return valider ? await factures.ValiderAsync(f.Id, ct) : f;
    }

    // ---------- Frais directs ----------

    public async Task<FraisDirectDto> SaisirFraisAsync(SaisieFraisDirectDto dto, CancellationToken ct)
    {
        Garde.Requis(dto.Libelle, "libelle");
        Garde.Positif(dto.MontantHt, "montantHt");
        if (dto.LivraisonIds.Count == 0) throw new ValidationException("Indiquez la ou les livraisons concernées.");
        var livs = await db.LivraisonsDirectes.Where(l => dto.LivraisonIds.Contains(l.Id)).ToListAsync(ct);
        if (livs.Count != dto.LivraisonIds.Distinct().Count()) throw new NonTrouveException("Livraison introuvable.");
        if (livs.Any(l => l.Statut != StatutLivraison.Validee)) throw new RegleMetierException("Les frais se répartissent sur des livraisons validées.");
        if (dto.FactureId.HasValue)
        {
            var fac = Garde.Trouve(await db.Factures.FindAsync([dto.FactureId.Value], ct), "Facture");
            if (fac.Sens != SensFacture.Fournisseur || fac.Statut != StatutDocument.Valide) throw new ValidationException("Les frais se rattachent à une facture fournisseur validée.");
            var deja = await db.FraisDirects.Where(x => x.FactureId == fac.Id).SumAsync(x => x.MontantHt, ct);
            if (deja + dto.MontantHt > fac.TotalHt) throw new RegleMetierException("Le total des frais dépasserait le montant HT de la facture (un coût ne se déduit qu'une fois).");
        }
        var ordonnees = dto.LivraisonIds.Select(id => livs.First(l => l.Id == id)).ToList();
        IReadOnlyList<decimal> parts = dto.Methode switch
        {
            MethodeRepartitionFrais.Volume => CalculsVrac.RepartirAuProrata(dto.MontantHt, ordonnees.Select(l => l.QuantiteLivree).ToList()),
            _ => dto.MontantsManuels is { } mm && mm.Count == ordonnees.Count && mm.Sum() == dto.MontantHt && mm.All(x => x >= 0)
                ? mm : throw new ValidationException("Répartition manuelle : un montant par livraison, dont la somme égale le montant du frais.")
        };
        var frais = new FraisDirect
        {
            Type = dto.Type, Libelle = dto.Libelle, FournisseurId = dto.FournisseurId, FactureId = dto.FactureId, Date = dto.Date, MontantHt = dto.MontantHt, Methode = dto.Methode
        };
        for (var i = 0; i < ordonnees.Count; i++)
            frais.Repartitions.Add(new RepartitionFrais { FraisId = frais.Id, LivraisonId = ordonnees[i].Id, MontantHt = parts[i] });
        db.FraisDirects.Add(frais);
        await db.SaveChangesAsync(ct);
        return new FraisDirectDto(frais.Id, frais.Type, frais.Libelle, frais.Date, frais.MontantHt, frais.Methode,
            frais.Repartitions.Select(r => new RepartitionFraisDto(r.LivraisonId, ordonnees.First(l => l.Id == r.LivraisonId).Numero, r.MontantHt)).ToList(), frais.FactureId);
    }

    // ---------- Marge (R-14 à R-17) ----------

    private async Task<MargeVracDto> MargeAsync(CommandeClient cc, CancellationToken ct)
    {
        var livs = await db.LivraisonsDirectes.AsNoTracking().Where(l => l.CommandeClientId == cc.Id && l.Statut == StatutLivraison.Validee).ToListAsync(ct);
        var ids = livs.Select(l => l.Id).ToList();
        var lignes = await db.LignesFacture.AsNoTracking().Where(l => l.LivraisonId != null && ids.Contains(l.LivraisonId.Value))
            .Join(db.Factures.AsNoTracking().Where(f => f.Statut == StatutDocument.Valide), l => l.FactureId, f => f.Id, (l, f) => new { l.LivraisonId, l.MontantHt, f.Id, f.Sens, f.Type, f.Origine })
            .ToListAsync(ct);
        var facturesClient = lignes.Where(x => x.Sens == SensFacture.Client && x.Type == TypeFacture.Facture).ToList();
        var facturesFournisseur = lignes.Where(x => x.Sens == SensFacture.Fournisseur && x.Type == TypeFacture.Facture && x.Origine == OrigineFacture.AchatVrac).ToList();
        var idsFC = facturesClient.Select(x => x.Id).Distinct().ToList();
        var idsFF = facturesFournisseur.Select(x => x.Id).Distinct().ToList();
        var avoirsClient = await db.Factures.AsNoTracking().Where(a => a.Type == TypeFacture.Avoir && a.Statut == StatutDocument.Valide && a.FactureOrigineId != null && idsFC.Contains(a.FactureOrigineId.Value)).SumAsync(a => a.TotalHt, ct);
        var avoirsFournisseur = await db.Factures.AsNoTracking().Where(a => a.Type == TypeFacture.Avoir && a.Statut == StatutDocument.Valide && a.FactureOrigineId != null && idsFF.Contains(a.FactureOrigineId.Value)).SumAsync(a => a.TotalHt, ct);
        var frais = await db.RepartitionsFrais.AsNoTracking().Where(r => ids.Contains(r.LivraisonId))
            .Join(db.FraisDirects.AsNoTracking(), r => r.FraisId, f => f.Id, (r, f) => new { r.MontantHt, f.Type, f.FactureId }).ToListAsync(ct);
        var transport = frais.Where(f => f.Type == TypeFrais.Transport).Sum(f => f.MontantHt);
        var autres = frais.Where(f => f.Type != TypeFrais.Transport).Sum(f => f.MontantHt);

        var clientPresente = livs.Count > 0 && livs.All(l => facturesClient.Any(x => x.LivraisonId == l.Id));
        var fournisseurPresente = livs.Count > 0 && livs.All(l => facturesFournisseur.Any(x => x.LivraisonId == l.Id));
        var fraisManquants = cc.FraisPrevusHt > 0 && frais.Count == 0;
        var reelle = CalculsVrac.Reelle(facturesClient.Sum(x => x.MontantHt), avoirsClient, clientPresente,
            facturesFournisseur.Sum(x => x.MontantHt) - avoirsFournisseur, fournisseurPresente, transport + autres, fraisManquants, cc.MargePrevueHt);
        var manquants = reelle.DonneesManquantes.ToList();
        if (livs.Count == 0) manquants.Insert(0, "Aucune livraison validée");

        var situationsC = await soldes.FacturesAsync(idsFC, ct);
        var idsFrais = frais.Where(f => f.FactureId.HasValue).Select(f => f.FactureId!.Value).Distinct().ToList();
        var situationsF = await soldes.FacturesAsync(idsFF.Union(idsFrais).ToList(), ct);
        var prev = cc.MargePrevueHt.HasValue ? null : await PrevisionnelAsync(cc, ct);
        var margePrevue = cc.MargePrevueHt ?? prev?.Marge.Marge;
        var caPrevu = Arrondi.Montant(cc.Volume * cc.PrixVenteHt);
        return new MargeVracDto(margePrevue, margePrevue.HasValue && caPrevu > 0 ? Arrondi.Pourcentage(margePrevue.Value / caPrevu * 100) : null,
            reelle.ChiffreAffairesNetHt, reelle.CoutAchatHt, transport, autres, reelle.CoutDirectHt, reelle.Marge,
            livs.Count > 0 && cc.MargePrevueHt.HasValue ? reelle.EcartVsPrevue : null, reelle.Provisoire || livs.Count == 0, manquants,
            livs.Sum(l => l.QuantiteLivree - l.QuantiteChargee), situationsC.Values.Sum(s => s.ResteDu), situationsF.Values.Sum(s => s.ResteDu),
            "CA net = factures client HT − avoirs ; coût direct = factures fournisseur HT − avoirs + frais directs répartis (prorata des volumes livrés ou manuel). Montants hors taxes.");
    }
}
