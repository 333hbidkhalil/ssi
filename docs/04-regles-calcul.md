# SSI — 04. Règles de calcul vérifiables

Chaque règle porte un identifiant `R-xx`, une formule, ses conditions, l'arrondi et un exemple chiffré. Les exemples sont repris tels quels dans les tests automatisés (`tests/Ssi.Domain.Tests`, classe indiquée).

Convention d'arrondi : `arrondi2(x)` = arrondi à 2 décimales, demi vers l'extérieur (`MidpointRounding.AwayFromZero`). Les volumes et prix ne sont jamais arrondis avant multiplication.

## A. Volucompteurs et ventes en station

**R-01 Volume distribué** = index_fin − index_debut.
Si index_fin < index_debut sans événement de remise à zéro validé : rejet.
Avec remise à zéro (passage par l'index maximum M) : volume = (M − index_debut) + index_fin — uniquement via un événement compteur validé.
Exemple : 125 430,50 → 126 210,75 ⇒ **780,25 L**. Test : `CalculsIndexTests`.

**R-02 Montant théorique TTC** = arrondi2(volume × prix_unitaire_ttc) ; prix figé au moment du relevé.
Exemple : 780,25 L × 12,85 DH = 10 026,2125 ⇒ **10 026,21 DH**.

**R-03 Montant HT** = arrondi2(TTC / (1 + taux_tva)) ; TVA = TTC − HT (le total TTC reste celui de R-02).
Exemple taux 10 % : 10 026,21 / 1,10 = 9 114,7363… ⇒ HT **9 114,74**, TVA **911,47**.

**R-04 Contrôles d'index** (tolérances dans `parametres`) :

| Contrôle | Règle | Effet |
|---|---|---|
| Fin < début | index_fin < index_debut | bloquant |
| Continuité | index_debut ≠ index_fin du relevé validé précédent (même pistolet, même cycle) | bloquant sauf correction autorisée avec motif |
| Doublon | même pistolet déjà relevé dans le poste | bloquant (contrainte unique) |
| Chevauchement | plage [début, fin] chevauchant une plage existante | bloquant (contrainte d'exclusion) |
| Volume inhabituel | volume > moyenne des 10 derniers relevés × `index.facteur_volume_inhabituel` (défaut 3) ou > `index.volume_max_poste` | avertissement à confirmer |
| Volume nul | volume = 0 sur un pistolet actif | avertissement |
| Absence de relève | pistolet actif sans relevé à la clôture | bloquant pour la clôture |
| Saisie probable erronée | index_fin − index_debut > 10 × moyenne, ou décimales > 3 | avertissement |

**R-05 Écart de poste** = Σ montants théoriques TTC − Σ règlements attribués (espèces + carte + chèques + crédit + autres).
Écart > 0 : manquant ; écart < 0 : excédent. Ventilé par mode et par opérateur.
Exemple : théorique 10 026,21 ; espèces 6 000 ; carte 3 000 ; bons crédit 1 000 ⇒ écart **26,21 DH (manquant)**.

**R-06 Volume théorique vs transactions** : pour chaque produit, volume des bons de crédit ≤ volume des index. Dépassement ⇒ alerte « bons supérieurs aux index » (incohérence de saisie).

## B. Prix

**R-07 Prix applicable** à l'instant t = l'unique ligne `prix_produits` (type Vente) telle que debut ≤ t < fin (fin nulle = en cours). Les périodes ne se chevauchent pas (contrainte d'exclusion). Un nouveau prix clôt automatiquement la période précédente à la date de début du nouveau.

**R-08 Immutabilité historique** : un relevé ou une vente stocke son prix unitaire et son taux de TVA. Aucune analyse ne relit le prix courant.

## C. Stocks

**R-09 Stock théorique d'une cuve** = Σ quantités signées de `mouvements_stock` de la cuve jusqu'à t,
c'est-à-dire stock initial + réceptions acceptées + transferts entrants − distributions − transferts sortants ± ajustements validés.
Les livraisons directes n'y figurent jamais.
Exemple : initial 10 000 ; réception acceptée 15 000 ; distribué 780,25 ⇒ **24 219,75 L**.

**R-10 Écart de jaugeage** : écart_L = mesuré − théorique ; écart_% = écart_L / théorique × 100 (si théorique > 0) ; valeur indicative = arrondi2(écart_L × CMP).
Exemple : mesuré 24 150 ; théorique 24 219,75 ⇒ **−69,75 L ; −0,288 %** ; CMP 11,20 ⇒ **−781,20 DH**.
Alerte si |écart_L| > tolerance_ecart_litres **ou** |écart_%| > tolerance_ecart_pct.

**R-11 Coût moyen pondéré (CMP)** à chaque entrée :
CMP_nouveau = (Q_avant × CMP_avant + Q_entrée × coût_entrée) / (Q_avant + Q_entrée).
Les sorties sont valorisées au CMP courant sans le modifier. Si Q_avant ≤ 0, CMP_nouveau = coût_entrée.
Exemple : 100 u à 50 ; entrée 50 u à 56 ⇒ CMP **52,00** ; sortie 30 u ⇒ coût des ventes **1 560,00**.

**R-12 Jours de couverture** = stock théorique / moyenne journalière distribuée sur les 7 derniers jours (si moyenne > 0).

**R-13 Rotation des stocks** = coût des ventes de la période / stock moyen valorisé (moyenne début/fin).

## D. Livraisons directes

**R-14 Marge prévisionnelle** = arrondi2(volume × (prix_vente_ht − prix_achat_ht)) − frais_prevus_ht.
Taux prévu = marge / CA prévu × 100. Si marge < 0 ou taux < `vrac.seuil_marge_pct` : confirmation refusée sauf permission `vrac.derogation` + motif.
Exemple : 20 000 L × (11,50 − 11,05) − 1 500 = **7 500,00 DH**.

**R-15 CA net de la livraison** = Σ montants HT des factures client liées − Σ avoirs HT liés.
**R-16 Coût direct** = Σ montants HT des factures fournisseur liées + Σ frais directs HT répartis.
**R-17 Marge réelle** = R-15 − R-16 ; écart = marge réelle − marge prévue.
Statut **Provisoire** si : facture client absente, facture fournisseur absente, ou frais prévu sans frais réel enregistré. Les manquants sont listés.
Exemple : livré 19 950 L facturé à 11,50 HT ⇒ CA 229 425,00 ; facture fournisseur 20 000 L × 11,05 = 221 000,00 ; transport 1 600,00 ⇒ marge réelle **6 825,00**, écart **−675,00**.

**R-18 Répartition d'un frais partagé** (méthode Volume) : part_i = arrondi2(frais × volume_livré_i / Σ volumes) ; le reliquat d'arrondi est affecté à la dernière livraison afin que Σ parts = frais.
Exemple : 1 000 DH sur 3 livraisons de 1 000 L chacune ⇒ 333,33 / 333,33 / **333,34**.

**R-19 Écart de quantité** = quantité_livrée − quantité_chargée ; justification obligatoire si |écart| / chargée × 100 > `vrac.tolerance_ecart_pct` (défaut 0,5 %). L'écart n'est jamais imputé automatiquement à la station.

## E. Factures, créances, dettes

**R-20 Ligne de facture** : HT = arrondi2(quantité × PU HT) ; TVA = arrondi2(HT × taux) ; TTC = HT + TVA. Totaux = Σ des lignes.
**R-21 Reste dû d'une facture** = TTC − Σ affectations de règlements valides − Σ avoirs imputés. Statut : Ouverte, Partiellement réglée, Soldée.
**R-22 Créance client** = Σ restes dus des factures client validées. **Encours crédit** = créance + bons de crédit station non encore facturés. Alerte si encours > plafond.
**R-23 Dette fournisseur** = Σ restes dus des factures fournisseur validées + charges à payer non soldées.
**R-24 Retard** = jours entre l'échéance et aujourd'hui si reste dû > 0 et échéance < aujourd'hui.
**R-25 Doublon de facture fournisseur** : bloquant si (fournisseur, numéro_tiers) existe déjà ; avertissement si (fournisseur, date, TTC) identiques.

## F. Caisse, banque, trésorerie

**R-26 Solde d'un compte financier** = Σ montants signés de ses mouvements.
**R-27 Solde théorique de caisse** = solde à l'ouverture + encaissements − décaissements de la session ; **écart** = compté − théorique.
Exemple : 2 000 + 6 000 − 500 = 7 500 ; compté 7 480 ⇒ écart **−20,00 DH**.
**R-28 Virement interne** : deux mouvements de nature `TransfertInterne` de même montant et de signes opposés ; exclus de tout calcul de CA, charges ou résultat.
**R-29 Trésorerie disponible** = Σ soldes des comptes Caisse + Banque. Les comptes AttenteCarte et ChequesAEncaisser sont présentés à part (« fonds en transit »). Une créance n'est jamais additionnée.
**R-30 Prévision à J+n** = trésorerie disponible + fonds en transit + Σ échéances clients ≤ J+n (non échues) × taux de recouvrement supposé (paramètre, défaut 100 %) − Σ échéances fournisseurs et charges ≤ J+n. Les créances déjà échues sont affichées séparément et ne sont pas incluses par défaut (hypothèse explicite).

## G. Rentabilité

**R-31 Marge brute** = CA net HT − coût des produits vendus (CPV).
CPV station = Σ volumes distribués × CMP de la cuve au moment de la sortie ; CPV lubrifiants = Σ quantités × CMP ; CPV vrac = R-16 hors frais.
**R-32 Marge unitaire** = marge brute / quantité (si quantité > 0, unité homogène). **Taux de marque** = MB / CA × 100 (si CA > 0). **Taux de marge** = MB / CPV × 100 (si CPV > 0). Sinon : « non calculable ».
**R-33 Contribution** = marge brute − frais directs non inclus dans le CPV (ex. transport vrac).
**R-34 Charges indirectes attribuées** : charge du centre commun × clé (part de l'activité selon la méthode : CA, volume, marge brute, coût direct ou pourcentage manuel). Chaque charge n'est déduite qu'une fois ; les charges d'un centre lié à une activité lui sont affectées directement.
**R-35 Résultat opérationnel estimé** = Σ contributions − Σ charges d'exploitation (directes d'activité + communes réparties). Marqué « incomplet » si une donnée source manque (marges vrac provisoires, période non clôturée).
**R-36 Seuil de rentabilité (activité homogène)** = charges fixes / taux de marge sur coûts variables, où taux MCV = (CA − coûts variables) / CA. Si taux MCV ≤ 0 : « non atteignable ». Volume équivalent = seuil CA / prix moyen HT.
Exemple : charges fixes 60 000 ; CA 500 000 ; coûts variables 470 000 ⇒ taux MCV 6 % ⇒ seuil **1 000 000 DH**.
**R-37 Simulation** : appliquée à une copie en mémoire des agrégats d'une période de référence ; aucune écriture en base sur les opérations.

## H. Contrôles transverses

**R-38 Cohérence stock** : pour toute cuve, position en cache = Σ mouvements (contrôle `GET /api/controles/stock`).
**R-39 Cohérence règlements** : Σ affectations d'un règlement ≤ montant du règlement ; Σ affectations sur une facture ≤ TTC − avoirs.
**R-40 Cohérence rapports** : les totaux d'un rapport et de son export proviennent de la même requête paramétrée (mêmes filtres).
