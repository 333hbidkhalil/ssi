namespace Ssi.Contracts;

// ---------- Tiers ----------
public sealed record ClientDto(Guid Id, string Code, string Nom, TypeClient Type, string? Ice, string? IdentifiantFiscal, string? RegistreCommerce,
    string? Adresse, string? Ville, string? Telephone, string? Email, string? Contact, int DelaiPaiementJours, decimal PlafondCredit, bool Actif,
    string? Notes, decimal Creance, decimal BonsNonFactures, decimal Encours, bool DepassementPlafond);
public sealed record EnregistrementClientDto(string Code, string Nom, TypeClient Type, string? Ice, string? IdentifiantFiscal, string? RegistreCommerce,
    string? Adresse, string? Ville, string? Telephone, string? Email, string? Contact, int DelaiPaiementJours, decimal PlafondCredit, bool Actif, string? Notes);
public sealed record FournisseurDto(Guid Id, string Code, string Nom, string? Ice, string? IdentifiantFiscal, string? RegistreCommerce,
    string? Adresse, string? Ville, string? Telephone, string? Email, string? Contact, int DelaiPaiementJours, string? ConditionsAchat,
    string? ProduitsFournis, bool Actif, string? Notes, decimal Dette);
public sealed record EnregistrementFournisseurDto(string Code, string Nom, string? Ice, string? IdentifiantFiscal, string? RegistreCommerce,
    string? Adresse, string? Ville, string? Telephone, string? Email, string? Contact, int DelaiPaiementJours, string? ConditionsAchat,
    string? ProduitsFournis, bool Actif, string? Notes);

public sealed record LigneReleveCompteDto(DateOnly Date, string Piece, string Libelle, decimal Debit, decimal Credit, decimal Solde, DateOnly? Echeance);
public sealed record ReleveCompteDto(string Tiers, DateOnly? Du, DateOnly? Au, decimal SoldeOuverture, IReadOnlyList<LigneReleveCompteDto> Lignes,
    decimal TotalDebit, decimal TotalCredit, decimal SoldeCloture, string Sens);
public sealed record RelanceDto(Guid Id, Guid ClientId, Guid? FactureId, DateOnly Date, string Moyen, string? Commentaire, string Par);
public sealed record SaisieRelanceDto(Guid? FactureId, string Moyen, string? Commentaire);
public sealed record IncidentFournisseurDto(Guid Id, DateOnly Date, string Type, string Description, string? DocumentRef);
public sealed record EvaluationFournisseurDto(Guid FournisseurId, string Fournisseur, int NombreLivraisons, decimal VolumeLivre, decimal EcartQuantiteMoyenPct,
    decimal? DelaiMoyenJours, decimal? PrixMoyenHt, int Incidents, IReadOnlyList<IncidentFournisseurDto> DerniersIncidents);

// ---------- Achats ----------
public sealed record LigneCommandeFournisseurDto(Guid ProduitId, Guid? CuveId, decimal Quantite, decimal PrixUnitaireHt);
public sealed record CreationCommandeFournisseurDto(Guid FournisseurId, DateOnly Date, DateOnly? DatePrevue, DestinationAchat Destination,
    string? ReferenceFournisseur, string? Conditions, decimal FraisPrevusHt, IReadOnlyList<LigneCommandeFournisseurDto> Lignes);
public sealed record CommandeFournisseurDto(Guid Id, string Numero, Guid FournisseurId, string Fournisseur, DateOnly Date, DateOnly? DatePrevue,
    DestinationAchat Destination, Guid? CommandeClientId, string? ReferenceFournisseur, string? Conditions, decimal FraisPrevusHt, StatutCommande Statut,
    IReadOnlyList<LigneCommandeDetailDto> Lignes, decimal TotalHt);
public sealed record LigneCommandeDetailDto(Guid Id, Guid ProduitId, string Produit, Guid? CuveId, decimal Quantite, decimal? PrixUnitaireHt, decimal QuantiteRecue);

public sealed record LigneReceptionSaisieDto(Guid ProduitId, Guid? CuveId, decimal QuantiteCommandee, decimal QuantiteChargee, decimal QuantiteRecue,
    decimal QuantiteAcceptee, decimal PrixUnitaireHt, string? JustificationEcart);
public sealed record CreationReceptionDto(Guid? CommandeId, Guid FournisseurId, DateTimeOffset? Horodatage, string? NumeroBl, string? Transporteur,
    string? Vehicule, IReadOnlyList<LigneReceptionSaisieDto> Lignes);
public sealed record ReceptionDto(Guid Id, string Numero, Guid? CommandeId, Guid FournisseurId, string Fournisseur, DateTimeOffset Horodatage,
    string? NumeroBl, string? Transporteur, string? Vehicule, StatutDocument Statut, IReadOnlyList<LigneReceptionDto> Lignes, IReadOnlyList<string> Controles);
public sealed record LigneReceptionDto(Guid Id, Guid ProduitId, string Produit, Guid? CuveId, string? Cuve, decimal QuantiteCommandee,
    decimal QuantiteChargee, decimal QuantiteRecue, decimal QuantiteAcceptee, decimal? PrixUnitaireHt, string? JustificationEcart);

// ---------- Factures ----------
public sealed record LigneFactureSaisieDto(Guid? ProduitId, string? Designation, decimal Quantite, decimal PrixUnitaireHt, decimal? TauxTva,
    Guid? LivraisonId, Guid? ReceptionId);
public sealed record CreationFactureDto(SensFacture Sens, TypeFacture Type, OrigineFacture Origine, Guid? ClientId, Guid? FournisseurId,
    string? NumeroTiers, DateOnly Date, DateOnly? Echeance, Guid? FactureOrigineId, Guid? CommandeClientId, Guid? ReceptionId, string? Commentaire,
    IReadOnlyList<LigneFactureSaisieDto> Lignes);
public sealed record FactureDto(Guid Id, string Numero, SensFacture Sens, TypeFacture Type, OrigineFacture Origine, Guid? TiersId, string Tiers,
    string? NumeroTiers, DateOnly Date, DateOnly Echeance, decimal TotalHt, decimal TotalTva, decimal TotalTtc, decimal Regle, decimal AvoirsImputes,
    decimal ResteDu, EtatReglementFacture Etat, int JoursRetard, StatutDocument Statut, Guid? FactureOrigineId, Guid? CommandeClientId,
    string? Commentaire, IReadOnlyList<LigneFactureDto> Lignes, IReadOnlyList<string> Avertissements);
public sealed record LigneFactureDto(Guid Id, Guid? ProduitId, string Designation, decimal Quantite, decimal PrixUnitaireHt, decimal TauxTva,
    decimal MontantHt, decimal MontantTva, decimal MontantTtc, Guid? LivraisonId, Guid? ReceptionId);
public sealed record FacturationBonsDto(Guid ClientId, DateOnly? Jusqua, DateOnly Date);

// ---------- Vrac ----------
public sealed record CreationCommandeClientDto(Guid ClientId, Guid ProduitId, decimal Volume, decimal PrixVenteHt, DateOnly? DatePrevue,
    string? LieuLivraison, string? ConditionsReglement, string? Transporteur, Guid? FournisseurPrevuId);
public sealed record CommandeFournisseurVracDto(Guid FournisseurId, decimal Volume, decimal PrixAchatHt, decimal FraisPrevusHt, DateOnly? DatePrevue,
    string? ReferenceFournisseur, string? Conditions);
public sealed record ConfirmationCommandeClientDto(string? MotifDerogation);
public sealed record CommandeClientDto(Guid Id, string Numero, Guid ClientId, string Client, Guid ProduitId, string Produit, decimal Volume,
    decimal? PrixVenteHt, DateOnly? DatePrevue, string? LieuLivraison, string? ConditionsReglement, string? Transporteur, Guid? FournisseurPrevuId,
    StatutCommande Statut, decimal VolumeLivre, MargeVracDto? Marge, IReadOnlyList<CommandeFournisseurDto> CommandesFournisseur,
    IReadOnlyList<LivraisonDto> Livraisons, string? DerogationMotif);
public sealed record SaisieLivraisonDto(Guid CommandeFournisseurId, DateTimeOffset? Horodatage, decimal QuantiteChargee, decimal QuantiteLivree,
    string? NumeroBlFournisseur, string? NumeroReceptionClient, string? Vehicule, string? Transporteur, string? Chauffeur, string? JustificationEcart,
    string? Reserves);
public sealed record LivraisonDto(Guid Id, string Numero, Guid CommandeClientId, Guid? CommandeFournisseurId, Guid FournisseurId, string Fournisseur,
    Guid ClientId, DateTimeOffset Horodatage, decimal QuantiteCommandee, decimal QuantiteChargee, decimal QuantiteLivree, decimal EcartQuantite,
    decimal? EcartPct, string? NumeroBlFournisseur, string? NumeroReceptionClient, string? Vehicule, string? Transporteur, string? Chauffeur,
    decimal? PrixAchatHt, decimal? PrixVenteHt, string? JustificationEcart, string? Reserves, StatutLivraison Statut, bool Facturee, decimal? FraisDirectsHt);
public sealed record SaisieFraisDirectDto(TypeFrais Type, string Libelle, Guid? FournisseurId, Guid? FactureId, DateOnly Date, decimal MontantHt,
    MethodeRepartitionFrais Methode, IReadOnlyList<Guid> LivraisonIds, IReadOnlyList<decimal>? MontantsManuels);
public sealed record FraisDirectDto(Guid Id, TypeFrais Type, string Libelle, DateOnly Date, decimal MontantHt, MethodeRepartitionFrais Methode,
    IReadOnlyList<RepartitionFraisDto> Repartitions, Guid? FactureId);
public sealed record RepartitionFraisDto(Guid LivraisonId, string Livraison, decimal MontantHt);
public sealed record MargeVracDto(decimal? MargePrevue, decimal? TauxMargePrevuPct, decimal ChiffreAffairesNetHt, decimal CoutAchatHt,
    decimal FraisTransportHt, decimal AutresFraisHt, decimal CoutDirectHt, decimal MargeReelle, decimal? EcartVsPrevue, bool Provisoire,
    IReadOnlyList<string> DonneesManquantes, decimal EcartQuantiteLitres, decimal ResteAEncaisser, decimal ResteAPayer, string Methode);

// ---------- Ventes d'articles ----------
public sealed record LigneVenteSaisieDto(Guid ProduitId, decimal Quantite, decimal? PrixUnitaireTtc);
public sealed record CreationVenteDto(Guid? ClientId, ModeReglement ModeReglement, Guid? CompteId, IReadOnlyList<LigneVenteSaisieDto> Lignes);
public sealed record VenteDto(Guid Id, string Numero, DateTimeOffset Horodatage, Guid? ClientId, string? Client, ModeReglement ModeReglement,
    StatutDocument Statut, decimal TotalHt, decimal TotalTva, decimal TotalTtc, IReadOnlyList<LigneVenteDto> Lignes, Guid? FactureId);
public sealed record LigneVenteDto(Guid ProduitId, string Produit, decimal Quantite, decimal PrixUnitaireTtc, decimal TauxTva, decimal MontantHt,
    decimal MontantTva, decimal MontantTtc, decimal? CoutUnitaire);
