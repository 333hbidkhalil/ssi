# SSI — Journal des phases

Une fonctionnalité n'est déclarée opérationnelle que si elle est reliée à la base et couverte par un test automatisé (voir `06-tests-acceptation.md`). Dernière mise à jour : 09/10/2026.

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

* Fait : 55 tests automatisés verts, dont la concurrence (validation simultanée), les permissions, l'idempotence, la sauvegarde et la restauration.
* Fait : client Windows WPF à 12 écrans (connexion, menu selon les droits, postes et relevés, caisse, ventes d'articles, stocks et jaugeages, vrac, clients et fournisseurs, factures et règlements, trésorerie et charges, rentabilité et simulation, rapports et exports, administration). Il compile et chaque lecture d'écran est testée contre l'API (`ContratClientWpf`), ce qui a déjà permis de corriger deux défauts (validation de caisse sans motif, détail des erreurs de saisie).
* Reste : recette visuelle du client sur Windows ; essai de charge à 8 postes simultanés.

## Phase 7 — Livraison : préparée

* Fait : `deploy/LISEZMOI.md`, installation serveur et postes, sauvegarde vérifiée planifiée avec copie externe facultative, restauration sans écrasement avec bascule contrôlée. Les scripts de sauvegarde et de restauration ont été exécutés sous PowerShell 7.
* Reste : première installation sur un poste Windows de test (service, pare-feu, certificat, tâche planifiée).

## Limites connues

* Écrans non encore réalisés dans le client (l'API et ses tests existent) : paramétrage des équipements et du catalogue, achats et réceptions, saisie libre de factures et avoirs, personnel. Ces opérations se font aujourd'hui par l'API ; elles sont la prochaine étape du client.
* SSI est un outil de gestion. Il n'est pas un logiciel de comptabilité certifié et ne remplace pas la tenue comptable légale.
* Les taux de TVA initiaux sont à valider par le comptable (H-03) avant toute mise en production.
