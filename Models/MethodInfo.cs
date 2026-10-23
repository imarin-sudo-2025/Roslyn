using System;
using System.Collections.Generic;
using System.Text;

namespace CatalogApp;

public class MethodInfo
{

    public Guid Id  { get; set; } = Guid.NewGuid();
    public string ProjectName { get; set; } = string.Empty;
    public string File { get; set; } = string.Empty;
    public int LineInit { get; set; }
    public int LineEnd { get; set; }
    public string Namespace { get; set; } = string.Empty;
    public string Class { get; set; } = string.Empty;
    public string MethodName { get; set; } = string.Empty;
    public string ReturnType { get; set; } = string.Empty;
    public List<string> Parameters { get; set; } = new();
    public List<string> Attributes { get; set; } = new();
    public bool IsAsync { get; set; }
    public bool IsPublic { get; set; }
    public List<MethodInfoChild> Children { get; set; } = new();

    /// <summary>
    /// Indica, con la misma evidencia real usada en MethodInfoChild.IsRemoteWrapper, si este método
    /// ES (no si invoca a) un wrapper remoto/OpenAPI generado (p.ej. por NSwag). Cuando es true, sus
    /// Children son plumbing interno del cliente HTTP generado (PrepareRequest, ProcessResponse,
    /// etc.), no flujo de negocio real, y no deben expandirse en el árbol de ejecución.
    /// </summary>
    public bool IsRemoteWrapper { get; set; }

    /// <summary>
    /// Verbo HTTP (GET/POST/...) tomado del atributo HttpGet/HttpPost/... del método, solo
    /// aplicable a acciones de controlador. Vacío si el método no es una acción HTTP.
    /// </summary>
    public string HttpVerb { get; set; } = string.Empty;

    /// <summary>
    /// Rutas completas combinadas (plantilla de [Route] de la clase + plantilla del atributo
    /// HttpGet/HttpPost/... del método), una por cada [Route] de clase si hay varias (p.ej.
    /// versión implícita y versión explícita). Permite resolver el controlador real a partir del
    /// verbo+path extraído del cuerpo de un wrapper remoto/OpenAPI, en vez de por nombre.
    /// </summary>
    public List<string> RouteTemplates { get; set; } = new();

}

public class MethodInfoChild
{
    public string ChildMethodName {  get; set; } = string.Empty;
    public string ContainingAssembly { get; set; } = string.Empty;
    public string ContainingNamespace { get; set; } = string.Empty;
    public List<string> Parameters { get; set; } = new();
    public string ReturnType { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public bool IsAsync { get; set; }
    public bool IsPublic { get; set; }
    public bool IsOverride { get; internal set; }
    public bool IsVirtual { get; internal set; }
    public string? FilePath { get; internal set; }

    /// <summary>
    /// Indica si el método invocado es, con evidencia real (no una suposición por ser async),
    /// un wrapper remoto/OpenAPI: namespace OpenAPI/Remote, clase generada por NSwag
    /// ([GeneratedCode("NSwag")]) o cuerpo con el patrón típico de cliente HTTP generado
    /// (HttpMethod(...) + urlBuilder_.Append(...)). Se calcula en MethodExtractor, donde
    /// se dispone del árbol sintáctico/semántico completo del método invocado.
    /// </summary>
    public bool IsRemoteWrapper { get; set; }

    /// <summary>
    /// Verbo HTTP (GET/POST/...) extraído del cuerpo del método invocado cuando
    /// <see cref="IsRemoteWrapper"/> es true. Se calcula en el momento de la extracción, por lo
    /// que no depende de que el wrapper exista también como nodo propio en la lista de métodos
    /// (p.ej. clientes NSwag generados únicamente en obj\ vía &lt;OpenApiReference&gt;, que nunca se
    /// recorren como nodo padre). Vacío si no es un wrapper remoto o no se pudo extraer.
    /// </summary>
    public string HttpVerb { get; set; } = string.Empty;

    /// <summary>
    /// Ruta completa (concatenación de los literales urlBuilder_.Append("...")) extraída del
    /// cuerpo del método invocado cuando <see cref="IsRemoteWrapper"/> es true. Permite resolver
    /// el destino real (controlador local o endpoint del inventario cross-solution) directamente
    /// desde este hijo, sin necesidad de localizar su propia declaración como nodo padre.
    /// </summary>
    public string RouteTemplate { get; set; } = string.Empty;
}
