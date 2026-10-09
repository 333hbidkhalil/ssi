#!/usr/bin/env bash
# Restauration SSI dans une NOUVELLE base (jamais par-dessus la base en service), puis contrôle de l'empreinte
# et des totaux enregistrés au moment de la sauvegarde. La bascule de l'API vers la base restaurée reste une décision manuelle.
# Usage : PGHOST=... PGUSER=... deploy/restaurer.sh <fichier.dump> <base_cible>
set -euo pipefail
FICHIER="${1:?fichier .dump}"
CIBLE="${2:?base cible (nouvelle)}"
ICI="$(cd "$(dirname "$0")" && pwd)"

if [[ -f "$FICHIER.sha256" ]] && [[ "$(sha256sum "$FICHIER" | cut -d' ' -f1)" != "$(cat "$FICHIER.sha256")" ]]; then
  echo "ÉCHEC : l'empreinte SHA-256 du fichier ne correspond pas (fichier altéré ou incomplet)." >&2; exit 2
fi
if psql -X -A -t -d postgres -c "SELECT 1 FROM pg_database WHERE datname = '$CIBLE'" | grep -q 1; then
  echo "ÉCHEC : la base $CIBLE existe déjà. Choisissez un nouveau nom : la restauration n'écrase jamais une base." >&2; exit 3
fi
createdb "$CIBLE"
pg_restore --no-owner --exit-on-error -d "$CIBLE" "$FICHIER"
if [[ -f "$FICHIER.controles.txt" ]]; then
  if [[ "$(psql -X -q -A -t -d "$CIBLE" -f "$ICI/controles.sql")" != "$(cat "$FICHIER.controles.txt")" ]]; then
    echo "ÉCHEC : totaux de contrôle différents de ceux de la sauvegarde." >&2; exit 4
  fi
  echo "Totaux de contrôle identiques à ceux de la sauvegarde."
fi
echo "Base $CIBLE restaurée. Pour l'utiliser, modifiez ConnectionStrings:Ssi puis redémarrez le service SSI API."
