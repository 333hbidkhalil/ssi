namespace Ssi.Contracts;

// Catalogue
public sealed record FamilleDto(Guid Id, string Code, string Libelle, CategorieProduit Categorie);
public sealed record ProduitDto(Guid Id, string Code, string Designation, Guid FamilleId, string Famille, CategorieProduit Categorie,
    string Unite, string? Marque, string? Conditionnement, Guid TaxeId, string Taxe, decimal SeuilAlerte, TypeCarburant? TypeCarburant,
    bool Actif, decimal? PrixVenteTtc, decimal? PrixAchatReferenceHt);
public sealed record EnregistrementProduitDto(string Code, string Designation, Guid FamilleId, string Unite, string? Marque,
    string? Conditionnement, Guid TaxeId, decimal SeuilAlerte, TypeCarburant? TypeCarburant, bool Actif);
public sealed record PrixDto(Guid Id, string Type, decimal Prix, DateTimeOffset Debut, DateTimeOffset? Fin, string? Motif, string? ValidePar);
public sealed record NouveauPrixDto(decimal Prix, DateTimeOffset Debut, string Motif, bool AchatReference = false);

// Équipements
public sealed record CuveDto(Guid Id, string Code, Guid ProduitId, string Produit, decimal Capacite, decimal SeuilMinimal,
    decimal ToleranceEcartLitres, decimal ToleranceEcartPct, bool Actif, string? Observations);
public sealed record EnregistrementCuveDto(string Code, Guid ProduitId, decimal Capacite, decimal SeuilMinimal,
    decimal ToleranceEcartLitres, decimal ToleranceEcartPct, bool Actif, string? Observations);
public sealed record VolucompteurDto(Guid Id, string Code, string Libelle, bool Actif, IReadOnlyList<PistoletDto> Pistolets);
public sealed record EnregistrementVolucompteurDto(string Code, string Libelle, bool Actif);
public sealed record PistoletDto(Guid Id, Guid VolucompteurId, string Volucompteur, string Code, Guid ProduitId, string Produit,
    Guid CuveId, string Cuve, decimal IndexMaximum, decimal IndexInitialCycle, int Cycle, bool Actif, decimal? DernierIndex);
public sealed record EnregistrementPistoletDto(string Code, Guid ProduitId, Guid CuveId, decimal IndexMaximum, decimal IndexInitial, bool Actif);

// Postes et relevés
public sealed record OuverturePosteDto(Guid? ResponsableId, DateTimeOffset? Debut, string? Commentaire);
public sealed record PosteResumeDto(Guid Id, string Numero, DateTimeOffset Debut, DateTimeOffset? Fin, DateOnly JourneeExploitation,
    StatutPoste Statut, string? Responsable, decimal MontantTheoriqueTtc, decimal TotalReglements, decimal Ecart);
public sealed record PreparationReleveDto(Guid PistoletId, string Volucompteur, string Pistolet, string Produit, decimal IndexDebutPropose,
    decimal PrixUnitaireTtc, bool DejaReleve);
public sealed record SaisieReleveDto(Guid PistoletId, decimal? IndexDebut, decimal IndexFin, Guid? OperateurId, string? Commentaire,
    bool ConfirmerAvertissements, string? MotifCorrection);
public sealed record ReleveDto(Guid Id, Guid PistoletId, string Volucompteur, string Pistolet, string Produit, Guid? OperateurId, string? Operateur,
    decimal IndexDebut, decimal IndexFin, decimal Volume, decimal PrixUnitaireTtc, decimal TauxTva, decimal MontantTtc, decimal MontantHt,
    StatutReleve Statut, IReadOnlyList<AnomalieDto> Anomalies, string? Commentaire, string? MotifCorrection, DateTimeOffset Horodatage);
public sealed record ResultatReleveDto(ReleveDto? Releve, IReadOnlyList<AnomalieDto> Anomalies, bool Enregistre, string Message);
public sealed record SaisieReglementPosteDto(ModeReglement Mode, decimal Montant, Guid? ClientId, Guid? ProduitId, decimal? Volume,
    string? Reference, Guid? OperateurId);
public sealed record ReglementPosteDto(Guid Id, ModeReglement Mode, decimal Montant, Guid? ClientId, string? Client, Guid? ProduitId,
    decimal? Volume, string? Reference, Guid? OperateurId, string? Operateur, bool Facture);
public sealed record PosteDetailDto(PosteResumeDto Poste, IReadOnlyList<ReleveDto> Releves, IReadOnlyList<ReglementPosteDto> Reglements,
    RapportPosteDto Rapport);
public sealed record LigneRapportProduitDto(string Produit, decimal Volume, decimal MontantTtc, decimal MontantHt, decimal VolumeBonsCredit);
public sealed record LigneRapportOperateurDto(string Operateur, decimal Volume, decimal MontantTheoriqueTtc, decimal ReglementsAttribues, decimal Ecart);
public sealed record LigneRapportModeDto(ModeReglement Mode, decimal Montant);
public sealed record RapportPosteDto(decimal MontantTheoriqueTtc, decimal TotalReglements, decimal Ecart, string InterpretationEcart,
    IReadOnlyList<LigneRapportProduitDto> ParProduit, IReadOnlyList<ReleveDto> ParVolucompteur, IReadOnlyList<LigneRapportOperateurDto> ParOperateur,
    IReadOnlyList<LigneRapportModeDto> ParMode, IReadOnlyList<string> PistoletsNonReleves, IReadOnlyList<string> Alertes, string? Responsable);
public sealed record EvenementCompteurDto(Guid Id, Guid PistoletId, TypeEvenementCompteur Type, decimal AncienIndex, decimal NouvelIndex,
    DateTimeOffset Horodatage, string Motif, StatutDemande Statut);
public sealed record SaisieEvenementCompteurDto(TypeEvenementCompteur Type, decimal AncienIndex, decimal NouvelIndex, string Motif);
public sealed record DecisionDto(bool Approuve, string? Commentaire);
