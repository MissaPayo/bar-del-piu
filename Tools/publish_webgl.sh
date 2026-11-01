#!/usr/bin/env bash
# Publica Build/WebGL en la rama gh-pages (GitHub Pages).
# Uso (desde la raíz del proyecto, después de «Bar del Piu ▸ Publicar ▸ Build WebGL»):
#   bash Tools/publish_webgl.sh
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
BUILD="$ROOT/Build/WebGL"
REMOTE="$(git -C "$ROOT" remote get-url origin)"

if [ ! -f "$BUILD/index.html" ]; then
  echo "No hay build en $BUILD. Primero: Bar del Piu > Publicar > Build WebGL" >&2
  exit 1
fi

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT
cp -r "$BUILD"/. "$TMP"/
touch "$TMP/.nojekyll"   # Pages sirve los archivos tal cual (sin Jekyll)

cd "$TMP"
git init -q -b gh-pages
git add -A
git -c user.name="$(git -C "$ROOT" config user.name)" -c user.email="$(git -C "$ROOT" config user.email)" \
  commit -q -m "Publicar build WebGL ($(date '+%Y-%m-%d %H:%M'))"
git push -q --force "$REMOTE" gh-pages
echo "Publicado en la rama gh-pages de $REMOTE"
