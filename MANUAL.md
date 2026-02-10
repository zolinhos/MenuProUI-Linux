MenuProUI - Manual do Usuário (MenuProUI-Linux)
=============================================

Visão geral
-----------

MenuProUI é um gerenciador GUI de acessos (SSH, RDP e URLs) organizado por clientes. Permite criar/editar/excluir clientes e acessos, exportar para CSV e abrir conexões usando ferramentas do sistema.

Requisitos
----------
- Para executar a partir do código-fonte: .NET 10 SDK/Runtime instalado (alvo do projeto: `net10.0`).
- Para o pacote `.deb` gerado pelo script: dependências listadas no `build-deb.sh` (ex.: `xdg-utils`, `openssh-client`, `freerdp2-x11`/`freerdp3-x11`/`freerdp-x11`).

Instalação a partir do pacote .deb
---------------------------------

1. Gere o `.deb` usando o script `build-deb.sh` na raiz do repositório. Exemplo:

```bash
chmod +x build-deb.sh
./build-deb.sh
```

2. O script publica o app (self-contained), monta a estrutura e gera um arquivo `${PKG_NAME}_${VERSION}_${ARCH}.deb` no diretório atual.

3. Instale o pacote gerado:

```bash
sudo dpkg -i menupro-ui_1.0.3_amd64.deb
sudo apt-get install -f   # para resolver dependências, se necessário
```

4. Após a instalação haverá um atalho no menu (desktop entry) e um wrapper em `/usr/bin/menuproui` que executa o binário em `/opt/menuproui`.

Instalação a partir do código-fonte (desenvolvimento)
----------------------------------------------------

1. Instale o .NET 10 SDK.
2. Publique em modo Release (exemplo para Linux x64):

```bash
dotnet publish -c Release -r linux-x64 --self-contained true -o publish/linux-x64
```

3. Execute localmente:

```bash
./publish/linux-x64/MenuProUI
```

Estrutura e caminhos de dados (backup e restauração)
---------------------------------------------------

A aplicação armazena os dados do usuário em um diretório de configuração (variável `AppPaths.AppDir`):

- Diretório: `Environment.SpecialFolder.ApplicationData` + `MenuProUI` (ex.: `~/.config/MenuProUI` ou `~/.local/share/MenuProUI`, dependendo do sistema).
- Arquivos:
  - `clientes.csv` — lista de clientes.
  - `acessos.csv` — lista de acessos vinculados a clientes.
  - `acessos_legacy.csv` — legado (padrão antigo); o app tenta migrar automaticamente quando detectado.

Para fazer backup rápido:

```bash
cp -a "$(xdg-user-dir CONFIG 2>/dev/null || echo ~/.config)/MenuProUI" ~/menuproui-backup-$(date +%F).tar.gz
```

Formato dos CSVs
---------------

Os modelos de dados estão em `Models/Client.cs` e `Models/AccessEntry.cs`.

- `clientes.csv` (colunas correspondentes a `Client`):
  - `Id` (GUID)
  - `Nome` (string)
  - `Observacoes` (string)
  - `CriadoEm` (DateTime)
  - `AtualizadoEm` (DateTime)

- `acessos.csv` (colunas correspondentes a `AccessEntry`):
  - `Id` (GUID)
  - `ClientId` (GUID vinculado a `clientes.csv`)
  - `Tipo` (enum: `SSH`, `RDP`, `URL`)
  - `Apelido` (string)
  - `Host` (string, opcional)
  - `Porta` (int?, opcional)
  - `Usuario` (string, opcional)
  - `Dominio` (string, opcional, RDP)
  - `RdpIgnoreCert` (bool)
  - `RdpFullScreen` (bool)
  - `RdpDynamicResolution` (bool)
  - `RdpWidth` / `RdpHeight` (int?, resolução fixa)
  - `Url` (string, opcional)
  - `Observacoes` (string)
  - `CriadoEm` / `AtualizadoEm` (DateTime)

O `CsvRepository` usa `CsvHelper` com cabeçalho (`HasHeaderRecord = true`). Ao editar CSVs manualmente, mantenha o cabeçalho e a ordem/nomes das propriedades.

Uso básico (GUI)
----------------

- Ao abrir o app, a coluna esquerda lista os `Clientes` e a coluna direita exibe os `Acessos` do cliente selecionado.
- Botões principais no topo (arquivo `Views/MainWindow.axaml`):
  - `Novo Cliente`, `Editar Cliente`, `Excluir Cliente` — gerenciam clientes.
  - `Novo Acesso`, `Editar Acesso`, `Excluir Acesso`, `Abrir` — gerenciam e iniciam acessos.
- Para abrir um acesso: selecione um acesso e clique em `Abrir` ou dê `DoubleTap`/duplo clique.

Comportamento de migração
-------------------------
Se existir apenas o antigo `acessos.csv` com coluna `Cliente` (legado), o app tenta migrar automaticamente para o novo modelo, criando `clientes.csv` e gravando um backup do legado como `acessos_legacy_backup_YYYYMMDD_HHMMSS.csv`.

Empacotamento (.deb) — detalhes do script `build-deb.sh`
-----------------------------------------------------

O script `build-deb.sh` realiza os passos abaixo:

1. `dotnet publish` em modo Release para `RUNTIME` (variável no início do script). Padrão: `linux-x64`.
2. Monta estrutura do pacote (`DEBIAN`, `/opt/menuproui`, `/usr/bin`, `usr/share/applications`, ícones, etc.).
3. Copia os artefatos publicados para `/opt/menuproui` dentro do pacote.
4. Cria um wrapper em `/usr/bin/menuproui` que executa `/opt/menuproui/MenuProUI`.
5. Gera o `desktop entry` (`/usr/share/applications/menupro-ui.desktop`) e instala ícone a partir de `Assets/icon-256.png`.
6. Escreve `DEBIAN/control` com as dependências e metadados.
7. Executa `dpkg-deb --build` para gerar o `.deb` final.

Você pode ajustar `APP_NAME`, `PKG_NAME`, `VERSION`, `ARCH` e `RUNTIME` no topo do script antes de rodá-lo.

Instalar e remover o pacote
---------------------------

Instalar:

```bash
sudo dpkg -i menupro-ui_1.0.3_amd64.deb
sudo apt-get install -f
```

Remover:

```bash
sudo dpkg -r menupro-ui
sudo rm -rf /opt/menuproui
```

Problemas conhecidos e troubleshooting
-------------------------------------

- Avisos Avalonia AVLN3001 durante `dotnet build`: aparecem quando um XAML não tem um construtor público na classe code-behind. Isso não impede o funcionamento do app, mas afeta carregamento via runtime loader. Verifique os arquivos em `Dialogs/*.axaml` e assegure-se que as classes code-behind tenham construtores públicos.
- Erro ao gerar `.deb`: o script exige `Assets/icon-256.png`. Se faltar, o script falha com mensagem de erro. Coloque um ícone nesse caminho ou modifique o script.
- Arquivos de dados não aparecem: verifique permissões e caminho `AppPaths.AppDir`. Ex.:

```bash
ls -la "$(xdg-user-dir CONFIG 2>/dev/null || echo ~/.config)/MenuProUI"
```

- Para depurar execução a partir do pacote, execute diretamente o binário em `/opt/menuproui/MenuProUI` em um terminal e observe saídas/erros.

Contribuição e contato
----------------------

Se quiser melhorar a embalagem ou documentação, abra uma issue ou PR no repositório. Mantainer indicado no `build-deb.sh`: Adriano Dias de Jesus <adriano@voceconfia.com.br>.

Licença
-------
Verifique o repositório para a licença aplicável (não adicionada aqui).

----

Arquivo criado automaticamente pelo assistente. Se quiser que eu ajuste este manual (ex.: traduções, mais comandos, exemplos de exportação CSV), diga o que prefere que eu acrescente.
