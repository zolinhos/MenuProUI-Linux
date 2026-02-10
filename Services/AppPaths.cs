using System;
using System.IO;

namespace MenuProUI.Services;

/// <summary>
/// Gerencia os caminhos de arquivos e diretórios da aplicação.
/// Dados são armazenados em ~/.config/MenuProUI (AppData no Windows).
/// </summary>
public static class AppPaths
{
    /// <summary>
    /// Diretório de dados da aplicação. Cria o diretório automaticamente se não existir.
    /// Windows: %APPDATA%\MenuProUI
    /// Linux: ~/.config/MenuProUI
    /// </summary>
    public static string AppDir
    {
        get
        {
            var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dir = Path.Combine(baseDir, "MenuProUI");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    /// <summary>Caminho completo do arquivo CSV com a lista de clientes</summary>
    public static string ClientsPath => Path.Combine(AppDir, "clientes.csv");
    
    /// <summary>Caminho completo do arquivo CSV com a lista de acessos</summary>
    public static string AccessesPath => Path.Combine(AppDir, "acessos.csv");

    /// <summary>Caminho legado do arquivo de acessos (usado em versões antigas)</summary>
    public static string LegacyAccessesPath => Path.Combine(AppDir, "acessos_legacy.csv");
}
