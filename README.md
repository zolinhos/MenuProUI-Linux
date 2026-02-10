# MenuProUI v1.0.4

MenuProUI é um gerenciador de acessos (SSH, RDP e URLs) organizado por clientes.

## Funcionalidades

- ✨ Gerenciamento de clientes e acessos com interface intuitiva
- 🔍 Busca em tempo real para clientes e acessos
- ⌨️ 15 atalhos de teclado para máxima produtividade
- 💾 Persistência de dados em CSV (fácil backup e migração)
- 🚀 Lançamento direto de SSH, RDP e URLs
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
sudo dpkg -i menupro-ui_1.0.4_amd64.deb
sudo apt-get install -f
```

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
| **Ctrl+E** | Editar Cliente |
| **Ctrl+Shift+E** | Editar Acesso |
| **Ctrl+Delete** | Excluir Cliente |
| **Ctrl+Shift+Delete** | Excluir Acesso |
| **Enter** | Lançar Acesso (SSH/RDP/URL) |

Documentação
-------------
Veja `MANUAL.md` para instruções completas, formato CSV, caminhos de dados e troubleshooting.

GitHub & Suporte
----------------

Para dúvidas, sugestões ou reportar problemas:

👉 https://github.com/adrianotech/MenuProUI-Linux

Contribuição
------------
Abra issues ou PRs no repositório para melhorias no empacotamento, multi-arch ou documentação.
