#!/usr/bin/env bash
set -euo pipefail

PROJECT_DIR="$(cd "$(dirname "$0")/.." && pwd)"

echo "[1/6] Verificando Homebrew..."
has_brew=true
if ! command -v brew >/dev/null 2>&1; then
  has_brew=false
  echo "Homebrew não encontrado. Vou usar instalador oficial do dotnet (sem brew)."
fi

echo "[2/6] Verificando dotnet..."
if ! command -v dotnet >/dev/null 2>&1; then
  if [[ "$has_brew" == true ]]; then
    echo "dotnet não encontrado no PATH. Instalando dotnet-sdk via Homebrew..."
    brew install --cask dotnet-sdk
  else
    echo "dotnet não encontrado. Instalando via script oficial em ~/.dotnet ..."
    tmp_script="/tmp/dotnet-install.sh"
    curl -fsSL https://dot.net/v1/dotnet-install.sh -o "$tmp_script"
    chmod +x "$tmp_script"
    "$tmp_script" --channel 10.0 --install-dir "$HOME/.dotnet"
  fi
else
  echo "dotnet já está instalado."
fi

# Tenta enriquecer PATH da sessão atual para cenários onde o cask foi instalado sem refresh do shell
export PATH="$HOME/.dotnet:/usr/local/share/dotnet:/opt/homebrew/share/dotnet:$PATH"

echo "[3/6] Validando SDK..."
if ! command -v dotnet >/dev/null 2>&1; then
  echo "dotnet ainda não está disponível no PATH desta sessão."
  echo "Abra um novo terminal e rode novamente este script."
  exit 1
fi

dotnet --info

echo "[4/6] Build do projeto..."
cd "$PROJECT_DIR"
dotnet build

echo "[5/6] Publish Linux x64 (self-contained)..."
dotnet publish MenuProUI.csproj -c Release -r linux-x64 --self-contained true -o publish/linux-x64

echo "[6/6] Concluído com sucesso."
echo "Artefatos gerados em: $PROJECT_DIR/publish/linux-x64"
