# SSI — 05. Plan de développement

Chaque phase se termine par : livrables, tests exécutés, défauts connus, reste à faire (consigné dans `docs/journal-phases.md`). Une fonctionnalité n'est déclarée opérationnelle que si elle est reliée à la base et couverte par un test d'intégration.

| Phase | Contenu | Livrables | Critère de sortie |
|---|---|---|---|
| 1. Analyse | Flux, règles, modèle, maquettes, hypothèses | `docs/00` à `06` | Revue par le propriétaire |
| 2. Socle | Solution .NET 10, PostgreSQL + migrations, authentification JWT, rôles/permissions, audit append-only, idempotence, numérotation, paramètres, logs, gestion d'erreurs | API démarrable, base créée par migration, comptes initiaux | Tests : connexion, refus de permission, audit non modifiable, double validation refusée |
| 3. Exploitation | Catalogue et prix datés ; cuves, volucompteurs, pistolets ; postes et relevés ; clôture ; achats et réceptions ; stocks, jaugeages, ajustements ; lubrifiants (CMP) ; vrac complet | Endpoints + écrans WPF correspondants | Scénarios d'acceptation 1 à 6, 13, 14, 15 |
| 4. Finance | Comptes financiers, caisses et sessions, règlements et affectations, factures/avoirs, créances, dettes, charges, virements, rapprochement bancaire, personnel | Endpoints + écrans | Scénarios 7 à 12, 17 |
| 5. Contrôle de gestion | Rentabilité par activité, répartition des charges, indicateurs, tableau de bord, alertes, seuil de rentabilité, simulations, rapports PDF/Excel/CSV | Tableau de bord, rapports | Scénarios 16, 20 |
| 6. Validation | Tests fonctionnels complets, concurrence (8 clients simultanés), sécurité (permissions, injection, jetons), sauvegarde/restauration | Rapport de tests | Scénarios 18, 19 ; zéro défaut bloquant |
| 7. Livraison | Publication Windows (client + service API), installateur, scripts de sauvegarde, guides | Paquets, documentation | Installation testée sur poste vierge |

## Ordre de réalisation dans le code

1. Domaine pur + tests unitaires des règles R-01 à R-37 (aucune dépendance base).
2. Infrastructure EF Core + migration initiale + triggers append-only + données de référence (rôles, permissions, produits Gasoil/SP, taxes datées à valider).
3. API : authentification, permissions, audit, idempotence ; puis modules dans l'ordre Station → Stock → Achats → Vrac → Finance → Rentabilité.
4. Tests d'intégration des 20 scénarios d'acceptation sur PostgreSQL réel.
5. Client WPF : connexion, navigation par permission, écrans quotidiens (relevés, clôture, caisse) en priorité, puis tableau de bord.
6. Scripts `deploy/` : installation serveur, pare-feu, sauvegarde vérifiée, restauration.

## Environnement de développement et contraintes

* Le développement et les tests automatisés s'exécutent sous Linux (CI) avec PostgreSQL 16 ; le client WPF y est **compilé** (cible `net10.0-windows`) mais son exécution et l'installateur doivent être validés sur Windows.
* Le code source est versionné dans Git ; le dépôt GitHub est à désigner par le propriétaire.
