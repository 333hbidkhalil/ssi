namespace Ssi.Contracts;

// Énumérations partagées par le domaine, l'API et le client WPF (sérialisées en texte).

public enum ModeReglement { Especes = 1, Carte = 2, Cheque = 3, Virement = 4, Credit = 5, Effet = 6, Autre = 7 }
public enum StatutPoste { Ouvert = 1, Cloture = 2, Valide = 3 }
public enum StatutReleve { Saisi = 1, Valide = 2, Annule = 3 }
public enum StatutDocument { Brouillon = 1, Valide = 2, Annule = 3 }
public enum StatutCommande { Brouillon = 1, Confirmee = 2, EnCours = 3, Terminee = 4, Annulee = 5 }
public enum StatutDemande { Demande = 1, Approuve = 2, Rejete = 3 }
public enum TypeEvenementCompteur { RemiseAZero = 1, Remplacement = 2, Correction = 3 }
public enum CategorieProduit { Carburant = 1, Lubrifiant = 2, Annexe = 3 }
public enum TypeCarburant { Gasoil = 1, SansPlomb = 2 }
public enum MethodeValorisation { CoutMoyenPondere = 1 }
public enum TypePrix { Vente = 1, AchatReference = 2 }
public enum MethodeMesure { JaugeManuelle = 1, Sonde = 2, BaremeHauteur = 3, Autre = 4 }
public enum CauseEcart { NonDeterminee = 0, AnomalieMesure = 1, Temperature = 2, ErreurSaisie = 3, PerteReelle = 4, Autre = 5 }
public enum StatutJaugeage { Saisi = 1, Explique = 2, Regularise = 3 }
public enum TypeClient { Comptant = 1, Credit = 2 }
public enum DestinationAchat { Cuve = 1, Magasin = 2, LivraisonDirecte = 3 }
public enum SensFacture { Client = 1, Fournisseur = 2 }
public enum TypeFacture { Facture = 1, Avoir = 2 }
public enum OrigineFacture { Vrac = 1, BonsStation = 2, Articles = 3, AchatStock = 4, AchatVrac = 5, Transport = 6, Autre = 7 }
public enum StatutLivraison { Planifiee = 1, Validee = 2, Annulee = 3 }
public enum TypeFrais { Transport = 1, Autre = 2 }
public enum MethodeRepartitionFrais { Volume = 1, Manuelle = 2 }
public enum TypeCompteFinancier { Caisse = 1, Banque = 2, AttenteCarte = 3, ChequesAEncaisser = 4 }
public enum SensReglement { Encaissement = 1, Decaissement = 2 }
public enum StatutReglement { Valide = 1, Annule = 2 }
public enum StatutSessionCaisse { Ouverte = 1, Cloturee = 2, Validee = 3 }
public enum MethodeCleRepartition { ChiffreAffaires = 1, Volume = 2, MargeBrute = 3, CoutDirect = 4, Manuelle = 5 }
public enum Activite { GasoilStation = 1, SansPlombStation = 2, GasoilVrac = 3, SansPlombVrac = 4, Lubrifiants = 5, Autres = 6 }
public enum TypeElementRemuneration { Salaire = 1, Prime = 2, Avance = 3, Retenue = 4, ChargeSociale = 5 }
public enum EtatReglementFacture { Ouverte = 1, PartiellementReglee = 2, Soldee = 3 }
public enum TypeMouvementStock
{
    StockInitial = 1, Reception = 2, Distribution = 3, VenteArticle = 4, RetourClient = 5,
    RetourFournisseur = 6, TransfertEntrant = 7, TransfertSortant = 8, Ajustement = 9, ContrePassation = 10, AjustementValeur = 11
}
public enum NatureMouvementFinancier
{
    SoldeInitial = 1, EncaissementVenteStation = 2, EncaissementVenteArticle = 3, EncaissementClient = 4,
    PaiementFournisseur = 5, PaiementCharge = 6, TransfertInterne = 7, FraisBancaires = 8, EcartCaisse = 9,
    ContrePassation = 10, Autre = 11
}
public enum GraviteAlerte { Info = 1, Attention = 2, Critique = 3 }

public sealed record Page<T>(IReadOnlyList<T> Elements, int Total, int NumeroPage, int Taille);

/// <summary>Valeur calculée accompagnée de sa disponibilité (ratio non calculable, résultat provisoire…).</summary>
public sealed record ValeurCalculee(decimal? Valeur, string? Remarque);

public sealed record ReferenceDto(Guid Id, string Code, string Libelle);

public sealed record MotifDto(string Motif);

/// <summary>Motif facultatif (exigé par la règle métier seulement dans certains cas, par ex. écart de caisse au-delà du seuil).</summary>
public sealed record MotifOptionnelDto(string? Motif);

public sealed record AnomalieDto(string Code, string Message, bool Bloquante);
