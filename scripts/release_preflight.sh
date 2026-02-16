#!/usr/bin/env bash

set -euo pipefail

VERSION_INPUT="${1:-}"

if [[ -z "$VERSION_INPUT" ]]; then
  echo "Uso: bash scripts/release_preflight.sh <versao>"
  exit 1
fi

normalize_version() {
  local raw="$1"
  if [[ "$raw" == v* ]]; then
    echo "${raw#v}"
  else
    echo "$raw"
  fi
}

VERSION="$(normalize_version "$VERSION_INPUT")"

ok() {
  echo "[OK] $1"
}

fail() {
  echo "[ERRO] $1"
  exit 1
}

require_command() {
  if ! command -v "$1" >/dev/null 2>&1; then
    fail "Comando obrigatório não encontrado: $1"
  fi
}

require_file() {
  if [[ ! -f "$1" ]]; then
    fail "Arquivo obrigatório não encontrado: $1"
  fi
}

echo "==> Preflight release ${VERSION}"

require_command git
require_command dotnet
require_command python3
require_command bash
ok "Dependências de comando"

require_file "Views/MainWindow.axaml"
require_file "Views/MainWindow.axaml.cs"
require_file "README.md"
require_file "build-deb.sh"
require_file "scripts/publish_github_release.py"
ok "Arquivos essenciais"

if grep -q 'Ctrl+Shift+K' README.md; then
  ok "Atalho Ctrl+Shift+K documentado no README"
else
  fail "Atalho Ctrl+Shift+K não documentado no README"
fi

if grep -q 'OnCheckConnectivity' Views/MainWindow.axaml.cs && grep -q 'ConnectivityScopeDialog' Views/MainWindow.axaml.cs; then
  ok "Fluxo de escopo de conectividade presente"
else
  fail "Fluxo de escopo de conectividade não encontrado"
fi

if grep -q 'ConnectivityBadge' Views/MainWindow.axaml && grep -q 'ConnectivityBadge' Models/Client.cs; then
  ok "Indicadores de conectividade presentes (acesso e cliente)"
else
  fail "Indicadores de conectividade não encontrados"
fi

echo "==> Build de validação"
dotnet build >/dev/null
ok "Build release"

echo "==> Preflight concluído para ${VERSION}"
