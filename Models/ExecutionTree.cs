using System;
using System.Collections.Generic;
using System.Text;

namespace CatalogApp;


public class ExecutionTreeNode
{
    public string Id { get; set; }
    public string MethodName { get; set; } = string.Empty;
    public string MethodClass { get; set; } = string.Empty;
    public string MethodFile { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public List<ExecutionTreeNode> Children { get; set; } = new();
    public string Origin { get; set; } = "MasterData";
    public CrossSolutionLink? CrossSolution { get; set; }
    public bool Unresolved { get; set; } = false;
    public string? UnresolvedReason { get; set; }
}
public class CrossSolutionLink
{
    public string HttpVerb { get; set; } = string.Empty;
    public string RequestedPath { get; set; } = string.Empty;
}