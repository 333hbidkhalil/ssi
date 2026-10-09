#!/usr/bin/env bash
# Phase de développement uniquement (aucune base de production) : régénère les migrations
# « Initiale » (modèle EF) et « IntegriteMetier » (triggers et contraintes d'exclusion).
set -euo pipefail
cd "$(dirname "$0")/.."
export PATH="$PATH:$HOME/.dotnet/tools"
rm -rf src/Ssi.Infrastructure/Persistance/Migrations
dotnet ef migrations add Initiale -p src/Ssi.Infrastructure -s src/Ssi.Infrastructure -o Persistance/Migrations
dotnet ef migrations add IntegriteMetier -p src/Ssi.Infrastructure -s src/Ssi.Infrastructure -o Persistance/Migrations
cible=$(ls src/Ssi.Infrastructure/Persistance/Migrations/*_IntegriteMetier.cs)
cp scripts/IntegriteMetier.template.cs "$cible"
echo "Migrations régénérées."
