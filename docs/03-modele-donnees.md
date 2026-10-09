# SSI — 03. Modèle relationnel de données

Conventions communes à toutes les tables métier :

* Clé primaire `id uuid` (générée côté application, UUID v7 ordonné dans le temps) ; les journaux append-only utilisent `bigint identity`.
* `cree_le`, `cree_par`, `modifie_le`, `modifie_par` ; pour les documents : `valide_le`, `valide_par`, `annule_le`, `annule_par`, `motif_annulation`.
* `xmin` PostgreSQL utilisé comme jeton de concurrence optimiste.
* Les documents ont un `numero` unique issu de `sequences_documents`.
* Types numériques : voir 01-architecture §4.

Légende : **PK** clé primaire, **FK** clé étrangère, **UQ** unique, **CK** contrainte de vérification, **EX** contrainte d'exclusion.

## 1. Sécurité et audit

| Table | Colonnes principales | Contraintes |
|---|---|---|
| `utilisateurs` | login, nom_complet, hash_mot_de_passe, actif, employe_id, doit_changer_mdp, echecs_connexion, verrouille_jusqu_a, horodatage_securite | UQ(lower(login)) ; FK employe |
| `roles` | code, libelle, systeme | UQ(code) |
| `permissions` | code (PK), module, libelle, sensible | — |
| `role_permissions` | role_id, permission_code | PK composite ; FK |
| `utilisateur_roles` | utilisateur_id, role_id | PK composite |
| `utilisateur_permissions` | utilisateur_id, permission_code, accorde (bool) | PK composite — accorde=false retire une permission du rôle |
| `journal_audit` | id bigint, horodatage, utilisateur_id, login, action, objet_type, objet_id, objet_ref, motif, avant jsonb, apres jsonb, adresse_ip | append-only (trigger) |
| `cles_idempotence` | cle, utilisateur_id, operation, code_statut, reponse jsonb, cree_le | PK(cle, utilisateur_id) |

## 2. Paramètres et société

| Table | Colonnes principales | Contraintes |
|---|---|---|
| `societe` | raison_sociale, forme, ice, identifiant_fiscal, rc, patente, cnss, adresse, ville, telephone, email, logo bytea, mentions_legales | une seule ligne |
| `parametres` | cle (PK), valeur, type, description | modification auditée |
| `taxes` | code, libelle | UQ(code) |
| `taux_taxes` | taxe_id, taux, date_debut, date_fin | EX(taxe_id =, daterange &&) |
| `sequences_documents` | type_document (PK), prefixe, annee, prochain_numero, longueur | verrou de ligne à l'attribution |
| `objectifs` | periode (date début mois), indicateur, activite, valeur | UQ(periode, indicateur, activite) |

## 3. Référentiels produits et prix

| Table | Colonnes principales | Contraintes |
|---|---|---|
| `familles_produits` | code, libelle, categorie (Carburant, Lubrifiant, Annexe) | UQ(code) |
| `produits` | code, designation, famille_id, unite, marque, conditionnement, taxe_id, methode_valorisation (CMP), seuil_alerte, type_carburant (Gasoil, SansPlomb, null), actif | UQ(code) ; FK famille, taxe |
| `prix_produits` | produit_id, type (Vente, AchatReference), prix_ttc / prix_ht, debut, fin, motif, valide_par | EX(produit_id =, type =, tstzrange(debut, fin) &&) |

## 4. Équipements, postes et index

| Table | Colonnes principales | Contraintes |
|---|---|---|
| `cuves` | code, produit_id, capacite, seuil_minimal, tolerance_ecart_litres, tolerance_ecart_pct, actif, observations | UQ(code) ; CK capacite > 0 |
| `volucompteurs` | code, libelle, actif | UQ(code) |
| `pistolets` | volucompteur_id, code, produit_id, cuve_id, index_maximum, cycle (n° de cycle du totalisateur), actif | UQ(volucompteur_id, code) ; CK produit(pistolet) = produit(cuve) contrôlé applicativement |
| `postes_travail` | numero, debut, fin, statut (Ouvert, Cloture, Valide), responsable_id (employé), ouvert_par, cloture_par, valide_par, ecart_total | UQ(numero) |
| `releves_index` | poste_id, pistolet_id, cycle, operateur_id, index_debut, index_fin, horodatage, prix_unitaire_ttc, prix_produit_id, taux_tva, volume, montant_ttc, montant_ht, statut (Saisi, Valide, Annule), anomalies, commentaire | UQ(poste_id, pistolet_id) hors annulés ; EX(pistolet_id =, cycle =, numrange(index_debut, index_fin) &&) hors annulés ; CK index_fin ≥ index_debut |
| `evenements_compteur` | pistolet_id, type (RemiseAZero, Remplacement, Correction), ancien_index, nouvel_index, horodatage, motif, demande_par, valide_par, statut | incrémente `pistolets.cycle` |
| `reglements_poste` | poste_id, mode (Especes, Carte, Cheque, Credit, Autre), montant, client_id, produit_id, volume, reference, operateur_id | CK client obligatoire si mode = Credit |

## 5. Stocks

| Table | Colonnes principales | Contraintes |
|---|---|---|
| `mouvements_stock` | id bigint, horodatage, produit_id, cuve_id (null = magasin), type, quantite (signée), cout_unitaire, valeur, quantite_apres, cmp_apres, document_type, document_id, motif, cree_par | append-only ; CK quantite ≠ 0 |
| `positions_stock` | produit_id, cuve_id, quantite, valeur, cmp, dernier_mouvement_id | PK(produit_id, cuve_id) — cache recalculable |
| `jaugeages` | cuve_id, horodatage, volume_mesure, hauteur_cm, temperature_c, methode, stock_theorique, ecart_litres, ecart_pct, valeur_indicative, statut, explication, cause, saisi_par | — |
| `ajustements_stock` | numero, produit_id, cuve_id, quantite, motif, cause, jaugeage_id, statut (Demande, Approuve, Rejete), demande_par, approuve_par, mouvement_id | CK approuve_par ≠ demande_par |

Types de mouvement : `StockInitial`, `Reception`, `Distribution`, `VenteArticle`, `RetourClient`, `RetourFournisseur`, `TransfertEntrant`, `TransfertSortant`, `Ajustement`, `ContrePassation`.

## 6. Tiers

| Table | Colonnes principales | Contraintes |
|---|---|---|
| `clients` | code, nom, type (Comptant, Credit), ice, identifiant_fiscal, rc, adresse, ville, telephone, email, contact, delai_paiement_jours, plafond_credit, actif | UQ(code) |
| `fournisseurs` | code, raison_sociale, ice, identifiant_fiscal, rc, adresse, ville, telephone, email, contact, delai_paiement_jours, conditions, actif | UQ(code) |
| `incidents_fournisseur` | fournisseur_id, date, type, description, document_ref | — |
| `relances` | client_id, facture_id, date, moyen, commentaire, par | — |
| `pieces_jointes` | objet_type, objet_id, nom_fichier, type_mime, taille, contenu bytea, ajoute_par | — |

## 7. Achats et ventes

| Table | Colonnes principales | Contraintes |
|---|---|---|
| `commandes_fournisseur` | numero, fournisseur_id, date, date_prevue, destination (Cuve, Magasin, LivraisonDirecte), commande_client_id, reference_fournisseur, conditions, frais_prevus_ht, statut | UQ(numero) |
| `lignes_commande_fournisseur` | commande_id, produit_id, cuve_id, quantite, prix_unitaire_ht | CK quantite > 0 |
| `receptions` | numero, commande_id, fournisseur_id, horodatage, numero_bl, transporteur, vehicule, statut | — |
| `lignes_reception` | reception_id, produit_id, cuve_id, quantite_commandee, quantite_chargee, quantite_recue, quantite_acceptee, prix_unitaire_ht, justification_ecart | CK acceptee ≤ recue |
| `ventes_articles` | numero, horodatage, client_id, poste_id, mode_reglement, compte_id, statut | — |
| `lignes_vente_article` | vente_id, produit_id, quantite, prix_unitaire_ttc, taux_tva, montant_ht, montant_tva, montant_ttc, cout_unitaire | — |
| `factures` | numero, sens (Client, Fournisseur), type (Facture, Avoir), origine (Vrac, BonsStation, Articles, AchatStock, AchatVrac, Transport), client_id, fournisseur_id, numero_tiers, date, echeance, total_ht, total_tva, total_ttc, facture_origine_id, statut | UQ(fournisseur_id, numero_tiers) hors annulées ; CK exactement un tiers |
| `lignes_facture` | facture_id, produit_id, designation, quantite, prix_unitaire_ht, taux_tva, montant_ht, montant_tva, montant_ttc, livraison_id, reception_id | — |

## 8. Livraisons directes (vrac)

| Table | Colonnes principales | Contraintes |
|---|---|---|
| `commandes_client` | numero, client_id, produit_id, volume, prix_vente_ht, date_prevue, lieu_livraison, conditions_reglement, transporteur, fournisseur_prevu_id, statut, marge_prevue_ht, derogation_par, derogation_motif | UQ(numero) |
| `livraisons_directes` | numero, commande_client_id, commande_fournisseur_id, fournisseur_id, client_id, produit_id, horodatage, quantite_commandee, quantite_chargee, quantite_livree, numero_bl_fournisseur, numero_reception_client, vehicule, transporteur, chauffeur, prix_achat_ht, prix_vente_ht, justification_ecart, reserves, statut | **aucun lien vers cuves ni mouvements_stock** |
| `frais_directs` | type (Transport, Autre), fournisseur_id, facture_id, date, montant_ht, methode_repartition (Volume, Manuel), libelle | — |
| `repartitions_frais` | frais_id, livraison_id, montant_ht | Σ = montant du frais |

## 9. Trésorerie

| Table | Colonnes principales | Contraintes |
|---|---|---|
| `comptes_financiers` | code, libelle, type (Caisse, Banque, AttenteCarte, ChequesAEncaisser), banque, rib, actif | UQ(code) |
| `mouvements_financiers` | id bigint, compte_id, date_operation, montant (signé), nature, libelle, reglement_id, transfert_id, charge_id, session_caisse_id, poste_id, document_ref, cree_par | append-only |
| `reglements` | numero, sens (Encaissement, Decaissement), client_id, fournisseur_id, beneficiaire, mode, compte_id, date, montant, reference, statut (Valide, Annule), reglement_contrepasse_id | CK montant > 0 |
| `affectations_reglement` | reglement_id, facture_id, charge_id, montant | CK exactement une cible ; Σ ≤ montant règlement |
| `transferts_internes` | numero, compte_source_id, compte_destination_id, montant, date, libelle | CK source ≠ destination |
| `sessions_caisse` | compte_id, numero, ouverture, solde_ouverture, cloture, solde_theorique, montant_compte, ecart, statut, commentaire, ouvert_par, cloture_par, valide_par | une seule session ouverte par caisse (index unique partiel) |
| `rapprochements_bancaires` | compte_id, date_releve, solde_releve, statut | — |
| `lignes_releve_bancaire` | rapprochement_id, date, libelle, montant, mouvement_id | UQ(mouvement_id) |

Natures de mouvement financier : `SoldeInitial`, `EncaissementVenteStation`, `EncaissementVenteArticle`, `EncaissementClient`, `PaiementFournisseur`, `PaiementCharge`, `TransfertInterne`, `FraisBancaires`, `EcartCaisse`, `ContrePassation`, `Autre`.

## 10. Charges, centres de coûts et personnel

| Table | Colonnes principales | Contraintes |
|---|---|---|
| `categories_charges` | code, libelle, fixe_par_defaut | UQ(code) |
| `centres_cout` | code, libelle, activite (null = commun), cle_repartition_id | UQ(code) |
| `cles_repartition` | code, libelle, methode (ChiffreAffaires, Volume, MargeBrute, CoutDirect, Manuelle) | — |
| `cles_repartition_manuelles` | cle_id, activite, pourcentage | Σ = 100 |
| `charges` | numero, date, fournisseur_id, beneficiaire, categorie_id, centre_cout_id, montant_ht, montant_tva, montant_ttc, fixe, echeance, description, statut (Validee, Annulee) | CK ht + tva = ttc |
| `employes` | matricule, nom, prenom, cin, fonction, date_embauche, affectation, salaire_base (sensible), telephone, actif | UQ(matricule) |
| `affectations_employe` | employe_id, fonction, affectation, debut, fin | — |
| `elements_remuneration` | employe_id, periode, type (Salaire, Prime, Avance, Retenue, ChargeSociale), montant, commentaire, charge_id | — |
| `absences` | employe_id, debut, fin, type, justifiee | — |

## 11. Contrôle de gestion

| Table | Colonnes principales |
|---|---|
| `simulations` | nom, parametres jsonb, cree_par — stockage des hypothèses uniquement, aucune écriture sur les opérations |
| `seuils_alertes` | code_alerte, actif, seuil, parametres jsonb |
| `sauvegardes` | horodatage, fichier, taille, sha256, statut (Reussie, Verifiee, Echec), message |

## 12. Activités de rentabilité

L'activité n'est pas saisie : elle est **dérivée** de la source :

| Activité | Source |
|---|---|
| `GasoilStation` | relevés validés dont produit.type_carburant = Gasoil |
| `SansPlombStation` | relevés validés dont produit.type_carburant = SansPlomb |
| `GasoilVrac` | factures client origine Vrac, produit Gasoil |
| `SansPlombVrac` | factures client origine Vrac, produit SansPlomb |
| `Lubrifiants` | ventes d'articles dont famille.categorie = Lubrifiant |
| `Autres` | ventes d'articles des autres familles |

Le dictionnaire de données détaillé (chaque colonne, type, nullabilité) est généré à partir des migrations EF Core (`docs/dictionnaire-donnees.md`).
