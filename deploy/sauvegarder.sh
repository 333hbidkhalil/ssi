#!/usr/bin/env bash
# Sauvegarde SSI vérifiée (équivalent Linux de sauvegarder.ps1, utilisé aussi par les tests).
# 1. pg_dump au format personnalisé, dans un instantané exporté ;
# 2. totaux de contrôle calculés sur la base source DANS LE MÊME instantané ;
# 3. restauration dans une base temporaire, mêmes totaux, comparaison ;
# 4. empreinte SHA-256 ; inscription du résultat dans la table sauvegardes ; rotation.
# Une sauvegarde n'est déclarée « VERIFIEE » que si la restauration et la comparaison réussissent.
# Usage : PGHOST=... PGUSER=... [PGPASSWORD=...] deploy/sauvegarder.sh <base> <dossier> [jours_conservation]
set -euo pipefail

BASE="${1:?base de données}"
DOSSIER="${2:?dossier de sauvegarde}"
CONSERVATION="${3:-30}"
ICI="$(cd "$(dirname "$0")" && pwd)"
HORODATAGE="$(date -u +%Y%m%d-%H%M%S)"
FICHIER="$DOSSIER/ssi-$BASE-$HORODATAGE.dump"
VERIF="ssi_verif_$(date +%s)_$$"
mkdir -p "$DOSSIER"

journaliser() {   # statut message
  local taille=0 sha=""
  [[ -f "$FICHIER" ]] && taille=$(stat -c %s "$FICHIER") && sha=$(sha256sum "$FICHIER" | cut -d' ' -f1)
  psql -X -q -d "$BASE" -v ON_ERROR_STOP=1 \
    -v fichier="$FICHIER" -v taille="$taille" -v sha="$sha" -v statut="$1" -v message="$2" <<'SQL' || true
INSERT INTO sauvegardes (horodatage, fichier, taille, sha256, statut, message)
VALUES (now(), :'fichier', :taille, NULLIF(:'sha', ''), :'statut', :'message');
SQL
}

nettoyer() { dropdb --if-exists "$VERIF" 2>/dev/null || true; [[ -n "${COPROC_PID:-}" ]] && kill "$COPROC_PID" 2>/dev/null || true; }
trap nettoyer EXIT

# Session qui tient l'instantané ouvert pendant le dump et le calcul des totaux source.
coproc PSQL { psql -X -q -A -t -d "$BASE" 2>&1; }
echo "BEGIN ISOLATION LEVEL REPEATABLE READ READ ONLY; SELECT pg_export_snapshot();" >&"${PSQL[1]}"
read -r -t 30 SNAPSHOT <&"${PSQL[0]}" || { journaliser ECHEC "Impossible d'ouvrir un instantané"; echo "ÉCHEC : instantané" >&2; exit 2; }

if ! pg_dump -Fc --snapshot="$SNAPSHOT" -d "$BASE" -f "$FICHIER"; then
  journaliser ECHEC "pg_dump a échoué"; echo "ÉCHEC : pg_dump" >&2; exit 3
fi
FIN="__fin_controles_$$__"
{ cat "$ICI/controles.sql"; echo "SELECT '$FIN';"; } >&"${PSQL[1]}"
SOURCE=""
while read -r -t 120 ligne <&"${PSQL[0]}"; do [[ "$ligne" == "$FIN" ]] && break; SOURCE+="$ligne"$'\n'; done
echo "COMMIT;" >&"${PSQL[1]}"
echo "\\q" >&"${PSQL[1]}"

# Vérification : restauration complète dans une base temporaire
createdb "$VERIF"
if ! pg_restore --no-owner --exit-on-error -d "$VERIF" "$FICHIER" 2>"$DOSSIER/.restauration.log"; then
  journaliser ECHEC "Restauration de vérification impossible : $(head -c 300 "$DOSSIER/.restauration.log")"
  echo "ÉCHEC : la sauvegarde ne se restaure pas" >&2; exit 4
fi
RESTAUREE="$(psql -X -q -A -t -d "$VERIF" -f "$ICI/controles.sql")"$'\n'
if [[ "$SOURCE" != "$RESTAUREE" ]]; then
  diff <(echo "$SOURCE") <(echo "$RESTAUREE") > "$DOSSIER/.ecarts.log" || true
  journaliser ECHEC "Totaux de contrôle différents après restauration : $(head -c 300 "$DOSSIER/.ecarts.log")"
  echo "ÉCHEC : totaux de contrôle différents" >&2; exit 5
fi
printf "%s" "$SOURCE" > "$FICHIER.controles.txt"
sha256sum "$FICHIER" | cut -d' ' -f1 > "$FICHIER.sha256"
journaliser VERIFIEE "Restauration de contrôle réussie ; $(echo -n "$SOURCE" | grep -c '|') totaux identiques"

# Rotation : seules les sauvegardes vérifiées plus anciennes que la durée de conservation sont supprimées
find "$DOSSIER" -name "ssi-$BASE-*.dump*" -mtime +"$CONSERVATION" -delete
echo "Sauvegarde vérifiée : $FICHIER"
