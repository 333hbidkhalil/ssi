using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Ssi.Domain.Commun;
using Ssi.Domain.Entites;

namespace Ssi.Application.Commun;

/// <summary>Journal d'audit des opérations sensibles (§7) : écrit dans la même transaction que l'opération.</summary>
public class Audit(ISsiDb db, IUtilisateurCourant utilisateur, IHorloge horloge)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string? Serialiser(object? valeur) => valeur is null ? null : JsonSerializer.Serialize(valeur, Json);

    public void Enregistrer(string action, string objetType, object? objetId, string? objetRef = null,
        string? motif = null, object? avant = null, object? apres = null)
    {
        db.JournalAudit.Add(new JournalAudit
        {
            Horodatage = horloge.Maintenant,
            UtilisateurId = utilisateur.Id,
            Login = utilisateur.Login,
            Action = action,
            ObjetType = objetType,
            ObjetId = objetId?.ToString(),
            ObjetRef = objetRef,
            Motif = motif,
            Avant = Serialiser(avant),
            Apres = Serialiser(apres),
            AdresseIp = utilisateur.AdresseIp
        });
    }
}

/// <summary>Paramètres applicatifs typés (tolérances, seuils) lus depuis la table parametres.</summary>
public class Parametres(ISsiDb db)
{
    public const string ToleranceVracPct = "vrac.tolerance_ecart_pct";
    public const string SeuilMargeVracPct = "vrac.seuil_marge_pct";
    public const string FacteurVolumeInhabituel = "index.facteur_volume_inhabituel";
    public const string VolumeMaxPoste = "index.volume_max_poste";
    public const string ToleranceReceptionPct = "achats.tolerance_ecart_pct";
    public const string TauxRecouvrement = "tresorerie.taux_recouvrement";
    public const string SeuilEcartCaisse = "caisse.seuil_alerte_ecart";
    public const string SeuilEcartPoste = "station.seuil_alerte_ecart";
    public const string JoursAlerteEcheance = "alertes.jours_echeance_fournisseur";
    public const string BaisseVentesPct = "alertes.baisse_ventes_pct";
    public const string HausseChargesPct = "alertes.hausse_charges_pct";
    public const string JoursRuptureStock = "alertes.jours_rupture";

    public static readonly IReadOnlyList<Parametre> ParDefaut =
    [
        new() { Cle = ToleranceVracPct, Valeur = "0.5", Type = "decimal", Description = "Tolérance d'écart chargé/livré vrac (%) avant justification obligatoire" },
        new() { Cle = SeuilMargeVracPct, Valeur = "2", Type = "decimal", Description = "Taux de marge prévisionnelle minimal d'une commande vrac (%)" },
        new() { Cle = FacteurVolumeInhabituel, Valeur = "3", Type = "decimal", Description = "Facteur au-delà duquel un volume de relevé est inhabituel (× moyenne des 10 derniers)" },
        new() { Cle = VolumeMaxPoste, Valeur = "", Type = "decimal", Description = "Volume maximal par pistolet et par poste (vide = pas de limite)" },
        new() { Cle = ToleranceReceptionPct, Valeur = "0.5", Type = "decimal", Description = "Tolérance d'écart chargé/reçu en réception cuve (%)" },
        new() { Cle = TauxRecouvrement, Valeur = "1", Type = "decimal", Description = "Taux de recouvrement supposé des créances à échoir dans les prévisions (1 = 100 %)" },
        new() { Cle = SeuilEcartCaisse, Valeur = "50", Type = "decimal", Description = "Écart de caisse (DH, valeur absolue) déclenchant une alerte" },
        new() { Cle = SeuilEcartPoste, Valeur = "50", Type = "decimal", Description = "Écart de poste (DH, valeur absolue) déclenchant une alerte" },
        new() { Cle = JoursAlerteEcheance, Valeur = "7", Type = "entier", Description = "Nombre de jours avant échéance fournisseur déclenchant une alerte" },
        new() { Cle = BaisseVentesPct, Valeur = "20", Type = "decimal", Description = "Baisse des volumes vs moyenne 7 jours déclenchant une alerte (%)" },
        new() { Cle = HausseChargesPct, Valeur = "25", Type = "decimal", Description = "Hausse des charges du mois vs mois précédent déclenchant une alerte (%)" },
        new() { Cle = JoursRuptureStock, Valeur = "3", Type = "entier", Description = "Jours de couverture en dessous desquels une rupture est jugée imminente" },
    ];

    public async Task<decimal> DecimalAsync(string cle, decimal defaut, CancellationToken ct = default)
    {
        var p = await db.Parametres.AsNoTracking().FirstOrDefaultAsync(x => x.Cle == cle, ct);
        return p is not null && decimal.TryParse(p.Valeur, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : defaut;
    }

    public async Task<decimal?> DecimalOptionnelAsync(string cle, CancellationToken ct = default)
    {
        var p = await db.Parametres.AsNoTracking().FirstOrDefaultAsync(x => x.Cle == cle, ct);
        return p is not null && decimal.TryParse(p.Valeur, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : null;
    }
}

/// <summary>Taux de taxes datés (aucun taux codé en dur).</summary>
public class Fiscalite(ISsiDb db)
{
    public async Task<decimal> TauxAsync(Guid taxeId, DateOnly date, CancellationToken ct = default)
    {
        var taux = await db.TauxTaxes.AsNoTracking()
            .Where(t => t.TaxeId == taxeId && t.DateDebut <= date && (t.DateFin == null || t.DateFin >= date))
            .Select(t => (decimal?)t.Taux).FirstOrDefaultAsync(ct);
        return taux ?? throw new RegleMetierException($"Aucun taux de taxe en vigueur au {date:dd/MM/yyyy} pour ce produit. Paramétrez la fiscalité datée.", "TAUX_ABSENT");
    }

    public async Task<decimal> TauxProduitAsync(Guid produitId, DateOnly date, CancellationToken ct = default)
    {
        var taxeId = await db.Produits.Where(p => p.Id == produitId).Select(p => p.TaxeId).FirstAsync(ct);
        return await TauxAsync(taxeId, date, ct);
    }
}

/// <summary>Prix datés (R-07) : le prix applicable est celui de la période contenant l'instant.</summary>
public class Tarifs(ISsiDb db)
{
    public Task<PrixProduit?> PrixApplicableAsync(Guid produitId, TypePrix type, DateTimeOffset instant, CancellationToken ct = default)
        => db.PrixProduits.AsNoTracking()
            .Where(p => p.ProduitId == produitId && p.Type == type && p.Debut <= instant && (p.Fin == null || p.Fin > instant))
            .FirstOrDefaultAsync(ct);

    public async Task<PrixProduit> PrixVenteObligatoireAsync(Guid produitId, DateTimeOffset instant, CancellationToken ct = default)
        => await PrixApplicableAsync(produitId, TypePrix.Vente, instant, ct)
           ?? throw new RegleMetierException("Aucun prix de vente en vigueur pour ce produit à cette date.", "PRIX_ABSENT");
}

public static class Garde
{
    public static string Motif(string? motif, string contexte = "cette opération")
    {
        if (string.IsNullOrWhiteSpace(motif) || motif.Trim().Length < 5)
            throw new ValidationException($"Un motif explicite (5 caractères minimum) est obligatoire pour {contexte}.",
                new Dictionary<string, string[]> { ["motif"] = ["Motif obligatoire."] });
        return motif.Trim();
    }

    public static void Positif(decimal valeur, string champ)
    {
        if (valeur <= 0) throw new ValidationException($"Le champ « {champ} » doit être strictement positif.",
            new Dictionary<string, string[]> { [champ] = ["Doit être strictement positif."] });
    }

    public static void PositifOuNul(decimal valeur, string champ)
    {
        if (valeur < 0) throw new ValidationException($"Le champ « {champ} » ne peut pas être négatif.",
            new Dictionary<string, string[]> { [champ] = ["Ne peut pas être négatif."] });
    }

    public static void Requis(string? valeur, string champ)
    {
        if (string.IsNullOrWhiteSpace(valeur)) throw new ValidationException($"Le champ « {champ} » est obligatoire.",
            new Dictionary<string, string[]> { [champ] = ["Obligatoire."] });
    }

    public static T Trouve<T>(T? entite, string libelle) where T : class
        => entite ?? throw new NonTrouveException($"{libelle} introuvable.");

    public static void Permission(IUtilisateurCourant u, string code)
    {
        if (!u.APermission(code)) throw new AccesRefuseException($"Vous n'avez pas l'autorisation requise ({code}).");
    }
}
