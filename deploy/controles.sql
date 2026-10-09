-- Totaux de contrôle SSI : exécutés sur la base source et sur la base restaurée ; les deux sorties doivent être identiques.
-- Une ligne par contrôle : nom|valeur. Ordre stable.
SELECT 'clients|' || count(*) FROM clients
UNION ALL SELECT 'fournisseurs|' || count(*) FROM fournisseurs
UNION ALL SELECT 'produits|' || count(*) FROM produits
UNION ALL SELECT 'prix_produits|' || count(*) || '|' || coalesce(sum(prix), 0) FROM prix_produits
UNION ALL SELECT 'postes_travail|' || count(*) || '|' || coalesce(sum(montant_theorique_ttc), 0) FROM postes_travail
UNION ALL SELECT 'releves_index|' || count(*) || '|' || coalesce(sum(volume), 0) || '|' || coalesce(sum(montant_ttc), 0) FROM releves_index
UNION ALL SELECT 'reglements_poste|' || count(*) || '|' || coalesce(sum(montant), 0) FROM reglements_poste
UNION ALL SELECT 'mouvements_stock|' || count(*) || '|' || coalesce(sum(quantite), 0) || '|' || coalesce(sum(valeur), 0) || '|' || coalesce(max(id), 0) FROM mouvements_stock
UNION ALL SELECT 'positions_stock|' || count(*) || '|' || coalesce(sum(quantite), 0) || '|' || coalesce(sum(valeur), 0) FROM positions_stock
UNION ALL SELECT 'factures|' || count(*) || '|' || coalesce(sum(total_ttc), 0) FROM factures
UNION ALL SELECT 'lignes_facture|' || count(*) || '|' || coalesce(sum(montant_ttc), 0) FROM lignes_facture
UNION ALL SELECT 'ventes_articles|' || count(*) || '|' || coalesce(sum(total_ttc), 0) FROM ventes_articles
UNION ALL SELECT 'livraisons_directes|' || count(*) || '|' || coalesce(sum(quantite_livree), 0) FROM livraisons_directes
UNION ALL SELECT 'reglements|' || count(*) || '|' || coalesce(sum(montant), 0) FROM reglements
UNION ALL SELECT 'affectations_reglement|' || count(*) || '|' || coalesce(sum(montant), 0) FROM affectations_reglement
UNION ALL SELECT 'mouvements_financiers|' || count(*) || '|' || coalesce(sum(montant), 0) || '|' || coalesce(max(id), 0) FROM mouvements_financiers
UNION ALL SELECT 'sessions_caisse|' || count(*) FROM sessions_caisse
UNION ALL SELECT 'charges|' || count(*) || '|' || coalesce(sum(montant_ttc), 0) FROM charges
UNION ALL SELECT 'journal_audit|' || count(*) || '|' || coalesce(max(id), 0) FROM journal_audit
UNION ALL SELECT 'utilisateurs|' || count(*) FROM utilisateurs
UNION ALL SELECT 'sequences_documents|' || coalesce(sum(prochain_numero), 0) FROM sequences_documents;
