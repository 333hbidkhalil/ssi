using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Ssi.Domain.Entites;

namespace Ssi.Application.Commun;

/// <summary>Accès aux données utilisé par les cas d'usage (implémenté par EF Core / PostgreSQL).</summary>
public interface ISsiDb
{
    DbSet<Utilisateur> Utilisateurs { get; }
    DbSet<Role> Roles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<UtilisateurRole> UtilisateurRoles { get; }
    DbSet<UtilisateurPermission> UtilisateurPermissions { get; }
    DbSet<JournalAudit> JournalAudit { get; }
    DbSet<CleIdempotence> ClesIdempotence { get; }

    DbSet<Societe> Societes { get; }
    DbSet<Parametre> Parametres { get; }
    DbSet<Taxe> Taxes { get; }
    DbSet<TauxTaxe> TauxTaxes { get; }
    DbSet<SequenceDocument> SequencesDocuments { get; }
    DbSet<Objectif> Objectifs { get; }
    DbSet<Simulation> Simulations { get; }
    DbSet<Sauvegarde> Sauvegardes { get; }

    DbSet<FamilleProduit> FamillesProduits { get; }
    DbSet<Produit> Produits { get; }
    DbSet<PrixProduit> PrixProduits { get; }

    DbSet<Cuve> Cuves { get; }
    DbSet<Volucompteur> Volucompteurs { get; }
    DbSet<Pistolet> Pistolets { get; }
    DbSet<PosteTravail> Postes { get; }
    DbSet<ReleveIndex> Releves { get; }
    DbSet<EvenementCompteur> EvenementsCompteur { get; }
    DbSet<ReglementPoste> ReglementsPoste { get; }

    DbSet<MouvementStock> MouvementsStock { get; }
    DbSet<PositionStock> PositionsStock { get; }
    DbSet<Jaugeage> Jaugeages { get; }
    DbSet<AjustementStock> AjustementsStock { get; }

    DbSet<Client> Clients { get; }
    DbSet<Fournisseur> Fournisseurs { get; }
    DbSet<IncidentFournisseur> IncidentsFournisseur { get; }
    DbSet<Relance> Relances { get; }
    DbSet<PieceJointe> PiecesJointes { get; }

    DbSet<CommandeFournisseur> CommandesFournisseur { get; }
    DbSet<LigneCommandeFournisseur> LignesCommandeFournisseur { get; }
    DbSet<Reception> Receptions { get; }
    DbSet<LigneReception> LignesReception { get; }
    DbSet<VenteArticle> VentesArticles { get; }
    DbSet<LigneVenteArticle> LignesVenteArticle { get; }
    DbSet<Facture> Factures { get; }
    DbSet<LigneFacture> LignesFacture { get; }

    DbSet<CommandeClient> CommandesClient { get; }
    DbSet<LivraisonDirecte> LivraisonsDirectes { get; }
    DbSet<FraisDirect> FraisDirects { get; }
    DbSet<RepartitionFrais> RepartitionsFrais { get; }

    DbSet<CompteFinancier> ComptesFinanciers { get; }
    DbSet<MouvementFinancier> MouvementsFinanciers { get; }
    DbSet<Reglement> Reglements { get; }
    DbSet<AffectationReglement> AffectationsReglement { get; }
    DbSet<TransfertInterne> TransfertsInternes { get; }
    DbSet<SessionCaisse> SessionsCaisse { get; }
    DbSet<RapprochementBancaire> RapprochementsBancaires { get; }
    DbSet<LigneReleveBancaire> LignesReleveBancaire { get; }

    DbSet<CategorieCharge> CategoriesCharges { get; }
    DbSet<CentreCout> CentresCout { get; }
    DbSet<CleRepartition> ClesRepartition { get; }
    DbSet<CleRepartitionManuelle> ClesRepartitionManuelles { get; }
    DbSet<Charge> Charges { get; }
    DbSet<Employe> Employes { get; }
    DbSet<AffectationEmploye> AffectationsEmploye { get; }
    DbSet<ElementRemuneration> ElementsRemuneration { get; }
    DbSet<Absence> Absences { get; }

    DatabaseFacade Database { get; }
    Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<T> Entry<T>(T entite) where T : class;
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    /// <summary>Pose un verrou de ligne exclusif (SELECT … FOR UPDATE) jusqu'à la fin de la transaction.</summary>
    Task VerrouillerAsync(string table, Guid id, CancellationToken ct = default);
}

/// <summary>Utilisateur authentifié à l'origine de la requête.</summary>
public interface IUtilisateurCourant
{
    Guid? Id { get; }
    string Login { get; }
    string? AdresseIp { get; }
    bool APermission(string code);
}

/// <summary>Horloge métier (fuseau Africa/Casablanca).</summary>
public interface IHorloge
{
    DateTimeOffset Maintenant { get; }
    DateOnly Aujourdhui { get; }
    DateOnly JourneeDe(DateTimeOffset instant);
}

public interface INumerotation
{
    /// <summary>Attribue le prochain numéro d'un type de document (verrou de ligne, sans trou ni doublon dans la transaction).</summary>
    Task<string> ProchainAsync(string typeDocument, CancellationToken ct = default);
}

public interface IHachageMotDePasse
{
    string Hacher(string motDePasse);
    bool Verifier(string hash, string motDePasse);
}
