# MenuProUI v1.9.4

MenuProUI é um gerenciador de acessos (SSH, RDP e URLs) organizado por clientes.

## Funcionalidades

- ✨ Gerenciamento de clientes e acessos com interface intuitiva
- 🔍 Busca em tempo real para clientes e acessos
- ⌨️ 15 atalhos de teclado para máxima produtividade
- 💾 Persistência de dados em CSV (fácil backup e migração)
- 🚀 Lançamento direto de SSH, RDP e URLs
- 🌐 Normalização de URL com HTTPS padrão
- 📡 Checagem manual de conectividade (cliente atual ou todos)
- 🌐 URL com fallback de portas configuráveis (ex.: 443,80,8443,8080,9443)
- 🧪 Fallback de conectividade com `nmap` e `nc` (quando disponíveis)
- 🟢 Indicador visual por acesso (online/offline/checking/unknown)
- 🛡️ Indicador de integridade de auditoria na barra superior
- 📄 Clonagem rápida de acesso
- ⭐ Favoritos por acesso + contador de aberturas/último acesso
- 🧾 Auditoria com `eventos.csv` + integridade encadeada (`eventos.chain`)
- 📦 Exportação/importação de CSVs com backup e rollback
- ♻️ Restauração do último backup via Configurações
- ⚙️ Configurações avançadas: auto-check por seleção, debounce, teste/revalidação nmap, proteção CSV injection
- 🔎 Teste inline de URL no diálogo de acesso
- 📚 Sistema de ajuda integrado (F1)
- 🔗 Links para GitHub e suporte

Resumo rápido
-------------

- Executável principal: `MenuProUI` (publicado em `/opt/menuproui` quando empacotado).
- Wrapper: `/usr/bin/menuproui` (criado pelo pacote `.deb`).
- Dados do usuário: diretório de aplicação (`AppPaths.AppDir`) — por exemplo `~/.config/MenuProUI`.

Build e empacotamento
---------------------

Gere um pacote `.deb` usando o script `build-deb.sh` (na raiz do repositório).

Modo padrão (single-arch):

```bash
chmod +x build-deb.sh
./build-deb.sh
```

Modo multi-arch (constrói para várias arquiteturas suportadas):

```bash
./build-deb.sh --all
```

O modo `--all` gera pacotes para as combinações internas:

- `amd64` → `linux-x64`
- `arm64` → `linux-arm64`
- `arm` → `linux-arm`

Observações
-----------

- Para builds cross-arch, verifique se o SDK .NET suporta publish para as `runtimes` alvo no host de build.
- O script espera o ícone em `Assets/icon-256.png` (copiado para o pacote). Se faltar, o script abortará.

Instalação do .deb
------------------

```bash
sudo dpkg -i menupro-ui_1.9.4_amd64.deb
sudo apt-get install -f
```

O `.deb` declara dependências de runtime para resolução automática via APT (`xdg-utils`, `openssh-client`, `freerdp` e libs gráficas X11/GTK).
O instalador também executa uma checagem simples de comandos essenciais no `preinst`.

Atalhos de Teclado
------------------

| Atalho | Ação |
|--------|------|
| **F1** | Abrir Ajuda |
| **Escape** | Fechar diálogo |
| **Ctrl+Q** | Sair da aplicação |
| **Ctrl+R** | Recarregar dados |
| **Ctrl+F** | Buscar Clientes |
| **Ctrl+Shift+F** | Buscar Acessos |
| **Ctrl+L** | Limpar busca |
| **Ctrl+N** | Novo Cliente |
| **Ctrl+Shift+N** | Novo Acesso |
| **Ctrl+Shift+D** | Clonar Acesso |
| **Ctrl+Shift+K** | Checar Conectividade (escopo) |
| **Ctrl+E** | Editar Cliente |
| **Ctrl+Shift+E** | Editar Acesso |
| **Ctrl+Delete** | Excluir Cliente |
| **Ctrl+Shift+Delete** | Excluir Acesso |
| **Enter** | Lançar Acesso (SSH/RDP/URL) |
| **Ctrl+.** | Favoritar/Desfavoritar Acesso |
| **Ctrl+Shift+B** | Exportar CSVs (snapshot em `~/.config/MenuProUI/exports`) |
| **Ctrl+Shift+I** | Importar CSVs de `~/.config/MenuProUI/imports` |
| **Ctrl+Shift+J** | Abrir auditoria (últimos 200 eventos) |
| **Ctrl+Shift+S** | Abrir configurações de conectividade |

Auditoria:
- Filtros por ação, entidade e termo textual
- Ordenação por data/ação/entidade/nome
- Exportação do resultado filtrado em CSV e JSON

Conectividade:
- Timeout, concorrência e portas fallback configuráveis em Configurações
- Auto-check opcional ao selecionar acesso (com debounce configurável)
- Probes por `tcp`, `nmap` e `nc` (quando instalados no sistema)
- Resultado com diagnóstico resumido de falha (DNS, timeout, conexão recusada, host indisponível)

Documentação
-------------
Veja `MANUAL.md` para instruções completas, formato CSV, caminhos de dados e troubleshooting.

GitHub & Suporte
----------------

Para dúvidas, sugestões ou reportar problemas:

👉 https://github.com/zolinhos/MenuProUI-Linux

Contribuição
------------
Abra issues ou PRs no repositório para melhorias no empacotamento, multi-arch ou documentação.

Release (organização igual ao repositório Mac)
----------------------------------------------

Arquivos adicionados para padronizar publicação:

- `RELEASE_CHECKLIST.md`
- `.env.release.example`
- `scripts/release_preflight.sh`
- `scripts/release_publish_linux.sh`
- `scripts/publish_github_release.py`

Fluxo recomendado:

```bash
cp .env.release.example .env.release
bash scripts/release_publish_linux.sh 1.9.4
```

O script executa automaticamente:

- preflight de validação
- build do `.deb`
- criação/push da tag `v<versão>`
- criação/atualização da release no GitHub e upload do `.deb`
