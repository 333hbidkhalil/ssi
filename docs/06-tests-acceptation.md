# SSI — 06. Tests d'acceptation

Les scénarios s'exécutent via l'API sur une base PostgreSQL réelle créée par les migrations (`tests/Ssi.IntegrationTests`, classe `ScenariosAcceptation*`). Données de départ communes (jeu « Station Démo ») :

* Produits : GO (Gasoil, TVA 10 %), SP (Sans plomb, TVA 10 %), LUB-15W40 (huile 5 L, TVA 20 %).
* Prix pompe TTC au 01/10/2026 : GO 12,85 ; SP 14,20.
* Cuves : C1-GO (40 000 L, stock initial 10 000 L à 11,20 HT), C2-SP (30 000 L).
* Volucompteur V1 : pistolet V1-GO → C1-GO ; V1-SP → C2-SP.
* Comptes : CAISSE-PRINC, BANQUE-1, TPE.
* Utilisateurs : `admin`, `caissier`, `pompiste`, `auditeur` (rôles correspondants).

| N° | Scénario | Données / actions | Résultat attendu | Règles |
|---|---|---|---|---|
| 1 | Volume = différence d'index | Relevé V1-GO 125 430,50 → 126 210,75 | volume 780,25 ; TTC 10 026,21 ; HT 9 114,74 | R-01–03 |
| 2 | Pas de double comptabilisation | Valider deux fois la clôture (même clé puis clé différente) ; saisir un 2e relevé V1-GO dans le poste | 1re validation OK, rejeu identique ; 2e clé ⇒ 409 ; 2e relevé ⇒ 409 ; un seul mouvement de stock | R-04 |
| 3 | Prix historiques préservés | Poste validé à 12,85 ; nouveau prix 13,10 ; relire le poste et le rapport des ventes | montant inchangé (10 026,21), prix 12,85 | R-07–08 |
| 4 | Réception augmente la cuve | Réception 15 000 L acceptés C1-GO à 11,30 HT | stock C1-GO = 10 000 + 15 000 = 25 000 L ; CMP 11,26 | R-09, R-11 |
| 5 | Vrac sans effet stock | Commande client 20 000 L GO, livraison 19 950 L | stocks C1-GO et C2-SP inchangés ; aucun mouvement de stock créé | R-09 |
| 6 | Marge vrac prévue puis réelle | Scénario 5 + factures client et fournisseur + transport 1 600 | prévue 7 500,00 ; avant facture fournisseur : « Provisoire » ; réelle 6 825,00 ; écart −675,00 | R-14–17 |
| 7 | Facture à crédit = créance sans trésorerie | Facture client 1 000 HT + TVA | créance +1 100 ; soldes caisse/banque inchangés | R-21–22, R-29 |
| 8 | Facture fournisseur = dette sans décaissement | Facture fournisseur 5 000 TTC | dette +5 000 ; soldes inchangés | R-23 |
| 9 | Règlement client | Règlement 600 par virement sur BANQUE-1 affecté à la facture du 7 | créance 500 ; BANQUE-1 +600 | R-21, R-26 |
| 10 | Encaissement sans nouveau CA | CA de la période avant et après le règlement du 9 | CA identique | Flux §1 |
| 11 | Virement interne neutre | Virement 2 000 CAISSE → BANQUE-1 | CA, charges, résultat inchangés ; trésorerie disponible inchangée | R-28 |
| 12 | Clôture de caisse et écart | Ouverture 2 000 ; encaissements 6 000 ; décaissement 500 ; compté 7 480 | théorique 7 500 ; écart −20,00 | R-27 |
| 13 | Ajustement contrôlé | Demande d'ajustement sans motif ; approbation par le demandeur ; approbation par un responsable | refus 400 ; refus 403/422 ; mouvement créé uniquement après approbation | §19 |
| 14 | Correction traçable | Annulation d'un règlement | règlement d'origine conservé (statut Annulé) + contre-passation ; audit avant/après | R-26, §7 |
| 15 | Validation simultanée | Deux clients valident la même clôture en parallèle (clés différentes) | exactement un succès, l'autre 409 ; un seul jeu de mouvements | Archi §6 |
| 16 | Rentabilité cohérente | Rapport de rentabilité de la période | CA GO station = Σ relevés validés HT ; MB = CA − CPV ; totaux = somme des lignes | R-31–35 |
| 17 | Rapprochement de période | Contrôle stock et contrôle comptes | positions = Σ mouvements ; soldes = Σ mouvements | R-38–39 |
| 18 | Restauration | `deploy/sauvegarde` puis restauration sur base vierge | comptages et totaux de contrôle identiques | Archi §9 |
| 19 | Permissions | `pompiste` lit les salaires, les marges, modifie un prix | 403 ; champs sensibles absents des réponses | Archi §7 |
| 20 | Exports conformes | Export CSV des ventes avec filtre produit | lignes et total = rapport affiché avec le même filtre | R-40 |

Les tests unitaires des règles (`tests/Ssi.Domain.Tests`) reprennent chaque exemple chiffré de `04-regles-calcul.md`.

## Correspondance avec les tests automatisés (état au 09/10/2026)

`dotnet test` exécute **60 tests, tous verts** : 27 tests unitaires des règles de calcul (`tests/Ssi.Domain.Tests`) et 33 tests d'intégration sur PostgreSQL 16 réel (`tests/Ssi.IntegrationTests`, une base créée par migration pour chaque classe de tests).

| Classe | Tests |
|---|---|
| `ScenariosStation` | Scénarios 1, 2, 3, 15 ; contrôles d'index (fin inférieure refusée, continuité, avertissements à confirmer) ; scénario 19 côté station (le pompiste ne peut ni modifier un prix ni administrer) |
| `ScenariosExploitation` | Scénarios 4, 5, 6, 6 bis (marge vrac insuffisante sans dérogation), 7-9-10-14, 8, 11, 12, 13 ; ventes d'articles ; charges ; H-07 (écart de prix facture fournisseur réparti entre stock restant et coût) |
| `ScenariosPilotage` | Scénarios 10, 16, 17, 19, 20 ; facture client PDF |
| `ScenarioSauvegarde` | Scénario 18 : sauvegarde vérifiée (instantané, restauration de contrôle, 21 totaux) puis restauration identique |
| `ContratClientWpf` | Chaque lecture des 15 écrans Windows (même URL, mêmes filtres, même type) ; menu selon les permissions ; parcours caisse, parcours commande → réception → facture fournisseur, avoir client et fiche employé modifiée sans accès aux salaires, avec les corps de requête du client |
| `ScenarioCharge` | 8 postes simultanés, rôles réels, 20 tours chacun : aucune erreur, aucune vente perdue ni doublée par les rejeux, stock d'article et caisse exacts, banque = somme des versements, numérotation continue (ventes, factures, postes, virements), contrôles de cohérence conformes, 95 % des appels sous 2 s |

Le client WPF est compilé sous Linux mais ne peut pas y être exécuté : sa recette visuelle se fait sur Windows. `ContratClientWpf` garantit en attendant que chaque écran lit des données réelles de l'API.


## Recette Windows

La chaîne GitHub Actions `.github/workflows/windows.yml` exécute les tests sous Windows, installe serveur et poste avec les scripts livrés, fait parcourir chaque écran par un robot et lance la sauvegarde planifiée puis une restauration. Compte rendu : `docs/recette-windows.md`.
