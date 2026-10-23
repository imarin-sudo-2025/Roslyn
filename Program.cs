using CatalogApp;
using Microsoft.Build.Locator;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;

namespace CatalogApp;

internal static class Program
{
    public static async Task<int> Main()
    {
        try
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .Build();

            var settings = new AppSettings();
            config.Bind(settings);

            if (settings.Solutions == null || settings.Solutions.Count == 0)
            {
                Console.Error.WriteLine("✗ No solutions configured in appsettings.json");
                return 1;
            }

            // Crear directorio de output si no existe
            if (!Directory.Exists(settings.Output.Directory))
            {
                Directory.CreateDirectory(settings.Output.Directory);
            }

            // Registrar MSBuild una sola vez al inicio
            MSBuildLocator.RegisterDefaults();

            foreach (var solution in settings.Solutions.Values)
            {
                Console.WriteLine($"=== Extrayendo métodos de {solution.Name} ===");
                await MethodExtractorRoslyn.ExtractAllMethods(solution.Path, solution.Prefix);
            }

            Console.WriteLine("\n=== Analizando árbol de ejecución ===");
            await MethodReader.ExtractAllEndPoints();

            Console.WriteLine("\n✓ Extracción completada");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"✗ Error: {ex.Message}");
            Console.Error.WriteLine($"{ex.StackTrace}");
            return 1;
        }
    }
}
