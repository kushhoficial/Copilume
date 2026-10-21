#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")"
[[ -d build/Copilume.app ]] || { printf 'Execute ./build.sh primeiro.\n' >&2; exit 1; }
VERSION="$(cat VERSION)"
[[ "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || exit 1
APP_VERSION="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleShortVersionString' build/Copilume.app/Contents/Info.plist)"
[[ "$VERSION" == "$APP_VERSION" ]] || { printf "Recompile o aplicativo antes de empacotar.\n" >&2; exit 1; }
mkdir -p dist
STAGE="$(mktemp -d "${TMPDIR:-/tmp}/copilume-package.XXXXXX")"
trap 'rm -rf "$STAGE"' EXIT
/usr/bin/ditto build/Copilume.app "$STAGE/Copilume.app"
cp build/Copilume.app/Contents/Resources/Copilume.icns "$STAGE/.VolumeIcon.icns"
if SETFILE="$(xcrun --find SetFile 2>/dev/null)"; then "$SETFILE" -a C "$STAGE"; fi
ln -s /Applications "$STAGE/Applications"
cat > "$STAGE/COMECE AQUI.txt" <<'TEXT'
Copilume — por @kushhoficial
Gratuito • código aberto • tudo local

1. Arraste Copilume para a pasta Applications ao lado.
2. Ejete este disco e abra Copilume pela pasta Aplicativos.
3. Copie um texto ou uma imagem e pressione Option + V.
4. Para colar automaticamente, use Preferências → Autorizar colagem.
   A permissão é opcional; sem ela, escolha o texto e use Command + V.

Compatível com macOS 13+ em Apple Silicon e Intel.
Liquid Glass nativo em macOS 26+.

Este pacote possui assinatura local ad hoc, sem notarização Apple.
Se o macOS bloquear a abertura, consulte o README do projeto e prefira
recompilar a fonte. Não desative as proteções do macOS.

Para desinstalar: desative Iniciar ao entrar nas preferências,
saia pelo menu e mova o app para a Lixeira.
Licença MIT. Copyright (c) 2026 kushhoficial.
TEXT
cp LICENSE "$STAGE/LICENSE.txt"
touch "$STAGE/.metadata_never_index"
DMG="$PWD/dist/Copilume-${VERSION}-universal.dmg"
if [[ -e "$DMG" ]]; then
  printf 'O pacote já existe; mova-o antes de gerar novamente: %s\n' "$DMG" >&2
  exit 1
fi
hdiutil create -volname 'Copilume — Instalar' -srcfolder "$STAGE" -format UDZO -ov "$DMG"
hdiutil verify "$DMG"
(cd dist && shasum -a 256 Copilume-${VERSION}-universal.dmg > SHA256SUMS.txt)
printf 'Pacote: %s\n' "$DMG"
