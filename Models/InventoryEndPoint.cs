// InventoryEndpoint.cs
using System.Collections.Generic;

namespace CatalogApp;

public class InventoryEndpoint
{
    public string ClassName { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;      // ControllerRoute, OpenApiPath, TsHttpCall, UrlReference, CSharpOrVbHttpCall
    public string HttpVerbReal { get; set; } = string.Empty;  // GET/POST/PUT/DELETE/...
    public string Method { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty;
    public string MethodName { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string SourceSpec { get; set; } = string.Empty;
    public string Origin { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;         // Internal / External
    public string OperationId { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;     // Inbound / Outbound
    public string ResultStatus { get; set; } = string.Empty;
    public string ResultType { get; set; } = string.Empty;
    public List<InventoryParameter> Parameters { get; set; } = new();
    public string Version { get; set; } = string.Empty;
    public string SourceFile { get; set; } = string.Empty;
    public int ParametersCount { get; set; }
}

public class InventoryParameter
{
    public string Name { get; set; } = string.Empty;
    public string In { get; set; } = string.Empty;
    public bool? Required { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Ref { get; set; } = string.Empty;
}