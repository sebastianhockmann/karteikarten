using System.Reflection;

namespace EnglishSteamTrainer.Core;

public static class AppVersion
{
    // Wird beim Release-Build aus dem Git-Tag gesetzt (scripts\Build-Package.ps1).
    public static string Current
    {
        get
        {
            var version = Assembly.GetEntryAssembly()?
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion ?? "0.0.0";

            var plus = version.IndexOf('+');
            return plus >= 0 ? version[..plus] : version;
        }
    }
}
