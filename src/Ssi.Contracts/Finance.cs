namespace Ssi.Contracts;

// ---------- Comptes et mouvements ----------
public sealed record CompteFinancierDto(Guid Id, string Code, string Libelle, TypeCompteFinancier Type, string? Banque, string? Rib, bool Actif, decimal Solde);
public sealed record EnregistrementCompteDto(string Code, string Libelle, TypeCompteFinancier Type, string? Banque, string? Rib, bool Actif);
public sealed record MouvementFinancierDto(long Id, Guid CompteId, string Compte, DateOnly DateOperation, DateTimeOffset Horodatage, decimal Montant,
    NatureMouvementFinancier Nature, string Libelle, string? DocumentRef, long? MouvementAnnuleId, decimal? SoldeApres);
public sealed record SoldeInitialCompteDto(Guid CompteId, decimal Montant, DateOnly Date, string Motif);

// ---------- Règlements ----------
public sealed record AffectationSaisieDto(Guid? FactureId, Guid? ChargeId, decimal Montant);
public sealed record CreationReglementDto(SensReglement Sens, Guid? ClientId, Guid? FournisseurId, string? Beneficiaire, ModeReglement Mode,
    Guid? CompteId, DateOnly Date, decimal Montant, string? Reference, IReadOnlyList<AffectationSaisieDto> Affectations);
public sealed record ReglementDto(Guid Id, string Numero, SensReglement Sens, Guid? TiersId, string? Tiers, ModeReglement Mode, Guid CompteId, string Compte,
    DateOnly Date, decimal Montant, decimal MontantAffecte, decimal NonAffecte, string? Reference, StatutReglement Statut,
    IReadOnlyList<AffectationDto> Affectations, string? MotifAnnulation);
public sealed record AffectationDto(Guid? FactureId, Guid? ChargeId, string Piece, decimal Montant);

// ---------- Transferts ----------
public sealed record CreationTransfertDto(Guid CompteSourceId, Guid CompteDestinationId, decimal Montant, decimal Commission, DateOnly Date, string Libelle);
public sealed record TransfertDto(Guid Id, string Numero, Guid CompteSourceId, string CompteSource, Guid CompteDestinationId, string CompteDestination,
    decimal Montant, decimal Commission, decimal MontantNetCredite, DateOnly Date, string Libelle, bool Annule);

// ---------- Caisse ----------
public sealed record OuvertureCaisseDto(Guid? CompteId, decimal? SoldeOuvertureCompte, string? Commentaire);
public sealed record OperationCompteDto(decimal Montant, bool Entree, NatureMouvementFinancier Nature, string Libelle, string? Reference);
public sealed record OperationCaisseDto(decimal Montant, bool Entree, string Libelle, string? Reference);
public sealed record ClotureCaisseDto(decimal MontantCompte, string? Commentaire);
public sealed record SessionCaisseDto(Guid Id, string Numero, Guid CompteId, string Compte, DateTimeOffset Ouverture, decimal SoldeOuverture,
    DateTimeOffset? Cloture, decimal Encaissements, decimal Decaissements, decimal SoldeTheorique, decimal? MontantCompte, decimal? Ecart,
    StatutSessionCaisse Statut, string? Commentaire, int NombreReouvertures, IReadOnlyList<MouvementFinancierDto> Mouvements, IReadOnlyList<string> Alertes);

// ---------- Banque ----------
public sealed record LigneReleveSaisieDto(DateOnly Date, string Libelle, decimal Montant);
public sealed record CreationRapprochementDto(Guid CompteId, DateOnly DateReleve, decimal SoldeReleve, IReadOnlyList<LigneReleveSaisieDto> Lignes);
public sealed record PointageDto(Guid LigneId, long? MouvementId);
public sealed record LigneRapprochementDto(Guid Id, DateOnly Date, string Libelle, decimal Montant, long? MouvementId, IReadOnlyList<long> Suggestions);
public sealed record RapprochementDto(Guid Id, Guid CompteId, string Compte, DateOnly DateReleve, decimal SoldeReleve, decimal SoldeSsi,
    decimal LignesNonPointees, decimal MouvementsNonPointes, decimal EcartInexplique, bool Valide, IReadOnlyList<LigneRapprochementDto> Lignes,
    IReadOnlyList<MouvementFinancierDto> MouvementsSsiNonPointes);

// ---------- Situation ----------
public sealed record SituationTresorerieDto(DateOnly Date, IReadOnlyList<CompteFinancierDto> Comptes, decimal Disponible, decimal EnAttente,
    decimal CreancesClients, decimal BonsNonFactures, decimal DettesFournisseurs, decimal ChargesAPayer, IReadOnlyList<PrevisionTresorerieDto> Previsions);
public sealed record PrevisionTresorerieDto(int Horizon, decimal Disponible, decimal FondsEnTransit, decimal EncaissementsAttendus,
    decimal DecaissementsPrevus, decimal TresorerieProjetee, decimal CreancesEchuesNonIncluses, string Hypotheses);
public sealed record EcheanceDto(Guid DocumentId, string Piece, string Tiers, Guid? TiersId, DateOnly Date, DateOnly Echeance, decimal Montant,
    decimal ResteDu, int JoursRetard, string Type);

// ---------- Charges ----------
public sealed record CategorieChargeDto(Guid Id, string Code, string Libelle, bool FixeParDefaut);
public sealed record CentreCoutDto(Guid Id, string Code, string Libelle, Activite? Activite, Guid? CleRepartitionId, string? CleRepartition);
public sealed record CreationChargeDto(DateOnly Date, Guid? FournisseurId, string? Beneficiaire, Guid CategorieId, Guid CentreCoutId, decimal MontantHt,
    decimal MontantTva, bool? Fixe, DateOnly? Echeance, string? Description, string? ReferencePiece, ModeReglement? PayeeImmediatement, Guid? CompteId);
public sealed record ChargeDto(Guid Id, string Numero, DateOnly Date, Guid? FournisseurId, string? Tiers, Guid CategorieId, string Categorie,
    Guid CentreCoutId, string CentreCout, decimal MontantHt, decimal MontantTva, decimal MontantTtc, bool Fixe, DateOnly Echeance, string? Description,
    string? ReferencePiece, StatutDocument Statut, decimal Regle, decimal ResteDu);

// ---------- Personnel ----------
public sealed record EmployeDto(Guid Id, string Matricule, string Nom, string Prenom, string? Cin, string Fonction, DateOnly DateEmbauche,
    string? Affectation, decimal? SalaireBase, string? Telephone, bool Actif);
public sealed record EnregistrementEmployeDto(string Matricule, string Nom, string Prenom, string? Cin, string Fonction, DateOnly DateEmbauche,
    string? Affectation, decimal? SalaireBase, string? Telephone, bool Actif);   // SalaireBase null = inchangé (utilisateur sans accès aux salaires)
public sealed record ElementRemunerationDto(Guid Id, Guid EmployeId, string Employe, DateOnly Periode, TypeElementRemuneration Type, decimal? Montant, string? Commentaire);
public sealed record SaisieElementRemunerationDto(Guid EmployeId, DateOnly Periode, TypeElementRemuneration Type, decimal Montant, string? Commentaire);
public sealed record AbsenceDto(Guid Id, Guid EmployeId, DateOnly Debut, DateOnly Fin, string Type, bool Justifiee);
public sealed record SaisieAbsenceDto(Guid EmployeId, DateOnly Debut, DateOnly Fin, string Type, bool Justifiee);
public sealed record MasseSalarialeDto(DateOnly Periode, decimal? Total, IReadOnlyDictionary<TypeElementRemuneration, decimal>? ParType, int Effectif);
