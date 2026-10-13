# SSI — Installation, sauvegarde et restauration (Windows, réseau local)

Ce guide s'adresse à la personne qui installe SSI dans la station. Les scripts n'ouvrent **aucun accès depuis Internet** : l'API n'écoute que le sous-réseau local et PostgreSQL n'est joignable que depuis le serveur.

> **Statut des scripts.** Tous les scripts de ce dossier sont exécutés sur Windows Server à chaque envoi de code (chaîne `.github/workflows/windows.yml`, compte rendu dans `docs/recette-windows.md`) : installation du serveur et d'un poste, service, pare-feu, certificat, tâche planifiée, sauvegarde VERIFIEE avec 21 totaux identiques, restauration dans une nouvelle base. La première installation sur le matériel de la station reste à faire en suivant la recette du § 6.

## 1. Préparer les fichiers (poste de développement)

```powershell
dotnet publish src/Ssi.Api        -c Release -r win-x64 --self-contained -o deploy/publish/api
dotnet publish src/Ssi.Client.Wpf -c Release -r win-x64 --self-contained -o deploy/publish/client
```

Copiez ensuite le dossier `deploy` complet (scripts + `publish`) sur le serveur, par exemple sur une clé USB.

## 2. Serveur

Prérequis : Windows 10/11 Pro ou Windows Server, **PostgreSQL 16** (installateur officiel, service démarré), droits administrateur.

```powershell
cd D:\deploy
.\installer-serveur.ps1 -HeureSauvegarde "23:30"
```

Le script :

1. crée le rôle `ssi_app` et la base `ssi` (il refuse d'écraser une base existante) ;
2. génère les secrets (mot de passe base, clé de signature des jetons) et un certificat HTTPS ;
3. écrit `C:\SSI\Api\appsettings.Production.json`, lisible seulement par les administrateurs et le service ;
4. installe le service Windows **SSI API** (redémarrage automatique en cas d'arrêt) ;
5. ouvre le port 5443 au **seul sous-réseau local** et avertit si le port PostgreSQL 5432 est ouvert ;
6. crée le rôle `ssi_sauvegarde` et planifie la sauvegarde quotidienne vérifiée.

À la fin, il affiche le **mot de passe initial du compte `admin`** (une seule fois) et le chemin du certificat à distribuer aux postes.

Avant la mise en service, connectez-vous en `admin` et :

* renseignez la fiche société (raison sociale, ICE, IF, RC, adresse) ;
* faites **valider les taux de TVA par le comptable** (Administration › Paramètres et taxes ; les taux initiaux portent la mention « À VALIDER ») ;
* créez les cuves, volucompteurs, pistolets, produits, prix, comptes financiers, puis les utilisateurs nominatifs ;
* saisissez les stocks et soldes de départ (motif obligatoire).

## 3. Postes clients

Copiez le certificat `C:\SSI\Certificats\ssi-serveur.cer` du serveur, puis sur chaque poste (administrateur) :

```powershell
.\installer-poste.ps1 -Serveur NOM-DU-SERVEUR -Certificat .\ssi-serveur.cer
```

Le script approuve le certificat, copie le client dans `C:\Program Files\SSI`, règle l'adresse du serveur et crée le raccourci **SSI** sur le bureau. Il vérifie que le serveur répond.

## 4. Sauvegardes

La tâche planifiée exécute `C:\SSI\Scripts\sauvegarder.ps1` chaque jour. Une sauvegarde n'est déclarée **VERIFIEE** que si :

1. le fichier est produit par `pg_dump` sur un instantané cohérent ;
2. il est restauré dans une base temporaire `ssi_verif_*` ;
3. 21 totaux de contrôle (nombre de relevés, sommes des mouvements de stock et de trésorerie, factures, règlements…) sont identiques entre la base source, au même instant, et la base restaurée ;
4. son empreinte SHA-256 est enregistrée à côté du fichier.

Le résultat (VERIFIEE ou ECHEC) est inscrit dans la table `sauvegardes`, visible dans **Administration › Sauvegardes**. Le tableau de bord lève une alerte s'il n'y a aucune sauvegarde vérifiée depuis 26 heures.

**Copie hors du serveur :** indiquez un disque externe ou un partage réseau pour qu'une copie soit faite après vérification :

```powershell
.\planifier-sauvegarde.ps1 -MotDePasse <mot de passe ssi_sauvegarde> -CopieExterne "E:\SSI-Sauvegardes"
```

Une sauvegarde qui reste sur le seul disque du serveur ne protège pas contre la perte de ce disque.

## 5. Restauration

La restauration ne remplace **jamais** la base en service : elle crée une nouvelle base, vérifie l'empreinte et les totaux, puis seulement sur demande bascule le service.

```powershell
# 1. Restaurer et contrôler dans une nouvelle base
.\restaurer.ps1 -Fichier "C:\SSI\Sauvegardes\ssi-ssi-20261009-233000.dump" -Cible ssi_restauree
# 2. Après vérification, basculer l'API sur cette base (totaux recontrôlés, configuration sauvegardée, service redémarré)
.\restaurer.ps1 -Fichier "C:\SSI\Sauvegardes\ssi-ssi-20261009-233000.dump" -Cible ssi_restauree -Basculer
```

L'ancienne base est conservée tant qu'un administrateur ne la supprime pas.

## 6. Recette d'installation (à cocher sur le poste de test)

- [ ] `installer-serveur.ps1` se termine sans erreur ; `https://localhost:5443/health` répond.
- [ ] Depuis un poste client, connexion `admin` et changement du mot de passe initial.
- [ ] Depuis une machine hors du sous-réseau, le port 5443 est refusé.
- [ ] La tâche planifiée « SSI - Sauvegarde » s'exécute manuellement ; une ligne VERIFIEE apparaît dans Administration › Sauvegardes.
- [ ] Un fichier de sauvegarde altéré (un octet modifié) est refusé par `restaurer.ps1` (empreinte différente).
- [ ] `restaurer.ps1` crée la base cible et affiche des totaux identiques.
- [ ] Arrêt brutal du serveur pendant la saisie d'un poste : après redémarrage, aucune opération n'est en double (les validations portent une clé d'idempotence).
