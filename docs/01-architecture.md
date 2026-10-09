# SSI — 01. Architecture détaillée

Version 1.0 — 09/10/2026 — statut : référence de développement (révisable)

## 1. Vue d'ensemble

SSI est une application de gestion client-serveur sur réseau local :

```
┌──────────────────────── Réseau local de la station (LAN) ────────────────────────┐
│                                                                                    │
│  Poste client 1..8 (Windows 10/11)              Serveur central (Windows Server   │
│  ┌──────────────────────────┐   HTTPS (TLS)     ou Windows 10/11 Pro + onduleur)  │
│  │ SSI Client (WPF .NET 10) │ ───────────────▶  ┌──────────────────────────────┐  │
│  │  - MVVM, vues par module │   port 5443/TCP   │ SSI API (ASP.NET Core 10)    │  │
│  │  - client HTTP typé      │ ◀───────────────  │  service Windows (Kestrel)   │  │
│  │  - aucun accès direct BD │   JSON + JWT      │  - authentification JWT      │  │
│  └──────────────────────────┘                   │  - permissions, audit        │  │
│                                                 │  - règles métier, transactions│  │
│                                                 └──────────────┬───────────────┘  │
│                                                                │ localhost:5432    │
│                                                 ┌──────────────▼───────────────┐  │
│                                                 │ PostgreSQL 16+               │  │
│                                                 │ (écoute 127.0.0.1 seulement) │  │
│                                                 └──────────────┬───────────────┘  │
│                                                                │ pg_dump planifié  │
│                                                 ┌──────────────▼───────────────┐  │
│                                                 │ Sauvegardes : disque local + │  │
│                                                 │ support distinct (NAS/USB)   │  │
│                                                 └──────────────────────────────┘  │
└────────────────────────────────────────────────────────────────────────────────────┘
```

Principes :

* Les postes clients ne parlent **qu'à l'API**. PostgreSQL n'écoute que sur `127.0.0.1` du serveur ; le port 5432 n'est jamais ouvert sur le pare-feu.
* Toute règle de calcul vit côté serveur (couche Domaine/Application). Le client WPF n'affiche que ce que l'API renvoie : un total affiché et un export utilisent la même requête.
* Aucun besoin d'Internet en exploitation courante.

## 2. Choix technologiques

| Couche | Technologie | Justification |
|---|---|---|
| Runtime | .NET 10 (LTS, support jusqu'à nov. 2028) | .NET 8 arrive en fin de support en novembre 2026 ; .NET 10 est la LTS courante |
| Client | WPF (.NET 10), CommunityToolkit.Mvvm | Application Windows native, MVVM testable |
| Graphiques | LiveChartsCore.SkiaSharpView.WPF | Bibliothèque WPF maintenue, open source (MIT) |
| API | ASP.NET Core 10 Web API, contrôleurs | Standard, hébergeable en service Windows |
| Données | PostgreSQL 16+, EF Core 10 + Npgsql | Transactions ACID, types `numeric`, contraintes d'exclusion |
| Auth | JWT porteur signé (HMAC-SHA256), mots de passe PBKDF2 (`PasswordHasher`) | Pas de dépendance à un annuaire |
| Logs | Serilog (fichiers JSON compacts, rotation quotidienne, 30 jours) | Logs structurés |
| PDF | QuestPDF (licence Community, gratuite sous 1 M$ de CA annuel — à confirmer pour la société) | Génération de documents |
| Excel | ClosedXML (MIT) ; CSV natif | Exports |
| Tests | xUnit, base PostgreSQL réelle pour les tests d'intégration | Les tests de concurrence exigent le vrai moteur |
| Installation | Script PowerShell serveur + installateur client (WiX Toolset v5 ou Inno Setup) | Voir phase 7 |

## 3. Découpage de la solution

```
Ssi.sln
├── src/
│   ├── Ssi.Domain          Entités, énumérations, calculs purs (aucune dépendance)
│   ├── Ssi.Application     Cas d'usage (services), validation, permissions, audit, DTO internes
│   ├── Ssi.Infrastructure  EF Core (DbContext, configurations, migrations, triggers SQL),
│   │                       hachage mots de passe, numérotation, sauvegarde
│   ├── Ssi.Contracts       DTO échangés entre API et client (partagés)
│   ├── Ssi.Api             Contrôleurs REST, JWT, politiques de permission, Serilog, ProblemDetails
│   └── Ssi.Client.Wpf      Application Windows (MVVM)
├── tests/
│   ├── Ssi.Domain.Tests        Règles de calcul (unitaires, sans base)
│   └── Ssi.IntegrationTests    Scénarios d'acceptation via l'API sur PostgreSQL réel
├── deploy/                 Scripts serveur : installation, pare-feu, sauvegarde, restauration
└── docs/                   Livrables d'analyse et documentation
```

Règle de dépendance : `Domain ← Application ← Infrastructure ← Api` ; `Contracts` est référencé par `Api` et `Client.Wpf`. Le client ne référence jamais `Domain` ni `Infrastructure`.

## 4. Représentation des nombres

| Grandeur | Type C# | Type PostgreSQL | Règle |
|---|---|---|---|
| Montants (DH) | `decimal` | `numeric(18,2)` | arrondi commercial `MidpointRounding.AwayFromZero` à 2 décimales, appliqué **par ligne** |
| Prix unitaires | `decimal` | `numeric(12,4)` | jamais arrondis avant multiplication |
| Volumes (L) | `decimal` | `numeric(14,3)` | pas d'arrondi intermédiaire |
| Index volucompteur | `decimal` | `numeric(15,3)` | stockés tels que saisis (jusqu'à 3 décimales) |
| Taux (TVA, %) | `decimal` | `numeric(7,4)` | 10 % = `0.1000` |
| Coût moyen pondéré | `decimal` | `numeric(14,6)` | précision étendue pour éviter la dérive |

Aucun `float`/`double` n'est utilisé dans les calculs métier.

## 5. Dates et fuseau

* Stockage : `timestamptz` (UTC) pour les horodatages ; `date` pour les dates métier (date de facture, journée d'exploitation).
* Affichage et « journée » métier : fuseau `Africa/Casablanca`, format `jj/mm/aaaa`.

## 6. Intégrité, concurrence et transactions

| Risque | Mécanisme |
|---|---|
| Transaction partielle | Chaque commande métier (validation, clôture, règlement…) s'exécute dans **une** transaction `ReadCommitted` ; échec ⇒ rollback complet |
| Double clic / renvoi après coupure | En-tête `Idempotency-Key` (UUID généré par le client) obligatoire sur les validations : la clé et la réponse sont stockées dans la même transaction ; un renvoi rejoue la réponse sans réexécuter |
| Deux utilisateurs valident la même opération | Jeton de concurrence optimiste (`xmin` PostgreSQL) + transition d'état conditionnelle (`Brouillon → Validé`) ; le second reçoit HTTP 409 avec un message clair |
| Mouvements de stock concurrents | Verrou de ligne (`SELECT … FOR UPDATE`) sur la ligne de position de stock du produit/cuve pendant le calcul du CMP |
| Numérotation des documents | Table `sequences_documents` verrouillée par ligne : numéros continus, sans doublon |
| Suppression silencieuse | Triggers PostgreSQL interdisant `UPDATE`/`DELETE` sur `journal_audit`, `mouvements_stock`, `mouvements_financiers` ; les documents validés ne passent qu'à l'état `Annulé` par opération compensatoire |
| Doublons métier | Contraintes uniques (facture fournisseur : fournisseur + numéro), contraintes d'exclusion (plages d'index d'un pistolet, périodes de prix) |

Les soldes (stock, caisse, banque, créances, dettes) ne sont **jamais** des champs saisissables : ils sont calculés à partir des mouvements. La table `positions_stock` est un cache recalculable (endpoint de contrôle `GET /api/controles/stock` qui compare cache et somme des mouvements).

## 7. Sécurité

* Authentification par identifiant individuel ; verrouillage après 5 échecs (15 min, paramétrable).
* Jeton JWT de 8 h (durée d'un poste), renouvelable ; révocation par désactivation du compte (vérifiée à chaque requête via un horodatage de sécurité).
* Autorisations : permissions atomiques `module.action` (consulter, creer, modifier, valider, annuler, exporter, sensible). Rôles = paquets de permissions ; chaque utilisateur peut recevoir des permissions individuelles accordées ou retirées.
* Les champs sensibles (salaires, marges, coûts d'achat) ne sont pas sérialisés pour un utilisateur sans la permission `*.sensible` correspondante (filtrage côté serveur, pas seulement masqué dans l'interface).
* TLS sur le LAN avec certificat auto-signé généré à l'installation et déployé sur les postes (hypothèse H-12).
* Pare-feu : seul le port de l'API (5443/TCP par défaut) est ouvert, limité au sous-réseau local. Bureau à distance désactivé par défaut.

## 8. Résilience réseau

* Client : détection des coupures (exception HTTP / délai de 15 s), bandeau « Serveur injoignable », reconnexion automatique avec back-off ; les saisies en cours restent dans le formulaire.
* Les commandes sensibles sont rejouables grâce à la clé d'idempotence : un renvoi après coupure ne crée jamais de double enregistrement.
* Point de santé `GET /health` (API + base).

## 9. Sauvegardes et reprise (objectifs)

| Objectif | Valeur proposée (hypothèse H-14) |
|---|---|
| RPO (perte de données maximale) | 1 heure en journée (sauvegarde horaire 7h–23h) + quotidienne complète |
| RTO (délai de rétablissement) | 4 heures sur serveur de remplacement préparé |
| Conservation | 14 quotidiennes, 8 hebdomadaires, 12 mensuelles |

Mécanisme : `pg_dump -Fc` planifié (tâche Windows), copie sur support distinct, empreinte SHA-256, **restauration de vérification automatique** dans une base temporaire suivie d'un contrôle de cohérence (nombre de lignes et totaux de contrôle). Une sauvegarde n'est marquée « vérifiée » que si cette restauration réussit.

## 10. Journalisation

Serilog → `logs/ssi-api-AAAAMMJJ.json` (rotation quotidienne, 30 fichiers). Chaque requête porte un identifiant de corrélation renvoyé au client et affiché dans les messages d'erreur pour faciliter le support.

## 11. API — conventions

* REST JSON, préfixe `/api`, dates ISO 8601.
* Erreurs au format ProblemDetails (RFC 9457) avec message en français : 400 (validation), 401, 403 (permission), 404, 409 (conflit / déjà validé), 422 (règle métier violée).
* Pagination `?page=&taille=`, tri `?tri=`, recherche `?q=`.
