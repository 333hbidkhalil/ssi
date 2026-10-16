# SSI — 00. Hypothèses révisables et questions ouvertes

Chaque hypothèse est appliquée dans le code tant qu'elle n'est pas révisée. Les paramètres marqués ⚙ sont modifiables sans développement (table `parametres`).

| N° | Hypothèse retenue | Impact si révisée |
|---|---|---|
| H-01 | Le carburant en station est vendu **par index** (clôture globale du poste). Les bons de crédit et paiements carte sont une **ventilation des règlements** du poste, jamais des ventes distinctes. | Ajout d'un mode « ticket » exclusif par pistolet |
| H-02 | Prix pompe saisis **TTC** ; HT = TTC / (1 + taux TVA en vigueur). Prix d'achat fournisseurs saisis **HT**. | Recalcul des conversions |
| H-03 | Taux de TVA initiaux chargés comme **données datées à valider** : carburants 10 %, lubrifiants et annexes 20 %. Aucun taux dans le code. La TIC est supposée incluse dans le prix d'achat et le prix pompe. | Saisie d'autres taux datés |
| H-04 | Valorisation des stocks au **coût moyen pondéré** perpétuel, par produit et par cuve (carburants) ou par produit (lubrifiants). | Ajout d'une méthode FIFO |
| H-05 | Un seul magasin pour les lubrifiants et produits annexes. | Ajout d'emplacements |
| H-06 | Un poste de travail couvre tous les volucompteurs ; un changement de prix en cours de poste impose une clôture de poste au moment du changement. | Relevés intermédiaires par pistolet |
| H-07 | Coût d'entrée en stock = prix de réception HT. À la validation de la facture fournisseur, l'écart (prix facturé − prix de réception) × quantité facturée est incorporé à la valeur du stock de l'emplacement ; la part correspondant aux quantités déjà sorties (approximation : 1 − stock actuel / quantité facturée) est reprise aussitôt en coût de la période. L'annulation de la facture contre-passe ces ajustements. | Affectation exacte par lot (FIFO) |
| H-08 | Vrac : le client est facturé sur la **quantité livrée**, le fournisseur facture la **quantité chargée** (paramétrable par commande). | Choix de la base de facturation |
| H-09 | Les bons de crédit station sont regroupés en **facture récapitulative** (mensuelle ou à la demande), sans CA supplémentaire. | — |
| H-10 | Caisse : une caisse principale + une caisse par poste de piste si configuré ; une seule session ouverte par caisse. | — |
| H-11 | Carte bancaire : encaissements carte enregistrés sur un compte d'attente « TPE » jusqu'à la remise bancaire ; la commission est une charge « Frais bancaires ». | — |
| H-12 | TLS sur le LAN avec certificat auto-signé déployé sur les postes. | HTTP simple en LAN isolé, déconseillé |
| H-13 | ⚙ Tolérances par défaut : écart chargé/livré vrac 0,5 % ; écart cuve 0,5 % ou 100 L ; volume inhabituel ×3 ; seuil marge vrac 2 %. | — |
| H-14 | RPO 1 h en journée, RTO 4 h, rétention 14 j / 8 sem / 12 mois. | Fréquence des sauvegardes |
| H-15 | Fuseau Africa/Casablanca ; la journée d'exploitation est la date locale de début du poste. | — |
| H-16 | Les rémunérations sont du **suivi de gestion** (coût du personnel), sans calcul de paie légale ni déclarations CNSS. | Module paie séparé |
| H-17 | Les pièces justificatives (≤ 10 Mo) sont stockées en base (`bytea`) pour qu'une sauvegarde unique couvre tout. | Stockage fichier |
| H-18 | Numérotation : `PREFIXE-AAAA-000001`, remise à 1 chaque année. ⚙ préfixes. | — |
| H-19 | Le stock d'articles (lubrifiants, annexes) ne peut pas devenir négatif : une vente au-delà du stock est refusée. | Autoriser le négatif avec régularisation |
| H-20 | Le compte financier par défaut d'un mode de règlement est le **plus ancien** compte actif du type correspondant (espèces → caisse, carte → attente TPE, chèque → chèques à encaisser, virement/effet → banque). | Choix explicite à chaque saisie |
| H-21 | Les salaires n'entrent dans la rentabilité que lorsqu'ils sont saisis en **charges** (catégorie personnel) ; le module personnel est un suivi, pas une comptabilisation. | Génération automatique des charges de personnel |
| H-22 | Frais bancaires et écarts de caisse sont répartis entre activités selon la clé « chiffre d'affaires ». | Autre clé paramétrable |
| H-23 | Vrac : le chiffre d'affaires est rattaché à la date de la facture client ; le coût d'achat et les frais sont rapprochés par livraison ; si la facture fournisseur manque, le coût est estimé au prix de commande et la période est signalée incomplète. | Rattachement à la date de livraison |
| H-24 | Une alerte « sauvegarde » est levée s'il n'existe aucune sauvegarde **vérifiée** (restauration de contrôle réussie) depuis 26 heures. | Seuil paramétrable |
| H-25 | La validation d'une session de caisse n'exige une explication que si l'écart dépasse le seuil paramétré ⚙ (`caisse.seuil_alerte_ecart`, 50 DH par défaut). | — |
| H-26 | Saisie du jour (format du tableau de l'exploitant) : le solde attendu en espèces = production (index) − bons clients − charges payées en caisse ; les espèces remises sont saisies, jamais déduites, et l'écart est affiché. Le prix d'un bon client est le prix à la pompe par défaut, modifiable s'il est convenu autrement (la différence apparaît dans l'écart). | Prix client imposé = prix pompe |
| H-27 | Les charges payées en caisse sur la recette du jour sont saisies TTC sans TVA récupérable, rattachées au centre « COMMUN », et réglées par la caisse à la validation de la journée (la réouverture annule ce paiement). Une facture avec TVA se saisit dans l'écran Charges. | Saisie de la TVA dans la saisie du jour |

## Questions ciblées (réponse attendue du propriétaire)

1. **Mode de vente en piste** : confirmez-vous la vente par index (H-01), ou certains pistolets enregistrent-ils un ticket par client ?
2. **Nombre d'équipements** : combien de volucompteurs, de pistolets et de cuves (capacités) ? Cela sert au paramétrage initial.
3. **Postes** : combien de postes par jour (ex. 6h–14h, 14h–22h, 22h–6h) ?
4. **TVA** : les taux de H-03 doivent être validés par votre comptable avant mise en production.
5. **Vrac** : base de facturation client et fournisseur (H-08) ?
6. **Carte bancaire** : délai et commission de votre banque sur les paiements TPE ?
7. **Serveur** : quel matériel (Windows 10/11 Pro ou Windows Server) et quel support de sauvegarde externe (NAS, disque USB) ?
