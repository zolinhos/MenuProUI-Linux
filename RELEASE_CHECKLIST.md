# Release Checklist — MenuProUI-Linux

> Este checklist é validado automaticamente por `scripts/release_preflight.sh`
> e executado antes do publish em `scripts/release_publish_linux.sh`.

## 1) Validação local
- [ ] Rodar app e testar atalhos principais (`Enter`, `Ctrl+N`, `Ctrl+Shift+N`, `Ctrl+R`, `Ctrl+Shift+K`, `Ctrl+Shift+D`)
- [ ] Confirmar abertura de SSH, RDP e URL
- [ ] Confirmar coluna `Net` nos acessos e indicador agregado na lista de clientes

## 2) Build e artefatos
- [x] Build release: `dotnet build`
- [x] Publish linux-x64: `dotnet publish MenuProUI.csproj -c Release -r linux-x64 --self-contained true -o publish/linux-x64`
- [ ] `.deb` gerado: `menupro-ui_1.0.4_amd64.deb`

## 3) Publicação no GitHub
- [ ] Criar tag `v1.0.4`
- [ ] Criar release "MenuProUI-Linux 1.0.4"
- [ ] Anexar `.deb`
- [ ] Incluir changelog curto (conectividade, clonagem, alinhamentos)

## 4) Script de automação
- [ ] Copiar ambiente: `cp .env.release.example .env.release`
- [ ] Rodar script: `bash scripts/release_publish_linux.sh 1.0.4`
