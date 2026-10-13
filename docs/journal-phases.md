# SSI — Journal des phases

Une fonctionnalité n'est déclarée opérationnelle que si elle est reliée à la base et couverte par un test automatisé (voir `06-tests-acceptation.md`). Dernière mise à jour : 13/10/2026.

## Phase 1 — Analyse : terminée

Livrables `docs/00` à `docs/06` : hypothèses révisables et questions ciblées, architecture, flux métier, modèle de données, règles de calcul, plan, tests d'acceptation.

## Phase 2 — Socle : terminée

Solution .NET 10, PostgreSQL 16 et migrations EF Core, authentification JWT, rôles et permissions `module.action`, journal d'audit en ajout seul (triggers), idempotence (`Idempotency-Key`), une transaction par requête, verrous de ligne, numérotation annuelle, paramètres, Serilog, erreurs métier en français (ProblemDetails).

## Phase 3 — Exploitation : terminée

Catalogue et prix datés, cuves, volucompteurs, pistolets, postes et relevés d'index (contrôles, clôture, validation, réouverture motivée), achats et réceptions, stocks au CMP, jaugeages et écarts, ajustements soumis à approbation par un tiers, transferts, inventaires d'articles, livraisons directes (vrac) sans effet sur le stock, marge prévisionnelle puis réelle, dérogation de marge motivée et tracée.

## Phase 4 — Finance : terminée

Factures et avoirs, facture récapitulative des bons station (sans chiffre d'affaires supplémentaire), règlements et affectations (sur-règlement refusé, acomptes), créances et dettes recalculées, caisse (sessions, comptage, écart, validation, réouverture), comptes et mouvements financiers, transferts internes avec commission, rapprochement bancaire, charges, personnel (salaires visibles seulement avec la permission sensible). H-07 traité : l'écart de prix sur facture fournisseur est incorporé au stock restant, la part déjà vendue passe en coût de la période.

## Phase 5 — Contrôle de gestion : terminée

Rentabilité par activité (station gasoil et sans plomb, vrac, lubrifiants, autres), répartition des charges indirectes par clé, seuil de rentabilité, simulations sans écriture, tableau de bord, alertes (dont sauvegarde non vérifiée, sans jamais qualifier une anomalie de fraude), contrôles de cohérence transverses, rapports filtrables et exports CSV, Excel et PDF identiques à l'écran, facture PDF.

## Phase 6 — Validation : en cours

* Fait : 60 tests automatisés verts, sous Linux et sous Windows, dont la concurrence (validation simultanée), les permissions, l'idempotence, la sauvegarde et la restauration.
* Fait : client Windows WPF à 15 écrans (connexion, menu selon les droits, postes et relevés, caisse, ventes d'articles, stocks et jaugeages, achats et réceptions, vrac, clients et fournisseurs, factures, avoirs et règlements, trésorerie et charges, personnel, rentabilité et simulation, rapports et exports, paramétrage de la station, administration). Toutes les opérations de l'API ont désormais un écran. Il compile et chaque lecture d'écran est testée contre l'API (`ContratClientWpf`), ce qui a déjà permis de corriger deux défauts (validation de caisse sans motif, détail des erreurs de saisie).
* Fait : essai de charge à 8 postes simultanés (`ScenarioCharge`), chaque poste avec son propre compte et son rôle réel : trois pompistes vendent au comptoir le même article (avec double envoi d'une requête sur cinq), la caisse vend et verse en banque, le commercial facture, le responsable de station enchaîne des postes de pompe complets, le comptable et l'auditeur consultent tableau de bord, rentabilité, rapports et contrôles. Résultat sur la machine de test : 432 appels en 9 s, aucune erreur ni conflit, aucune vente perdue ou doublée, stock et caisse exacts au dirham près, numérotation continue, contrôles de cohérence conformes ; médiane d'une vente 107 ms, 95 % des appels sous 2 s (le plus lent : tableau de bord pendant les écritures, 1,9 s).
* Fait : recette complète sur Windows à chaque envoi de code (`docs/recette-windows.md`) : installation serveur et poste par les scripts livrés, 34 écrans et onglets ouverts par un robot sans erreur, sauvegarde planifiée VERIFIEE et restauration. Elle a permis de corriger huit défauts, dont trois écrans absents du menu et une sauvegarde planifiée qui échouait sous Windows.
* Reste : un regard humain sur le poste réel de la station ; mesurer les temps de réponse sur le serveur et le réseau réels (l'essai de charge tourne dans le même processus que l'API, sans réseau).

## Phase 7 — Livraison : préparée

* Fait : `deploy/LISEZMOI.md`, installation serveur et postes, sauvegarde vérifiée planifiée avec copie externe facultative, restauration sans écrasement avec bascule contrôlée. Tous les scripts ont été exécutés sur Windows Server (installation du serveur et d'un poste, service, pare-feu, certificat, tâche planifiée, sauvegarde vérifiée, restauration).
* Reste : installation sur le matériel de la station et choix du support de copie externe des sauvegardes.

## Limites connues

* Le client a été recetté sur Windows par un robot (captures dans la branche `recette-windows`) ; un regard humain sur le poste réel reste nécessaire.
* SSI est un outil de gestion. Il n'est pas un logiciel de comptabilité certifié et ne remplace pas la tenue comptable légale.
* Les taux de TVA initiaux sont à valider par le comptable (H-03) avant toute mise en production.
