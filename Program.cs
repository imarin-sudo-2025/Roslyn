using CatalogApp;
using Microsoft.Build.Locator;
using System.Diagnostics;

namespace CatalogApp;

internal static class Program
{
    public static async Task<int> Main()
    {
        try
        {
            // Registrar MSBuild una sola vez al inicio
            MSBuildLocator.RegisterDefaults();

            Console.WriteLine("=== Extrayendo métodos de SolucionA (StockTool) ===");
            var solutionPathA = @"c:\TEST1\SolucionA\SolucionA.sln";
            await MethodExtractorRoslyn.ExtractAllMethods(solutionPathA, "_SolucionA");

            Console.WriteLine("\n=== Extrayendo métodos de SolucionB (MasterData) ===");
            var solutionPathB = @"c:\TEST1\SolucionB\SolucionB.sln";
            await MethodExtractorRoslyn.ExtractAllMethods(solutionPathB, "_SolucionB");

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
