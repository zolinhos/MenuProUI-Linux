#!/usr/bin/env bash
set -euo pipefail

APP_NAME="MenuProUI"
PKG_NAME="menupro-ui"
VERSION="1.0.3"
ARCH="amd64"

RUNTIME="linux-x64"
OUTDIR="publish/${RUNTIME}"

INSTALL_DIR="/opt/menuproui"
BIN_LINK="/usr/bin/menuproui"

STAGE="${PKG_NAME}_${VERSION}_${ARCH}"

echo "[1/6] Publish (self-contained) ..."
dotnet publish -c Release -r "${RUNTIME}" --self-contained true \
  -o "${OUTDIR}" \
  -p:PublishSingleFile=false \
  -p:PublishTrimmed=false

echo "[2/6] Montando estrutura do pacote ..."
rm -rf "${STAGE}"
mkdir -p "${STAGE}/DEBIAN"
mkdir -p "${STAGE}${INSTALL_DIR}"
mkdir -p "${STAGE}/usr/bin"
mkdir -p "${STAGE}/usr/share/applications"
mkdir -p "${STAGE}/usr/share/icons/hicolor/256x256/apps"
mkdir -p "${STAGE}/usr/share/pixmaps"

echo "[3/6] Copiando arquivos do app ..."
cp -a "${OUTDIR}/." "${STAGE}${INSTALL_DIR}/"

# Wrapper em /usr/bin
cat > "${STAGE}${BIN_LINK}" <<'WRAP'
#!/usr/bin/env bash
exec /opt/menuproui/MenuProUI "$@"
WRAP
chmod 0755 "${STAGE}${BIN_LINK}"

# Desktop entry
cat > "${STAGE}/usr/share/applications/menupro-ui.desktop" <<'DESK'
[Desktop Entry]
Type=Application
Name=MenuProUI
Comment=Gerenciador de acessos SSH/RDP/URLs (sem armazenar senhas)
Exec=/usr/bin/menuproui
Icon=menupro-ui
Terminal=false
Categories=Utility;Network;
DESK
chmod 0644 "${STAGE}/usr/share/applications/menupro-ui.desktop"

# Ícone (tema + pixmaps fallback)
if [ -f "Assets/icon-256.png" ]; then
  cp "Assets/icon-256.png" "${STAGE}/usr/share/icons/hicolor/256x256/apps/menupro-ui.png"
  cp "Assets/icon-256.png" "${STAGE}/usr/share/pixmaps/menupro-ui.png"
else
  echo "ERRO: Assets/icon-256.png não encontrado. O menu ficará sem ícone."
  exit 1
fi

chmod 0644 "${STAGE}/usr/share/icons/hicolor/256x256/apps/menupro-ui.png"
chmod 0644 "${STAGE}/usr/share/pixmaps/menupro-ui.png"

echo "[4/6] Control file (metadados + dependências) ..."
cat > "${STAGE}/DEBIAN/control" <<CTRL
Package: ${PKG_NAME}
Version: ${VERSION}
Section: utils
Priority: optional
Architecture: ${ARCH}
Maintainer: Adriano Dias de Jesus <adriano@voceconfia.com.br>
Depends: xdg-utils, openssh-client, freerdp2-x11 | freerdp3-x11 | freerdp-x11
Description: MenuProUI - gerenciador de acessos SSH/RDP/URLs
 Aplicação Avalonia para organizar acessos por cliente, com exportação em CSV.
 Não armazena senhas.
CTRL
chmod 0644 "${STAGE}/DEBIAN/control"

cat > "${STAGE}/DEBIAN/postinst" <<'POST'
#!/usr/bin/env bash
set -e
command -v update-desktop-database >/dev/null 2>&1 && update-desktop-database -q || true
command -v gtk-update-icon-cache >/dev/null 2>&1 && gtk-update-icon-cache -f -q /usr/share/icons/hicolor || true
command -v kbuildsycoca5 >/dev/null 2>&1 && kbuildsycoca5 --noincremental >/dev/null 2>&1 || true
exit 0
POST
chmod 0755 "${STAGE}/DEBIAN/postinst"

cat > "${STAGE}/DEBIAN/postrm" <<'POSTRM'
#!/usr/bin/env bash
set -e
command -v update-desktop-database >/dev/null 2>&1 && update-desktop-database -q || true
command -v gtk-update-icon-cache >/dev/null 2>&1 && gtk-update-icon-cache -f -q /usr/share/icons/hicolor || true
command -v kbuildsycoca5 >/dev/null 2>&1 && kbuildsycoca5 --noincremental >/dev/null 2>&1 || true
exit 0
POSTRM
chmod 0755 "${STAGE}/DEBIAN/postrm"

echo "[5/6] Build do .deb ..."
dpkg-deb --build "${STAGE}"

echo
echo "OK: Gerado -> ${STAGE}.deb"
