# SSI — Recette sur Windows

Dernière exécution : 13/10/2026, machine Windows Server de GitHub Actions (`windows-latest`), PostgreSQL 17, PowerShell 7 pour l'installation, Windows PowerShell 5.1 pour la tâche planifiée (comme sur un serveur de station).

La chaîne `.github/workflows/windows.yml` se relance à chaque envoi sur `main` (ou à la demande depuis l'onglet *Actions* du dépôt). Ses résultats (captures de chaque écran, rapport, journaux d'installation et de sauvegarde) sont publiés sur la branche **`recette-windows`** du dépôt.

## Ce qui est exécuté

| Étape | Comment | Résultat |
|---|---|---|
| Tests automatisés | `dotnet test` sous Windows (hors scénario bash de sauvegarde, remplacé par l'essai réel ci-dessous) | tous verts |
| Installation du serveur | `deploy/installer-serveur.ps1` tel que livré : base et rôles, certificat, configuration protégée, service Windows, pare-feu limité au sous-réseau local, tâche de sauvegarde | réussie ; service démarré automatiquement ; règle de pare-feu `LocalSubnet` |
| Installation d'un poste | `deploy/installer-poste.ps1` : certificat approuvé, client copié, adresse du serveur, raccourci | réussie ; serveur joignable en HTTPS avec certificat vérifié |
| Recette du client | robot `tools/Ssi.RecetteWindows` : première connexion de l'administrateur et changement du mot de passe initial par l'écran, jeu de données (prix, cuves, pistolets, poste validé, vente d'article, client, fournisseur), puis ouverture de chaque entrée du menu et de chaque onglet avec actualisation, capture, relevé du bandeau d'erreur et des fenêtres d'erreur | 34 écrans et onglets sur 34 sans erreur |
| Sauvegarde planifiée | lancement de la tâche « SSI - Sauvegarde » (compte SYSTEM) | VERIFIEE : restauration de contrôle, 21 totaux identiques, inscrite en base |
| Restauration | `restaurer.ps1` sur le dernier fichier, dans une nouvelle base | totaux identiques à ceux de la sauvegarde |

## Défauts trouvés par cette recette et corrigés

1. **Trois écrans inaccessibles** : Achats et réceptions, Personnel et Paramétrage station n'avaient pas d'entrée dans le menu. Un test vérifie désormais que chaque écran déclaré figure au menu.
2. **Installation interrompue à la vérification finale** sous PowerShell 7 (certificat auto-signé refusé par `Invoke-WebRequest`).
3. **Accents corrompus dans les scripts** exécutés par Windows PowerShell 5.1 (fichiers sans BOM) ; un test l'empêche désormais.
4. **Sauvegarde planifiée en échec** quand une variable `PGPASSWORD` existe au niveau du système : elle masquait le fichier pgpass du compte de sauvegarde.
5. **Résultat de sauvegarde non inscrit en base** (message accentué refusé), ce qui aurait laissé l'alerte « aucune sauvegarde vérifiée » active alors que la sauvegarde était bonne. Chaque résultat est aussi écrit dans `C:\SSI\Sauvegardes\sauvegardes.log`, lisible même si la base est injoignable.
6. **Valeurs affichées sous leur nom technique** (« Especes », « AttenteCarte », « JaugeManuelle ») : libellés français partout.
7. **Séparateur des milliers ambigu** (« 14.932,00 ») : remplacé par une espace (« 14 932,00 »).
8. Grilles de saisie des lignes d'achat et de facture écrasées quand elles sont vides ; colonnes tronquées ; menu illisible par les lecteurs d'écran.

L'installateur s'arrête désormais sur toute commande SQL en échec, au lieu de continuer.

## Ce que cette recette ne remplace pas

* Un regard humain sur le poste réel de la station (taille d'écran, lisibilité, habitudes de saisie des pompistes et du caissier).
* Les temps de réponse sur le serveur et le réseau réels de la station.
* La copie externe des sauvegardes (disque USB ou autre poste), qui dépend du matériel de la station.
