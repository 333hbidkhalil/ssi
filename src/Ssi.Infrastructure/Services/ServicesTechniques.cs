using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Ssi.Application.Commun;
using Ssi.Infrastructure.Persistance;

namespace Ssi.Infrastructure.Services;

public class HorlogeCasablanca : IHorloge
{
    private static readonly TimeZoneInfo Fuseau = TimeZoneInfo.FindSystemTimeZoneById("Africa/Casablanca");
    public virtual DateTimeOffset Maintenant => DateTimeOffset.UtcNow;
    public DateOnly Aujourdhui => JourneeDe(Maintenant);
    public DateOnly JourneeDe(DateTimeOffset instant) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, Fuseau).DateTime);

    public DateTimeOffset DebutJournee(DateOnly jour)
    {
        var local = jour.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, Fuseau.GetUtcOffset(local)).ToUniversalTime();
    }
}

public class Numerotation(SsiDbContext db, IHorloge horloge) : INumerotation
{
    private static readonly Dictionary<string, string> Prefixes = new()
    {
        ["POSTE"] = "PST", ["COMMANDE_FOURNISSEUR"] = "CF", ["RECEPTION"] = "REC", ["VENTE"] = "VTE",
        ["FACTURE_CLIENT"] = "FAC", ["AVOIR_CLIENT"] = "AVC", ["FACTURE_FOURNISSEUR"] = "FFR", ["AVOIR_FOURNISSEUR"] = "AVF",
        ["COMMANDE_CLIENT"] = "CC", ["LIVRAISON"] = "LIV", ["REGLEMENT_ENCAISSEMENT"] = "ENC", ["REGLEMENT_DECAISSEMENT"] = "DEC",
        ["TRANSFERT"] = "VIR", ["SESSION_CAISSE"] = "CAI", ["CHARGE"] = "CHG", ["AJUSTEMENT"] = "AJS",
    };

    public async Task<string> ProchainAsync(string typeDocument, CancellationToken ct = default)
    {
        var annee = horloge.Aujourdhui.Year;
        var prefixe = Prefixes.GetValueOrDefault(typeDocument, typeDocument[..Math.Min(3, typeDocument.Length)]);
        // Crée la séquence au besoin, puis l'incrémente atomiquement (verrou de ligne implicite de l'UPDATE).
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO sequences_documents (type_document, prefixe, annee, prochain_numero, longueur) VALUES ({typeDocument}, {prefixe}, {annee}, 1, 6) ON CONFLICT (type_document) DO NOTHING", ct);
        var resultat = await db.Database.SqlQuery<SequenceLue>($"""
            UPDATE sequences_documents
               SET prochain_numero = CASE WHEN annee = {annee} THEN prochain_numero + 1 ELSE 2 END,
                   annee = {annee}
             WHERE type_document = {typeDocument}
            RETURNING prefixe AS "Prefixe", annee AS "Annee", (prochain_numero - 1) AS "Numero", longueur AS "Longueur"
            """).ToListAsync(ct);
        var s = resultat.Single();
        return $"{s.Prefixe}-{s.Annee}-{s.Numero.ToString().PadLeft(s.Longueur, '0')}";
    }

    private sealed record SequenceLue(string Prefixe, int Annee, long Numero, int Longueur);
}

public class HachageMotDePasse : IHachageMotDePasse
{
    private static readonly PasswordHasher<object> Hasher = new();
    public string Hacher(string motDePasse) => Hasher.HashPassword(null!, motDePasse);
    public bool Verifier(string hash, string motDePasse) => Hasher.VerifyHashedPassword(null!, hash, motDePasse) != PasswordVerificationResult.Failed;
}
