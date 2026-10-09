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
| H-07 | Coût d'entrée en stock = prix de commande HT ; un écart de prix constaté sur facture est passé en ajustement de valeur. | — |
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

## Questions ciblées (réponse attendue du propriétaire)

1. **Mode de vente en piste** : confirmez-vous la vente par index (H-01), ou certains pistolets enregistrent-ils un ticket par client ?
2. **Nombre d'équipements** : combien de volucompteurs, de pistolets et de cuves (capacités) ? Cela sert au paramétrage initial.
3. **Postes** : combien de postes par jour (ex. 6h–14h, 14h–22h, 22h–6h) ?
4. **TVA** : les taux de H-03 doivent être validés par votre comptable avant mise en production.
5. **Vrac** : base de facturation client et fournisseur (H-08) ?
6. **Carte bancaire** : délai et commission de votre banque sur les paiements TPE ?
7. **Serveur** : quel matériel (Windows 10/11 Pro ou Windows Server) et quel support de sauvegarde externe (NAS, disque USB) ?
