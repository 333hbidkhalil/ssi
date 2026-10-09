namespace Ssi.Contracts;

// ---------- Rentabilité (R-31 à R-36) ----------
public sealed record RatioDto(decimal? Valeur, string? Raison);
public sealed record LigneRentabiliteDto(Activite Activite, string Libelle, decimal Quantite, string Unite, decimal ChiffreAffairesHt, decimal CoutDesVentes,
    decimal MargeBrute, RatioDto MargeUnitaire, RatioDto TauxMarque, RatioDto TauxMarge, decimal FraisDirects, decimal Contribution,
    decimal ChargesDirectes, decimal ChargesReparties, decimal ResultatEstime, RatioDto PartChiffreAffairesPct);
public sealed record RepartitionChargeDto(string Source, string Centre, string Cle, decimal Montant, IReadOnlyDictionary<Activite, decimal> ParActivite);
public sealed record RentabiliteDto(DateOnly Du, DateOnly Au, IReadOnlyList<LigneRentabiliteDto> Activites, LigneRentabiliteDto Total,
    IReadOnlyList<RepartitionChargeDto> Charges, bool Incomplet, IReadOnlyList<string> Avertissements, string Methode);

public sealed record SeuilRentabiliteDto(DateOnly Du, DateOnly Au, decimal ChiffreAffairesHt, decimal CoutsVariables, decimal ChargesFixes,
    RatioDto TauxMcvPct, RatioDto SeuilChiffreAffaires, RatioDto SeuilVolumeCarburant, RatioDto ProgressionPct, string Methode);

public sealed record HypotheseActiviteDto(Activite Activite, decimal VariationPrixVenteHt, decimal VariationPrixAchatHt, decimal VariationVolumePct);
public sealed record SimulationDemandeDto(DateOnly Du, DateOnly Au, IReadOnlyList<HypotheseActiviteDto> Hypotheses, decimal VariationChargesFixes,
    decimal VariationChargesVariablesPct);
public sealed record LigneSimulationDto(Activite Activite, string Libelle, decimal QuantiteAvant, decimal QuantiteApres, decimal MargeAvant, decimal MargeApres,
    decimal ContributionAvant, decimal ContributionApres);
public sealed record SimulationDto(DateOnly Du, DateOnly Au, IReadOnlyList<LigneSimulationDto> Activites, decimal ResultatAvant, decimal ResultatApres,
    decimal Ecart, string Avertissement);

// ---------- Tableau de bord et alertes ----------
public sealed record AlerteDto(string Code, GraviteAlerte Gravite, string Module, string Message, string? ObjetType, Guid? ObjetId);
public sealed record PointJourDto(DateOnly Date, decimal ChiffreAffairesHt, decimal VolumeCarburant);
public sealed record IndicateurPeriodeDto(decimal ChiffreAffairesHt, decimal MargeBrute, decimal VolumeGasoil, decimal VolumeSansPlomb, decimal VolumeVrac,
    decimal VentesArticlesHt);
public sealed record TableauBordDto(DateTimeOffset Calcule, IndicateurPeriodeDto Jour, IndicateurPeriodeDto Mois, decimal TresorerieDisponible,
    decimal FondsEnTransit, decimal Creances, decimal Dettes, decimal? TresorerieJ30, IReadOnlyList<EtatCuveDto> Cuves, IReadOnlyList<PointJourDto> Evolution30Jours,
    IReadOnlyList<AlerteDto> Alertes, int PostesOuverts, int PostesAValider, bool MargesMasquees);

// ---------- Contrôles transverses (R-38, R-39) ----------
public sealed record ResultatControleDto(string Code, string Libelle, bool Conforme, int Anomalies, IReadOnlyList<string> Details);

// ---------- Rapports et exports (R-40) ----------
public sealed record ColonneRapportDto(string Cle, string Libelle, string Type);
public sealed record RapportDto(string Code, string Titre, string Filtres, IReadOnlyList<ColonneRapportDto> Colonnes,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Lignes, IReadOnlyDictionary<string, decimal> Totaux, DateTimeOffset GenereLe, string GenerePar);
