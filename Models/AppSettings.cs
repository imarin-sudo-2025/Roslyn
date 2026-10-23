namespace CatalogApp;

public class AppSettings
{
    public Dictionary<string, SolutionConfig> Solutions { get; set; } = new();
    public OutputConfig Output { get; set; } = new();
    public ExtractionConfig Extraction { get; set; } = new();
    public AnalysisConfig Analysis { get; set; } = new();
}

public class SolutionConfig
{
    public string Path { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty;
}

public class OutputConfig
{
    public string Directory { get; set; } = "./output";
    public string MethodsFile { get; set; } = "methods_{0}.json";
    public string ExecutionTreeFile { get; set; } = "{0}.{1}_{2}_execution-tree.json";
}

public class ExtractionConfig
{
    public List<string> ExcludePatterns { get; set; } = new();
    public List<string> IncludeControllers { get; set; } = new();
    public List<string> IncludeServices { get; set; } = new();
}

public class AnalysisConfig
{
    public string HttpEndpointPattern { get; set; } = string.Empty;
    public List<string> RemoteWrapperNamespaces { get; set; } = new();
}
