#!/bin/sh

set -e

PUBLISH_DIR="$1"
OUT_DIR="${2:-.}"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

if [ -z "${PUBLISH_DIR}" ] || [ ! -f "${PUBLISH_DIR}/KhDownloader" ]; then
    echo "Usage: $0 <path-to-published-linux-x64-dir> [output-dir]" >&2
    echo "(expected to find a 'KhDownloader' binary inside that directory)" >&2
    exit 1
fi

WORK_DIR="$(mktemp -d)"
trap 'rm -rf "${WORK_DIR}"' EXIT

APPDIR="${WORK_DIR}/AppDir"
mkdir -p "${APPDIR}/usr/bin"

cp "${PUBLISH_DIR}/KhDownloader" "${APPDIR}/usr/bin/KhDownloader"
chmod +x "${APPDIR}/usr/bin/KhDownloader"

cp "${SCRIPT_DIR}/AppRun" "${APPDIR}/AppRun"
chmod +x "${APPDIR}/AppRun"

cp "${SCRIPT_DIR}/khdownloader.desktop" "${APPDIR}/khdownloader.desktop"
cp "${SCRIPT_DIR}/khdownloader.svg" "${APPDIR}/khdownloader.svg"
ln -sf khdownloader.svg "${APPDIR}/.DirIcon"

APPIMAGETOOL_VERSION="1.9.1"

if [ -z "${APPIMAGETOOL:-}" ]; then
    APPIMAGETOOL="${WORK_DIR}/appimagetool"
    curl -fsSL -o "${APPIMAGETOOL}" \
        "https://github.com/AppImage/appimagetool/releases/download/${APPIMAGETOOL_VERSION}/appimagetool-x86_64.AppImage"
    chmod +x "${APPIMAGETOOL}"
fi

mkdir -p "${OUT_DIR}"

ARCH=x86_64 "${APPIMAGETOOL}" --appimage-extract-and-run \
    "${APPDIR}" "${OUT_DIR}/KhDownloader-x86_64.AppImage"

echo "Built ${OUT_DIR}/KhDownloader-x86_64.AppImage"
