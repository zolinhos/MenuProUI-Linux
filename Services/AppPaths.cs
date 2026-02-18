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

    /// <summary>Caminho completo do arquivo CSV de auditoria de eventos</summary>
    public static string EventsPath => Path.Combine(AppDir, "eventos.csv");

    /// <summary>Caminho completo do arquivo de integridade encadeada dos eventos</summary>
    public static string EventsChainPath => Path.Combine(AppDir, "eventos.chain");

    /// <summary>Diretório para snapshots de backup de importação/exportação</summary>
    public static string BackupsDir => EnsureDir(Path.Combine(AppDir, "backups"));

    /// <summary>Diretório padrão para exportação de CSVs</summary>
    public static string ExportDir => EnsureDir(Path.Combine(AppDir, "exports"));

    /// <summary>Diretório padrão para importação de CSVs</summary>
    public static string ImportDir => EnsureDir(Path.Combine(AppDir, "imports"));

    /// <summary>Caminho legado do arquivo de acessos (usado em versões antigas)</summary>
    public static string LegacyAccessesPath => Path.Combine(AppDir, "acessos_legacy.csv");

    private static string EnsureDir(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }
}
