using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using System.Text.Json;

namespace CatalogApp;

public class MethodExtractorRoslyn
{
    public static async Task ExtractAllMethods(string solutionPath, string filename)
    {
        if (!File.Exists(solutionPath))
        {
            Console.Error.WriteLine($"No se encuentra la solución: {solutionPath}");
            return;
        }

        // MSBuildLocator.RegisterDefaults() ya fue llamado en Program.Main()

        using var workspace = MSBuildWorkspace.Create();

        var solution = await workspace.OpenSolutionAsync(solutionPath);

        Console.WriteLine($"📂 Solución: {solutionPath}");
        Console.WriteLine($"📦 Proyectos: {solution.Projects.Count()}\n");

        var allMethods = new List<MethodInfo>();

        // 2. Itera por cada proyecto
        foreach (var project in solution.Projects)
        {
            Console.WriteLine($"📌 Proyecto: {project.Name}");

            if (project.Name?.Contains("Testing", StringComparison.OrdinalIgnoreCase) == true) continue;

            var compilation = await project.GetCompilationAsync();
            if (compilation == null) continue;

            foreach (var syntaxTree in compilation.SyntaxTrees)
            {
                var filePath = syntaxTree.FilePath;

                // Salta bin/ y obj/
                if (filePath.Contains("\\bin\\") && filePath.Contains("\\obj\\"))
                    continue;

                var root = await syntaxTree.GetRootAsync();
                var semanticModel = compilation.GetSemanticModel(syntaxTree);

                // 4. Extrae todos los métodos del archivo
                var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

                foreach (var method in methods)
                {

                    List<MethodInfoChild> methodChild = new();

                    // Obtén clase contenedora
                    var classDecl = method.FirstAncestorOrSelf<ClassDeclarationSyntax>();
                    if (classDecl == null) continue;

                    var className = classDecl.Identifier.Text;
                    var methodName = method.Identifier.Text;
                    var lineNumber = method.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    var lineNumberEnd = method.GetLocation().GetLineSpan().EndLinePosition.Line + 1;

                    // Obtén namespace
                    var namespaceSyntax = method.FirstAncestorOrSelf<NamespaceDeclarationSyntax>();
                    var namespaceName = namespaceSyntax?.Name.ToString() ?? "global";

                    var invocations = method.DescendantNodes().OfType<InvocationExpressionSyntax>();

                    foreach (var invocation in invocations)
                    {
                        var info = semanticModel.GetSymbolInfo(invocation);

                        if (info.Symbol is IMethodSymbol calledMethod)
                        {
                            var allDeclarations = calledMethod
                                .DeclaringSyntaxReferences
                                .Select(x => x.GetSyntax())
                                .OfType<MethodDeclarationSyntax>()
                                .ToList();

                            var declaration =
                                allDeclarations.FirstOrDefault(d =>
                                {
                                    var p = d.SyntaxTree.FilePath;
                                    return !(p.Contains("\\bin\\") || p.Contains("\\obj\\"));
                                })
                                ?? allDeclarations.FirstOrDefault();

                            if (declaration != null)
                            {
                                var PO = declaration.ParameterList.Parameters
                                       .Select(x => x.GetText().ToString())
                                       .ToList();

                                var PORET = declaration.ReturnType.ToString();

                                var isRemoteWrapperChild = IsRemoteWrapperMethod(calledMethod, declaration);
                                var (childHttpVerb, childRouteTemplate) = (string.Empty, string.Empty);
                                
                                if (isRemoteWrapperChild)
                                {
                                    // Intentar extraer con Roslyn primero
                                    (childHttpVerb, childRouteTemplate) = ExtractWrapperHttpInfo(declaration);
                                    
                                    // Si no encontramos verbo y está en obj/, buscar en el archivo
                                    if (string.IsNullOrWhiteSpace(childHttpVerb) && declaration.SyntaxTree.FilePath.Contains("\\obj\\"))
                                    {
                                        var childMethodName = declaration.Identifier.Text;
                                        try
                                        {
                                            var sourceCode = System.IO.File.ReadAllText(declaration.SyntaxTree.FilePath);
                                            // Buscar el método recursivamente (todas las versiones)
                                            (childHttpVerb, childRouteTemplate) = ExtractHttpVerbAndPathFromMethodInCodeAll(sourceCode, childMethodName);
                                        }
                                        catch { }
                                    }
                                }

                                methodChild.Add(new MethodInfoChild
                                {
                                    ChildMethodName = calledMethod.Name,
                                    ContainingAssembly = calledMethod.ContainingAssembly.Name,
                                    ContainingNamespace = calledMethod.ContainingNamespace.Name,
                                    Parameters = PO,
                                    ReturnType = PORET,
                                    ClassName = calledMethod.ContainingType?.Name ?? string.Empty,
                                    IsAsync = calledMethod.IsAsync,
                                    IsVirtual = calledMethod.IsVirtual,
                                    IsOverride = calledMethod.IsOverride,
                                    FilePath = calledMethod.Locations.FirstOrDefault()?.SourceTree?.FilePath,
                                    IsRemoteWrapper = isRemoteWrapperChild,
                                    HttpVerb = childHttpVerb,
                                    RouteTemplate = childRouteTemplate
                                });
                            }
                        }
                    }

                    // Obtén parámetros iterando cada uno para capturar formato exacto
                    var parameters = method.ParameterList.Parameters
                        .Select(p => p.GetText().ToString())
                        .ToList();

                    // Obtén tipo de retorno
                    var returnType = method.ReturnType.ToString();

                    // Obtén atributos (ej: [HttpPost], [Authorize])
                    var attributes = method.AttributeLists
                        .SelectMany(a => a.Attributes)
                        .Select(a => a.Name.ToString())
                        .ToList();

                    // Verifica si es async
                    var isAsync = method.Modifiers.Any(m => m.Text == "async");

                    // Verifica si es público
                    var isPublic = method.Modifiers.Any(m => m.Text == "public");

                    // Determina si el PROPIO método
                    // reutilizando la misma heurística de evidencia real aplicada a los hijos invocados.
                    var methodSymbol = semanticModel.GetDeclaredSymbol(method);
                    var isRemoteWrapper = methodSymbol != null && IsRemoteWrapperMethod(methodSymbol, method);

                    // Captura verbo HTTP + rutas completas (Route de clase + HttpGet/HttpPost/... del
                    // método) para poder resolver el controlador real a partir del verbo+path extraído
                    // del cuerpo de un wrapper remoto, en vez de por coincidencia de nombre.
                    var (httpVerb, routeTemplates) = ExtractRouteInfo(classDecl, method, className);

                    var methodInfo = new MethodInfo
                    {
                        ProjectName = project.Name,
                        File = filePath,
                        LineInit = lineNumber,
                        LineEnd = lineNumberEnd,
                        Namespace = namespaceName,
                        Class = className,
                        MethodName = methodName,
                        ReturnType = returnType,
                        Parameters = parameters,
                        Attributes = attributes,
                        IsAsync = isAsync,
                        IsPublic = isPublic,
                        Children = methodChild,
                        IsRemoteWrapper = isRemoteWrapper,
                        HttpVerb = httpVerb,
                        RouteTemplates = routeTemplates
                    };

                    var qry = allMethods.Where(x => x.MethodName == methodName &&
                                                    x.File == filePath &&
                                                    x.Class == className &&
                                                    x.ReturnType == returnType &&
                                                    x.Parameters == parameters)
                                         .FirstOrDefault();

                    if (qry is null)
                    {
                        allMethods.Add(methodInfo);
                    }
                    else
                    {
                        // Imprime en consola
                        Console.WriteLine($"  Ya existe {className}.{methodName}()");
                    }

                    // Imprime en consola
                    var async = isAsync ? "async " : "";
                    var pub = isPublic ? "public " : "";
                    Console.WriteLine($"  └─ [{lineNumber:D4}] {pub}{async}{className}.{methodName}()");

                }

            }

        }

        if (allMethods.Count > 0)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var json = JsonSerializer.Serialize(allMethods, options);

            File.WriteAllText($"c:\\TEST1\\Catalog\\output\\methods{filename}.json", json);
            Console.WriteLine($"✓ Guardado: c:\\TEST1\\Catalog\\output\\methods{filename}.json");
        }

    }

    private static (string HttpVerb, List<string> RouteTemplates) ExtractRouteInfo(
        ClassDeclarationSyntax classDecl, MethodDeclarationSyntax method, string className)
    {
        var httpVerbNames = new[] { "HttpGet", "HttpPost", "HttpPut", "HttpDelete", "HttpPatch", "HttpHead", "HttpOptions" };

        var verbAttribute = method.AttributeLists
            .SelectMany(a => a.Attributes)
            .FirstOrDefault(a => httpVerbNames.Contains(a.Name.ToString()));

        if (verbAttribute == null)
            return (string.Empty, new List<string>());

        var httpVerb = verbAttribute.Name.ToString()["Http".Length..].ToUpperInvariant();

        var methodTemplate = GetFirstStringLiteralArgument(verbAttribute);

        var classRouteTemplates = classDecl.AttributeLists
            .SelectMany(a => a.Attributes)
            .Where(a => a.Name.ToString() == "Route")
            .Select(GetFirstStringLiteralArgument)
            .ToList();

        if (!classRouteTemplates.Any())
            classRouteTemplates.Add(string.Empty);

        var controllerToken = className.EndsWith("Controller", StringComparison.OrdinalIgnoreCase)
            ? className[..^"Controller".Length]
            : className;

        var routeTemplates = classRouteTemplates
            .Select(classTemplate =>
            {
                var resolvedClassTemplate = classTemplate.Replace("[controller]", controllerToken, StringComparison.OrdinalIgnoreCase);
                var combined = string.Join("/", new[] { resolvedClassTemplate, methodTemplate }
                    .Where(s => !string.IsNullOrWhiteSpace(s)));
                return "/" + combined.Trim('/');
            })
            .ToList();

        return (httpVerb, routeTemplates);
    }

    private static string GetFirstStringLiteralArgument(AttributeSyntax attribute) =>
        attribute.ArgumentList?.Arguments
            .Select(a => a.Expression)
            .OfType<LiteralExpressionSyntax>()
            .Select(l => l.Token.ValueText)
            .FirstOrDefault() ?? string.Empty;

    private static bool IsRemoteWrapperMethod(IMethodSymbol calledMethod, MethodDeclarationSyntax declaration)
    {
        var fullNamespace = calledMethod.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        if (fullNamespace.Contains("OpenAPI", StringComparison.OrdinalIgnoreCase) ||
            fullNamespace.Contains("Remote", StringComparison.OrdinalIgnoreCase))
            return true;

        var isGeneratedByNSwag = calledMethod.ContainingType?.GetAttributes()
            .Any(a => a.AttributeClass?.Name == "GeneratedCodeAttribute" &&
                      a.ConstructorArguments.Length > 0 &&
                      string.Equals(a.ConstructorArguments[0].Value?.ToString(), "NSwag", StringComparison.OrdinalIgnoreCase))
            ?? false;
        if (isGeneratedByNSwag)
            return true;

        var body = (SyntaxNode?)declaration.Body ?? declaration.ExpressionBody;
        var bodyText = body?.ToString() ?? string.Empty;
        return bodyText.Contains("System.Net.Http.HttpMethod(", StringComparison.Ordinal) &&
               bodyText.Contains("urlBuilder_", StringComparison.Ordinal);
    }

    private static readonly System.Text.RegularExpressions.Regex _wrapperHttpMethodRegex =
        new(@"new\s+System\.Net\.Http\.HttpMethod\(""(\w+)""\)", System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private static readonly System.Text.RegularExpressions.Regex _wrapperAppendLiteralRegex =
        new(@"\.Append\(""([^""]+)""\)", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static (string HttpVerb, string RouteTemplate) ExtractHttpVerbAndPathFromMethodInCode(string sourceCode, string methodName)
    {
        // Busca: public async ... MethodName(...) { ... }
        // Patrón: public [async] [tipo retorno] MethodName (parámetros) {
        // Usamos [\s\S]* en lugar de [^{]* para capturar multi-línea correctamente
        var escapedMethodName = System.Text.RegularExpressions.Regex.Escape(methodName);
        var pattern = $@"public\s+(?:async\s+)?[\s\S]*?{escapedMethodName}\s*\([^)]*\)\s*\{{";
        var regex = new System.Text.RegularExpressions.Regex(pattern, System.Text.RegularExpressions.RegexOptions.Multiline);
        var match = regex.Match(sourceCode);
        
        if (!match.Success)
        {
            // Fallback: patrón más simple para el caso "async public"
            pattern = $@"async\s+[\s\S]*?{escapedMethodName}\s*\([^)]*\)\s*\{{";
            regex = new System.Text.RegularExpressions.Regex(pattern, System.Text.RegularExpressions.RegexOptions.Multiline);
            match = regex.Match(sourceCode);
        }
        
        if (!match.Success) return (string.Empty, string.Empty);
        
        // Encuentra el inicio del cuerpo (después del {)
        var startIndex = match.Index + match.Length;
        
        // Busca el cierre de llaves correspondiente (cuenta llaves abiertas/cerradas)
        int braceCount = 1;
        int endIndex = startIndex;
        while (endIndex < sourceCode.Length && braceCount > 0)
        {
            if (sourceCode[endIndex] == '{') braceCount++;
            else if (sourceCode[endIndex] == '}') braceCount--;
            endIndex++;
        }
        
        // Extrae el cuerpo del método
        var methodBody = sourceCode.Substring(startIndex, endIndex - startIndex - 1);
        return ExtractHttpVerbAndPathFromCode(methodBody);
    }

    private static (string HttpVerb, string RouteTemplate) ExtractHttpVerbAndPathFromMethodInCodeAll(string sourceCode, string methodName)
    {
        // Busca TODAS las ocurrencias del método en el archivo y retorna la primera que tiene HTTP info
        var escapedMethodName = System.Text.RegularExpressions.Regex.Escape(methodName);
        var pattern = $@"public\s+(?:async\s+)?[\s\S]*?{escapedMethodName}\s*\([^)]*\)\s*\{{";
        var regex = new System.Text.RegularExpressions.Regex(pattern, System.Text.RegularExpressions.RegexOptions.Multiline);
        
        var matches = regex.Matches(sourceCode);
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            // Encuentra el inicio del cuerpo (después del {)
            var startIndex = match.Index + match.Length;
            
            // Busca el cierre de llaves correspondiente (cuenta llaves abiertas/cerradas)
            int braceCount = 1;
            int endIndex = startIndex;
            while (endIndex < sourceCode.Length && braceCount > 0)
            {
                if (sourceCode[endIndex] == '{') braceCount++;
                else if (sourceCode[endIndex] == '}') braceCount--;
                endIndex++;
            }
            
            // Extrae el cuerpo del método
            var methodBody = sourceCode.Substring(startIndex, endIndex - startIndex - 1);
            var (verb, path) = ExtractHttpVerbAndPathFromCode(methodBody);
            
            // Si encuentra HTTP info, retorna inmediatamente
            if (!string.IsNullOrWhiteSpace(verb))
            {
                return (verb, path);
            }
        }
        
        return (string.Empty, string.Empty);
    }

    private static (string HttpVerb, string RouteTemplate) ExtractWrapperHttpInfo(MethodDeclarationSyntax declaration)
    {
        var body = (SyntaxNode?)declaration.Body ?? declaration.ExpressionBody;
        var bodyText = body?.ToString() ?? string.Empty;
        return ExtractHttpVerbAndPathFromCode(bodyText);
    }

    private static (string HttpVerb, string RouteTemplate) ExtractHttpVerbAndPathFromCode(string code)
    {
        // Busca patrones: new System.Net.Http.HttpMethod("GET")
        var verbMatch = _wrapperHttpMethodRegex.Match(code);
        if (!verbMatch.Success)
        {
            // Intenta un patrón más simple: HttpMethod("GET")
            verbMatch = System.Text.RegularExpressions.Regex.Match(code, @"HttpMethod\(['""](\w+)['""]\)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        if (!verbMatch.Success) return (string.Empty, string.Empty);
        var verb = verbMatch.Groups[1].Value.ToUpperInvariant();

        // Busca .Append("/api/...")
        var pathLiterals = _wrapperAppendLiteralRegex.Matches(code)
            .Select(m => m.Groups[1].Value)
            .Where(s => s.StartsWith("/"))
            .ToList();

        if (!pathLiterals.Any())
        {
            // Intenta patrón más simple: .Append("...")
            pathLiterals = System.Text.RegularExpressions.Regex.Matches(code, @"\.Append\(['""]([^'""]+)['""]\)")
                .Cast<System.Text.RegularExpressions.Match>()
                .Select(m => m.Groups[1].Value)
                .Where(s => s.StartsWith("/"))
                .ToList();
        }

        return pathLiterals.Any() ? (verb, string.Join(string.Empty, pathLiterals)) : (string.Empty, string.Empty);
    }
}

