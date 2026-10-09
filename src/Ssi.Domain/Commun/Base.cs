namespace Ssi.Domain.Commun;

/// <summary>Base des entités métier : identifiant, traçabilité de création/modification, jeton de concurrence.</summary>
public abstract class Entite
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public DateTimeOffset CreeLe { get; set; }
    public Guid? CreePar { get; set; }
    public DateTimeOffset? ModifieLe { get; set; }
    public Guid? ModifiePar { get; set; }

    /// <summary>Jeton de concurrence optimiste (colonne système xmin de PostgreSQL).</summary>
    public uint Version { get; set; }
}

/// <summary>Document numéroté soumis à validation / annulation.</summary>
public abstract class Document : Entite
{
    public string Numero { get; set; } = "";
    public DateTimeOffset? ValideLe { get; set; }
    public Guid? ValidePar { get; set; }
    public DateTimeOffset? AnnuleLe { get; set; }
    public Guid? AnnulePar { get; set; }
    public string? MotifAnnulation { get; set; }
}

/// <summary>Violation d'une règle métier (HTTP 422).</summary>
public class RegleMetierException(string message, string? code = null) : Exception(message)
{
    public string? Code { get; } = code;
}

/// <summary>Conflit : opération déjà effectuée ou modifiée simultanément (HTTP 409).</summary>
public class ConflitException(string message) : Exception(message);

/// <summary>Objet introuvable (HTTP 404).</summary>
public class NonTrouveException(string message) : Exception(message);

/// <summary>Action interdite pour l'utilisateur (HTTP 403).</summary>
public class AccesRefuseException(string message) : Exception(message);

/// <summary>Données invalides (HTTP 400).</summary>
public class ValidationException(string message, IReadOnlyDictionary<string, string[]>? erreurs = null) : Exception(message)
{
    public IReadOnlyDictionary<string, string[]> Erreurs { get; } = erreurs ?? new Dictionary<string, string[]>();
}
