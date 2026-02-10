using System;
using System.IO;

namespace MenuProUI.Services;

public static class AppPaths
{
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

    public static string ClientsPath => Path.Combine(AppDir, "clientes.csv");
    public static string AccessesPath => Path.Combine(AppDir, "acessos.csv");

    // legado (da versão antiga)
    public static string LegacyAccessesPath => Path.Combine(AppDir, "acessos_legacy.csv");
}
