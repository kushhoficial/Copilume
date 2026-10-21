#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")"
SOURCE="$PWD/build/Copilume.app"
DESTINATION="${1:-$HOME/Applications/Copilume.app}"
if [[ ! -d "$SOURCE" ]]; then
  printf 'Execute ./build.sh antes de instalar.\n' >&2
  exit 1
fi
if [[ -e "$DESTINATION" ]]; then
  printf 'Destino já existe; nada foi substituído: %s\n' "$DESTINATION" >&2
  exit 1
fi
mkdir -p "$(dirname "$DESTINATION")"
/usr/bin/ditto "$SOURCE" "$DESTINATION"
/usr/bin/codesign --verify --strict "$DESTINATION"
printf 'Instalado: %s\n' "$DESTINATION"
