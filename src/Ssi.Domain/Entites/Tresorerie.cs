using Ssi.Domain.Commun;

namespace Ssi.Domain.Entites;

public class CompteFinancier : Entite
{
    public string Code { get; set; } = "";
    public string Libelle { get; set; } = "";
    public TypeCompteFinancier Type { get; set; }
    public string? Banque { get; set; }
    public string? Rib { get; set; }
    public bool Actif { get; set; } = true;
}

/// <summary>Mouvement financier append-only ; le solde d'un compte est la somme de ses mouvements (R-26).</summary>
public class MouvementFinancier
{
    public long Id { get; set; }
    public Guid CompteId { get; set; }
    public DateOnly DateOperation { get; set; }
    public DateTimeOffset Horodatage { get; set; }
    public decimal Montant { get; set; }
    public NatureMouvementFinancier Nature { get; set; }
    public string Libelle { get; set; } = "";
    public Guid? ReglementId { get; set; }
    public Guid? TransfertId { get; set; }
    public Guid? ChargeId { get; set; }
    public Guid? SessionCaisseId { get; set; }
    public Guid? PosteId { get; set; }
    public Guid? VenteId { get; set; }
    public string? DocumentRef { get; set; }
    /// <summary>Mouvement contre-passé par celui-ci (annulation).</summary>
    public long? MouvementAnnuleId { get; set; }
    public Guid? CreePar { get; set; }
}

public class Reglement : Document
{
    public SensReglement Sens { get; set; }
    public Guid? ClientId { get; set; }
    public Guid? FournisseurId { get; set; }
    public string? Beneficiaire { get; set; }
    public ModeReglement Mode { get; set; }
    public Guid CompteId { get; set; }
    public DateOnly Date { get; set; }
    public decimal Montant { get; set; }
    public string? Reference { get; set; }
    public StatutReglement Statut { get; set; } = StatutReglement.Valide;
    public List<AffectationReglement> Affectations { get; set; } = [];
}

public class AffectationReglement
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid ReglementId { get; set; }
    public Guid? FactureId { get; set; }
    public Guid? ChargeId { get; set; }
    public decimal Montant { get; set; }
}

public class TransfertInterne : Document
{
    public Guid CompteSourceId { get; set; }
    public Guid CompteDestinationId { get; set; }
    public decimal Montant { get; set; }
    public DateOnly Date { get; set; }
    public string Libelle { get; set; } = "";
    /// <summary>Commission prélevée (remise TPE) enregistrée comme frais bancaires.</summary>
    public decimal Commission { get; set; }
}

public class SessionCaisse : Document
{
    public Guid CompteId { get; set; }
    public DateTimeOffset Ouverture { get; set; }
    public decimal SoldeOuverture { get; set; }
    public DateTimeOffset? Cloture { get; set; }
    public decimal? Encaissements { get; set; }
    public decimal? Decaissements { get; set; }
    public decimal? SoldeTheorique { get; set; }
    public decimal? MontantCompte { get; set; }
    public decimal? Ecart { get; set; }
    public StatutSessionCaisse Statut { get; set; } = StatutSessionCaisse.Ouverte;
    public string? Commentaire { get; set; }
    public Guid? CloturePar { get; set; }
    public int NombreReouvertures { get; set; }
}

public class RapprochementBancaire : Entite
{
    public Guid CompteId { get; set; }
    public DateOnly DateReleve { get; set; }
    public decimal SoldeReleve { get; set; }
    public bool Valide { get; set; }
    public List<LigneReleveBancaire> Lignes { get; set; } = [];
}

public class LigneReleveBancaire
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid RapprochementId { get; set; }
    public DateOnly Date { get; set; }
    public string Libelle { get; set; } = "";
    public decimal Montant { get; set; }
    public long? MouvementId { get; set; }
}
