#!/usr/bin/env bash
# Copie le code source (sans binaires) vers le dossier partagé du projet.
set -euo pipefail
cd "$(dirname "$0")/.."
mkdir -p /mnt/project-files/ssi/src
rsync -a --delete --exclude bin --exclude obj --exclude .vs --exclude '*.user' ./ /mnt/project-files/ssi/src/
