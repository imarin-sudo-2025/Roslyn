using CatalogApp.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CatalogApp;

public class MethodReader
{
    private static string _relativePath = "c:\\TEST1\\Catalog";
    private static List<string> _errors = new();
    private static HashSet<string> _visited = new();

    public static async Task ExtractAllEndPoints()
    {
        var output = @$"{_relativePath}\\output";
        var methodsPath_A = @$"{output}\\methods_SolucionA.json";
        var methodsPath_B = @$"{output}\\methods_SolucionB.json";

        Console.WriteLine("\n=== Analizando árbol de ejecución ===\n");

        // Procesar SolucionB (que tiene las llamadas a SolucionA)
        await ProcessSolution(methodsPath_B, methodsPath_A, output);
    }

    private static async Task ProcessSolution(string methodsPathFrontend, string methodsPathBackend, string output)
    {
        if (!File.Exists(methodsPathFrontend))
        {
            Console.WriteLine($"⚠️ No encontrado: {methodsPathFrontend}");
            return;
        }

        var allMethodsFrontend = JsonSerializer.Deserialize<List<MethodInfo>>(
            File.ReadAllText(methodsPathFrontend)) ?? new();

        var allMethodsBackend = new List<MethodInfo>();
        if (File.Exists(methodsPathBackend))
        {
            allMethodsBackend = JsonSerializer.Deserialize<List<MethodInfo>>(
                File.ReadAllText(methodsPathBackend)) ?? new();
        }

        // Buscar métodos públicos no-remote en frontend
        var publicMethods = allMethodsFrontend
            .Where(m => m.IsPublic && !m.IsRemoteWrapper && m.HttpVerb.Length > 0)
            .ToList();

        Console.WriteLine($"📊 Encontrados {publicMethods.Count} métodos públicos HTTP en frontend");

        foreach (var method in publicMethods)
        {
            _visited.Clear();
            _errors.Clear();

            Console.WriteLine($"\n📍 {method.Class}.{method.MethodName}()");

            var tree = BuildExecutionTree(allMethodsFrontend, allMethodsBackend, method, 1);

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            var lname = $"{method.Namespace}.{method.MethodName}_execution-tree";
            var outputFile = $"{output}\\{lname}.json";

            File.WriteAllText(outputFile, JsonSerializer.Serialize(tree, options));
            Console.WriteLine($"   ✓ Árbol guardado: {lname}.json");

            if (_errors.Any())
            {
                var errorsFile = $"{output}\\{lname}_errors.log";
                File.WriteAllLines(errorsFile, _errors);
                Console.WriteLine($"   ⚠️ {_errors.Count} errores registrados");
            }
        }
    }

    private static ExecutionTreeNode BuildExecutionTree(
        List<MethodInfo> allMethodsFrontend,
        List<MethodInfo> allMethodsBackend,
        MethodInfo method,
        int level)
    {
        var nodeId = $"{method.Namespace}.{method.Class}.{method.MethodName}";
        
        if (_visited.Contains(nodeId))
            return null;

        _visited.Add(nodeId);

        var node = new ExecutionTreeNode
        {
            Id = method.Id.ToString(),
            MethodName = method.MethodName,
            MethodClass = method.Class,
            MethodFile = method.File,
            Origin = "Frontend"
        };

        try
        {
            var startLine = Math.Max(1, method.LineInit);
            var endLine = method.LineEnd;
            if (File.Exists(method.File))
            {
                var code = File.ReadLines(method.File)
                    .Skip(startLine - 1)
                    .Take(Math.Max(0, endLine - startLine + 1))
                    .ToList();
                node.Code = string.Join(Environment.NewLine, code);
            }
        }
        catch { /* Archivo no disponible */ }

        // Cargar código de métodos backend
        if (method.LineInit > 0 && method.LineEnd > 0)
        {
            try
            {
                if (File.Exists(method.File))
                {
                    var lines = File.ReadAllLines(method.File);
                    var startIdx = Math.Max(0, method.LineInit - 1);
                    var endIdx = Math.Min(lines.Length - 1, method.LineEnd - 1);
                    if (startIdx <= endIdx)
                    {
                        var codeLines = lines.Skip(startIdx).Take(endIdx - startIdx + 1).ToList();
                        node.Code = string.Join(Environment.NewLine, codeLines);
                    }
                }
            }
            catch { /* Archivo no disponible */ }
        }

        if (level > 10) return node;

        // Procesar hijos
        if (method.Children?.Any() == true)
        {
            foreach (var child in method.Children)
            {
                var childNode = ResolveChild(allMethodsFrontend, allMethodsBackend, child, method, level + 1);
                if (childNode != null)
                {
                    node.Children.Add(childNode);
                }
            }
        }

        return node;
    }

    private static ExecutionTreeNode ResolveChild(
        List<MethodInfo> allMethodsFrontend,
        List<MethodInfo> allMethodsBackend,
        MethodInfoChild childInfo,
        MethodInfo parentMethod,
        int level)
    {
        // Si es un wrapper remoto, crear nodo intermedio + resolver al backend
        if (childInfo.IsRemoteWrapper)
        {
            var httpInfo = ExtractHttpInfo(childInfo);
            
            if (httpInfo != null)
            {
                var backendMatch = FindBackendMethod(allMethodsBackend, httpInfo.Value.Verb, httpInfo.Value.Path);
                
                if (backendMatch != null)
                {
                    Console.WriteLine($"   └─ {childInfo.ChildMethodName}() [Wrapper]");
                    Console.WriteLine($"      └─ {backendMatch.Class}.{backendMatch.MethodName}() [Backend/{httpInfo.Value.Verb} {httpInfo.Value.Path}]");
                    
                    // Cargar código del método backend
                    var backendCode = string.Empty;
                    try
                    {
                        if (File.Exists(backendMatch.File) && backendMatch.LineInit > 0 && backendMatch.LineEnd > 0)
                        {
                            var lines = File.ReadAllLines(backendMatch.File);
                            var startIdx = Math.Max(0, backendMatch.LineInit - 1);
                            var endIdx = Math.Min(lines.Length - 1, backendMatch.LineEnd - 1);
                            if (startIdx <= endIdx)
                            {
                                var codeLines = lines.Skip(startIdx).Take(endIdx - startIdx + 1).ToList();
                                backendCode = string.Join(Environment.NewLine, codeLines);
                            }
                        }
                    }
                    catch { /* Archivo no disponible */ }
                    
                    var backendNode = new ExecutionTreeNode
                    {
                        Id = backendMatch.Id.ToString(),
                        MethodName = backendMatch.MethodName,
                        MethodClass = backendMatch.Class,
                        MethodFile = backendMatch.File,
                        Code = backendCode,
                        Origin = "Backend",
                        CrossSolution = new CrossSolutionLink
                        {
                            HttpVerb = httpInfo.Value.Verb,
                            RequestedPath = httpInfo.Value.Path
                        }
                    };

                    // Crear nodo wrapper intermedio con información completa
                    var wrapperFullInfo = FindMatchingMethod(allMethodsFrontend, childInfo);
                    
                    var wrapperChildren = new List<ExecutionTreeNode>();
                    
                    // Buscar si el wrapper tiene una invocación a sí mismo con CancellationToken
                    if (wrapperFullInfo != null)
                    {
                        var asyncOverload = wrapperFullInfo.Children
                            .FirstOrDefault(c => 
                                c.ChildMethodName == childInfo.ChildMethodName &&
                                c.Parameters.Any(p => p.Contains("CancellationToken")));
                        
                        if (asyncOverload != null)
                        {
                            // Buscar el método del overload filtrando por tipo de parámetro (CancellationToken)
                            var asyncOverloadMethod = allMethodsFrontend
                                .FirstOrDefault(m =>
                                    m.MethodName.Equals(asyncOverload.ChildMethodName, StringComparison.OrdinalIgnoreCase) &&
                                    m.Class.Equals(asyncOverload.ClassName, StringComparison.OrdinalIgnoreCase) &&
                                    m.Namespace.Contains(asyncOverload.ContainingNamespace, StringComparison.OrdinalIgnoreCase) &&
                                    m.Parameters.Count == asyncOverload.Parameters.Count &&
                                    m.Parameters.Any(p => p.Contains("CancellationToken")));
                            
                            if (asyncOverloadMethod != null)
                            {
                                var asyncOverloadNode = new ExecutionTreeNode
                                {
                                    Id = asyncOverloadMethod.Id.ToString(),
                                    MethodName = asyncOverload.ChildMethodName,
                                    MethodClass = asyncOverload.ClassName,
                                    MethodFile = asyncOverloadMethod.File,
                                    Code = ExtractMethodCode(asyncOverloadMethod),
                                    Origin = "RemoteWrapper",
                                    Children = new List<ExecutionTreeNode> { backendNode }
                                };
                                wrapperChildren.Add(asyncOverloadNode);
                            }
                            else
                            {
                                wrapperChildren.Add(backendNode);
                            }
                        }
                        else
                        {
                            wrapperChildren.Add(backendNode);
                        }
                    }
                    else
                    {
                        wrapperChildren.Add(backendNode);
                    }
                    
                    var wrapperNode = new ExecutionTreeNode
                    {
                        Id = wrapperFullInfo?.Id.ToString() ?? "",
                        MethodName = childInfo.ChildMethodName,
                        MethodClass = childInfo.ClassName,
                        MethodFile = wrapperFullInfo?.File ?? "",
                        Code = wrapperFullInfo != null ? ExtractMethodCode(wrapperFullInfo) : "",
                        Origin = "RemoteWrapper",
                        Children = wrapperChildren
                    };

                    return wrapperNode;
                }
                else
                {
                    _errors.Add($"No backend method found for {httpInfo.Value.Verb} {httpInfo.Value.Path}");
                }
            }
            else
            {
                _errors.Add($"No HTTP info extracted from wrapper {childInfo.ChildMethodName}");
            }
        }

        // Intentar resolver en el mismo proyecto
        var candidate = FindMatchingMethod(allMethodsFrontend, childInfo);

        if (candidate != null)
        {
            Console.WriteLine($"   └─ {candidate.Class}.{candidate.MethodName}() [Local]");
            return BuildExecutionTree(allMethodsFrontend, allMethodsBackend, candidate, level);
        }

        _errors.Add($"Unresolved: {childInfo.ChildMethodName} (Assembly: {childInfo.ContainingAssembly})");
        
        return new ExecutionTreeNode
        {
            MethodName = childInfo.ChildMethodName,
            MethodClass = childInfo.ClassName,
            Unresolved = true,
            UnresolvedReason = $"No matching method found"
        };
    }

    private static MethodInfo FindMatchingMethod(List<MethodInfo> methods, MethodInfoChild childInfo)
    {
        var methodName = childInfo.ChildMethodName;
        var methodNameNoAsync = RemoveAsyncSuffix(methodName);

        // Búsqueda exacta sin Async
        var exact = methods.FirstOrDefault(m =>
            m.MethodName.Equals(methodNameNoAsync, StringComparison.OrdinalIgnoreCase) &&
            m.Namespace.Contains(childInfo.ContainingNamespace, StringComparison.OrdinalIgnoreCase));

        if (exact != null) return exact;

        // Búsqueda con Async
        var withAsync = methods.FirstOrDefault(m =>
            m.MethodName.Equals(methodName, StringComparison.OrdinalIgnoreCase) &&
            m.Namespace.Contains(childInfo.ContainingNamespace, StringComparison.OrdinalIgnoreCase));

        return withAsync;
    }

    private static MethodInfo FindBackendMethod(List<MethodInfo> methods, string httpVerb, string path)
    {
        if (string.IsNullOrWhiteSpace(httpVerb) || string.IsNullOrWhiteSpace(path))
            return null;

        var normalizedPath = NormalizePath(path);

        return methods.FirstOrDefault(m =>
            m.HttpVerb.Equals(httpVerb, StringComparison.OrdinalIgnoreCase) &&
            m.RouteTemplates?.Any(rt => NormalizePath(rt).Equals(normalizedPath, StringComparison.OrdinalIgnoreCase)) == true);
    }

    private static (string Verb, string Path)? ExtractHttpInfo(MethodInfoChild childInfo)
    {
        if (!string.IsNullOrWhiteSpace(childInfo.HttpVerb) && 
            !string.IsNullOrWhiteSpace(childInfo.RouteTemplate))
        {
            return (childInfo.HttpVerb, childInfo.RouteTemplate);
        }

        return null;
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "";
        
        path = path.Split('?')[0].ToLowerInvariant().TrimEnd('/');
        path = System.Text.RegularExpressions.Regex.Replace(path, @"\{[^}]+\}", "X", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        
        return path;
    }

    private static string RemoveAsyncSuffix(string methodName)
    {
        return methodName.EndsWith("Async", StringComparison.Ordinal)
            ? methodName[..^5]
            : methodName;
    }

    private static string ExtractMethodCode(MethodInfo method)
    {
        if (method.LineInit <= 0 || method.LineEnd <= 0)
            return "";

        try
        {
            if (File.Exists(method.File))
            {
                var lines = File.ReadAllLines(method.File);
                var startIdx = Math.Max(0, method.LineInit - 1);
                var endIdx = Math.Min(lines.Length - 1, method.LineEnd - 1);
                if (startIdx <= endIdx)
                {
                    var codeLines = lines.Skip(startIdx).Take(endIdx - startIdx + 1).ToList();
                    return string.Join(Environment.NewLine, codeLines);
                }
            }
        }
        catch { }

        return "";
    }
}
