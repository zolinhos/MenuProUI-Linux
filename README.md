# MenuProUI

MenuProUI é um gerenciador de acessos (SSH, RDP e URLs) organizado por clientes.

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
sudo dpkg -i menupro-ui_1.0.3_amd64.deb
sudo apt-get install -f
```

Documentação
-------------
Veja `MANUAL.md` para instruções completas, formato CSV, caminhos de dados e troubleshooting.

Contribuição
------------
Abra issues ou PRs no repositório para melhorias no empacotamento, multi-arch ou documentação.
