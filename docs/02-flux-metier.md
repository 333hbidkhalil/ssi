# SSI — 02. Flux métier

Les diagrammes sont au format Mermaid (lisibles dans GitHub, VS Code et la plupart des visionneuses Markdown).

## 1. Carte générale des flux

```mermaid
flowchart LR
    subgraph Station["Station (stock physique)"]
        CF1[Commande fournisseur<br/>destination cuve] --> REC[Réception<br/>chargé / reçu / accepté]
        REC -->|+ stock accepté| CUVE[(Cuves)]
        CUVE -->|− volume distribué| IDX[Relevés d'index<br/>par poste]
        IDX --> CLO[Clôture de poste]
        JAU[Jaugeage] -.compare.-> CUVE
        LUBA[Réception lubrifiants] -->|+ stock, CMP| MAG[(Magasin lubrifiants)]
        MAG -->|− CMP| LUBV[Vente lubrifiants]
    end

    subgraph Vrac["Livraison directe (hors stock)"]
        CC[Commande client vrac] --> CFV[Commande fournisseur liée]
        CFV --> MP{Marge prévisionnelle<br/>≥ seuil ?}
        MP -->|non| DER[Dérogation habilitée + motif]
        MP -->|oui| LD[Livraison directe<br/>chargé / livré / BL]
        DER --> LD
        LD --> FCV[Facture client]
        LD --> FFV[Facture fournisseur + frais transport]
        FCV & FFV --> MR[Marge réelle]
    end

    subgraph Finance
        CLO -->|espèces| CAISSE[(Caisse)]
        CLO -->|carte| TPE[(Attente carte TPE)]
        CLO -->|bons crédit| BONS[Bons non facturés]
        BONS --> FCB[Facture récapitulative<br/>régularisation, sans CA]
        FCV --> CRE[(Créances clients)]
        FCB --> CRE
        LUBV -->|crédit| CRE
        REC --> FFA[Facture fournisseur] --> DET[(Dettes fournisseurs)]
        FFV --> DET
        CHG[Charge à payer] --> DET
        CRE -->|règlement client| ENC[Encaissement] --> CAISSE & BANQUE[(Banques)]
        DET -->|règlement fournisseur| DEC[Décaissement]
        CAISSE & BANQUE --> DEC
        TPE -->|remise TPE − commission| BANQUE
        CAISSE -->|remise en banque<br/>virement interne| BANQUE
    end

    CLO & LUBV & FCV --> CA[Chiffre d'affaires]
    CA & REC & LD & CHG --> RENT[Rentabilité par activité]
```

Règle structurante : **seules trois sources créent du chiffre d'affaires** :

1. Les relevés d'index validés (carburant vendu en station) ;
2. Les ventes d'articles validées (lubrifiants, produits annexes) ;
3. Les factures clients de livraisons directes (vrac), diminuées des avoirs.

Les règlements, les bons de crédit à la pompe et la facture récapitulative de ces bons **ne créent jamais** de chiffre d'affaires : ils ventilent ou recouvrent un CA déjà constaté.

## 2. Poste de travail et relevés d'index (vente par index)

```mermaid
sequenceDiagram
    actor P as Pompiste / Resp. station
    participant API as SSI API
    participant DB as PostgreSQL
    P->>API: Ouvrir poste (date, responsable)
    API->>DB: poste OUVERT ; index de début proposés = index de fin précédents
    loop Pour chaque pistolet
        P->>API: Saisir index fin (+ opérateur)
        API->>API: Contrôles : fin ≥ début, continuité, doublon, volume inhabituel
        API->>DB: relevé SAISI (prix applicable figé)
    end
    P->>API: Saisir règlements du poste (espèces, carte, bons crédit, chèques…)
    P->>API: Clôturer le poste
    API->>API: Volumes, montants théoriques, écarts par mode, par opérateur
    API->>DB: poste CLÔTURÉ (rapport d'écarts)
    actor R as Responsable caisse
    R->>API: Valider la clôture (Idempotency-Key)
    API->>DB: TRANSACTION : relevés VALIDÉS ; mouvements de stock (− cuve) ;<br/>mouvements financiers (caisse, TPE, chèques) ; bons crédit enregistrés ;<br/>écart de poste tracé ; audit
    Note over API,DB: Réouverture = permission + motif, auditée, contre-passations
```

États d'un poste : `Ouvert → Clôturé → Validé` ; `Validé → Rouvert` (autorisé, motif) qui contre-passe les mouvements générés puis revient à `Clôturé`.

Exclusivité vente par index / transactions individuelles : pour le carburant, SSI v1 applique la **vente par index** (hypothèse H-01). Les transactions individuelles (bons de crédit, paiements carte) sont saisies comme **ventilation des règlements du poste**, rattachées au même volume. Elles ne peuvent pas créer de vente distincte.

## 3. Livraison directe en vrac

```mermaid
stateDiagram-v2
    [*] --> Brouillon : commande client
    Brouillon --> Confirmée : commande fournisseur liée\n+ marge prévue ≥ seuil (ou dérogation)
    Confirmée --> EnLivraison : 1re livraison enregistrée
    EnLivraison --> EnLivraison : livraisons partielles\n(un ou plusieurs fournisseurs)
    EnLivraison --> Livrée : volume livré ≥ volume commandé\nou clôture manuelle
    Livrée --> Facturée : facture client validée
    Facturée --> Soldée : facture client réglée\net factures fournisseurs payées
    Brouillon --> Annulée
    Confirmée --> Annulée : aucune livraison validée
```

Calcul de marge par livraison :

* **Prévue** (à la confirmation) : volume × (prix vente HT − prix achat HT) − frais prévus.
* **Réelle** : CA facturé HT net − (coût d'achat facturé HT + frais directs répartis HT). Statut `Provisoire` tant qu'il manque la facture fournisseur, la facture client ou un frais attendu ; les éléments manquants sont listés.

Aucune écriture de stock n'est générée à aucune étape.

## 4. Achat carburant pour les cuves

```mermaid
flowchart LR
    DA[Demande d'achat<br/>facultative] --> CF[Commande fournisseur] --> R[Réception]
    R --> CTL{Contrôle quantités<br/>chargé / reçu / accepté}
    CTL -->|écart > tolérance| JUS[Justification obligatoire]
    CTL --> V[Validation réception]
    JUS --> V
    V -->|+ volume ACCEPTÉ<br/>au coût HT| STK[(Mouvement stock cuve)]
    V --> FF[Facture fournisseur]
    FF --> CP{Contrôle prix et quantités<br/>vs réception}
    CP --> DET[(Dette fournisseur)]
    DET --> PAY[Paiement total / partiel]
```

Le coût d'entrée en stock est le prix de commande HT ; à la validation de la facture, l'écart de prix éventuel est enregistré comme ajustement de valeur (hypothèse H-07).

## 5. Caisse

```mermaid
stateDiagram-v2
    [*] --> Ouverte : fonds initial = solde théorique courant
    Ouverte --> Ouverte : encaissements / décaissements / dépenses / remises en banque
    Ouverte --> Clôturée : comptage physique saisi\nécart = compté − théorique
    Clôturée --> Validée : validation habilitée\n(écart tracé comme mouvement « écart de caisse »)
    Validée --> Rouverte : permission + motif (audit)
    Rouverte --> Clôturée
```

## 6. Règlements, créances et dettes

```mermaid
flowchart TB
    FC[Facture client validée] -->|crée| CR[Créance = TTC]
    AV[Avoir] -->|diminue| CR
    RC[Règlement client] -->|mouvement +| CPT[(Compte financier)]
    RC -->|affectation| CR
    FF[Facture fournisseur / charge à payer] -->|crée| DT[Dette = TTC]
    RF[Règlement fournisseur] -->|mouvement −| CPT
    RF -->|affectation| DT
    ANN[Annulation de règlement] -->|contre-passation| CPT
```

Créance d'une facture = TTC − Σ affectations − Σ avoirs imputés. Aucun champ « solde » n'est saisi.

## 7. Jaugeage et écart de stock

```mermaid
flowchart LR
    J[Jaugeage saisi<br/>volume mesuré, méthode, auteur] --> T[Stock théorique à l'instant]
    T --> E[Écart = mesuré − théorique<br/>L, %, valeur indicative CMP]
    E --> S{> seuil cuve ?}
    S -->|oui| AL[Alerte + historique récent]
    AL --> EX[Explication : mesure, température,<br/>saisie, perte réelle, inconnue]
    EX --> DR[Demande de régularisation]
    DR --> AP{Autorisation<br/>autre utilisateur habilité}
    AP -->|approuvée| MV[Mouvement d'ajustement tracé]
```

Le stock n'est jamais modifié automatiquement pour faire disparaître un écart.
