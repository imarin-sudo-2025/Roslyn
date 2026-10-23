# 📚 Parameter Matching Framework - Usage Guide

## 🎯 What Does This Framework Do?

This framework automatically maps frontend method calls to backend API endpoints across two independent ASP.NET Core solutions using **Roslyn-based code analysis** and **NSwag-generated HTTP client wrappers**.

**Goal:** Trace complete end-to-end execution chains showing:
- Frontend Controller → Frontend Service → NSwag Wrapper → Backend API
- HTTP metadata (verb, route) at each level
- Complete method source code

---

## 📁 Project Structure

```
c:\TEST1\
├── SolucionA\                          # Backend API (StockTool)
│   ├── SolucionA.sln
│   └── Controllers\Api\
│       └── ProductosApiController.cs   # HTTP endpoints
│
├── SolucionB\                          # Frontend MVC (MasterData)
│   ├── SolucionB.sln
│   ├── Controllers\
│   │   └── ServicioLocalController.cs  # HTTP entry points
│   ├── Services\
│   │   ├── IServicioLocal.cs           # Service interface
│   │   └── ServicioLocal.cs            # Service implementation
│   └── RemoteClientBackEnd\
│       └── obj\
│           └── ServiceBackEndClient.cs # NSwag-generated wrapper
│
└── Catalog\                            # Analysis Framework
    ├── Catalog.csproj
    ├── Program.cs                      # Entry point
    ├── MethodExtractorRoslyn.cs        # Roslyn-based extraction
    ├── MethodReader.cs                 # Tree building & resolution
    ├── Models\
    │   ├── MethodInfo.cs               # Method metadata
    │   └── ExecutionTree.cs            # Tree structure
    └── output\
        ├── methods_SolucionA.json      # Extracted backend methods
        ├── methods_SolucionB.json      # Extracted frontend methods
        ├── *.json                      # Generated execution trees
        └── *.md                        # Analysis documentation
```

---

## 🚀 How to Run

### Step 1: Setup

Navigate to the Catalog project:
```powershell
cd C:\TEST1\Catalog
```

### Step 2: Execute Framework

```powershell
dotnet run
```

**Output will show:**
```
=== Extrayendo métodos de SolucionA (StockTool) ===
📂 Solución: c:\TEST1\SolucionA\SolucionA.sln
📦 Proyectos: 1
...
✓ Guardado: c:\TEST1\Catalog\output\methods_SolucionA.json

=== Extrayendo métodos de SolucionB (MasterData) ===
📂 Solución: c:\TEST1\SolucionB\SolucionB.sln
📦 Proyectos: 2
...
✓ Guardado: c:\TEST1\Catalog\output\methods_SolucionB.json

=== Analizando árbol de ejecución ===
📊 Encontrados 2 métodos públicos HTTP en frontend

📍 ServicioLocalController.ObtenerA()
   └─ ServicioLocal.ObtenerModeloAAsync() [Local]
   └─ ProductosApiAsync() [Wrapper]
      └─ ProductosApiController.GetPorId() [Backend/GET /api/ProductosApi/{id}]
   ...
✓ Árbol guardado: SolucionB.Controllers.ObtenerA_execution-tree.json
```

---

## 📊 Output Files Generated

### 1. `methods_SolucionA.json`
Extracted methods from backend (SolucionA)

**Fields:**
```json
{
  "Id": "unique-guid",
  "ProjectName": "SolucionA",
  "File": "path/to/file.cs",
  "LineInit": 24,
  "LineEnd": 31,
  "Namespace": "SolucionA.Controllers.Api",
  "Class": "ProductosApiController",
  "MethodName": "GetPorId",
  "ReturnType": "ActionResult<Producto>",
  "Parameters": ["int id"],
  "Attributes": ["HttpGet"],
  "IsAsync": false,
  "IsPublic": true,
  "Children": [],
  "HttpVerb": "GET",
  "RouteTemplates": ["/api/ProductosApi/{id}"]
}
```

### 2. `methods_SolucionB.json`
Extracted methods from frontend (SolucionB)

**Additional Fields (for wrappers):**
```json
{
  "IsRemoteWrapper": true,          // NSwag-generated client method
  "HttpVerb": "GET",                // Extracted from wrapper code
  "RouteTemplate": "/api/..."       // Extracted from wrapper code
}
```

### 3. Execution Tree JSONs
Generated for each public HTTP endpoint in frontend.

**File naming:** `{Namespace}.{MethodName}_execution-tree.json`

**Example:** `SolucionB.Controllers.ObtenerA_execution-tree.json`

**Structure:**
```json
{
  "Id": "method-guid",
  "MethodName": "ObtenerA",
  "MethodClass": "ServicioLocalController",
  "MethodFile": "path/to/ServicioLocalController.cs",
  "Code": "actual C# code...",
  "Children": [
    {
      "MethodName": "ObtenerModeloAAsync",
      "Origin": "Frontend",
      "Children": [
        {
          "MethodName": "ProductosApiAsync",
          "Origin": "RemoteWrapper",
          "Children": [
            {
              "MethodName": "GetPorId",
              "Origin": "Backend",
              "CrossSolution": {
                "HttpVerb": "GET",
                "RequestedPath": "/api/ProductosApi/{id}"
              },
              "Code": "actual C# code..."
            }
          ]
        }
      ]
    }
  ]
}
```

---

## 🔍 Understanding the Output

### Origin Types

| Origin | Meaning |
|--------|---------|
| `Frontend` | Method in SolucionB (controllers, services) |
| `RemoteWrapper` | NSwag-generated HTTP client method |
| `Backend` | Method in SolucionA (actual API endpoint) |

### Resolution Flow

1. **Frontend HTTP Method** (e.g., ObtenerA)
   - Has HttpVerb and RouteTemplates from attributes
   - Contains calls to service methods

2. **Frontend Service Method** (e.g., ObtenerModeloAAsync)
   - Resolved as [Local] - means found in same project
   - Contains calls to remote service wrapper

3. **Remote Wrapper Method** (e.g., ProductosApiAsync)
   - Resolved as [RemoteWrapper]
   - Has HTTP verb and route path extracted from generated code
   - Points to backend implementation

4. **Backend Method** (e.g., GetPorId)
   - Resolved as [Backend]
   - Matched via HTTP verb + normalized route path
   - Contains CrossSolution link with HTTP metadata

---

## 🐛 How It Detects Remote Wrappers

### Identification Logic

Method is flagged as RemoteWrapper if:

1. **Namespace contains "Remote" or "OpenAPI"**
   ```csharp
   if (fullNamespace.Contains("OpenAPI") || fullNamespace.Contains("Remote"))
       return true;
   ```

2. **Generated by NSwag** (has GeneratedCodeAttribute)
   ```csharp
   var isGeneratedByNSwag = calledMethod.ContainingType?.GetAttributes()
       .Any(a => a.AttributeClass?.Name == "GeneratedCodeAttribute" &&
                 a.ConstructorArguments[0].Value?.ToString() == "NSwag")
   ```

3. **Code pattern matches NSwag signature**
   ```csharp
   var bodyText = body?.ToString() ?? string.Empty;
   return bodyText.Contains("System.Net.Http.HttpMethod(", StringComparison.Ordinal) &&
          bodyText.Contains("urlBuilder_", StringComparison.Ordinal);
   ```

### HTTP Metadata Extraction

For remote wrappers, framework extracts HTTP info using regex:

```csharp
// Pattern 1: Find HTTP verb
new System.Net.Http.HttpMethod("GET")  → Verb: "GET"

// Pattern 2: Find URL parts (concatenated via Append)
.Append("/api/ProductosApi/").Append("{id}") → Path: "/api/ProductosApi/{id}"
```

If regex fails (e.g., in obj/ generated files), falls back to file-based extraction using source code regex patterns.

---

## 🔗 How It Matches Backend Methods

### Matching Algorithm

```
Frontend Wrapper Call: ProductosApiAsync(id)
  ↓
Extract HTTP Info: (GET, "/api/ProductosApi/{id}")
  ↓
Normalize Path: "/api/productoapi/x"  (replace {param} with X, lowercase)
  ↓
Search Backend Methods:
  - GetTodos()        → GET /api/ProductosApi       ✗ (different path)
  - GetPorId()        → GET /api/ProductosApi/{id}  ✓ MATCH!
  - GetPorNombre()    → GET /api/ProductosApi/buscar ✗
  ↓
Return: ProductosApiController.GetPorId()
```

### Normalization Rules

1. **Query string removal:** `/api/endpoint?param=value` → `/api/endpoint`
2. **Parameter replacement:** `{id}`, `{name}`, etc. → `X`
3. **Case insensitive:** `api/Endpoint` matches `api/endpoint`
4. **Whitespace trim:** `/api/endpoint/` → `/api/endpoint`

---

## 📋 Key Classes

### MethodExtractorRoslyn.cs
**Responsibility:** Extract method metadata using Roslyn AST analysis

**Main Methods:**
- `ExtractAllMethods()` - Main entry point, processes solutions
- `IsRemoteWrapperMethod()` - Detect if method is NSwag-generated
- `ExtractHttpVerbAndPathFromMethodInCodeAll()` - Extract HTTP info from wrapper code
- `ExtractWrapperHttpInfo()` - Try Roslyn-based extraction first
- `NormalizePath()` - Normalize route for comparison

### MethodReader.cs
**Responsibility:** Build execution trees and resolve cross-solution links

**Main Methods:**
- `ExtractAllEndPoints()` - Entry point for tree building
- `ProcessSolution()` - Process frontend solution
- `BuildExecutionTree()` - Recursively build tree from method
- `ResolveChild()` - Resolve child methods and wrappers
- `FindBackendMethod()` - Match frontend call to backend endpoint
- `FindMatchingMethod()` - Find method in same project

### Models
- `MethodInfo` - Metadata for extracted methods
- `MethodInfoChild` - Information about method's child calls
- `ExecutionTreeNode` - Node in execution tree
- `CrossSolutionLink` - Link metadata for wrapper→backend calls

---

## ⚙️ Configuration

### Solution Paths

Edit `MethodExtractorRoslyn.cs` to change solutions:

```csharp
public static async Task ExtractAllMethods(string solutionPath, string filename)
{
    // Change this path:
    var solutionPath = @"c:\TEST1\SolucionA\SolucionA.sln";
    
    // Output filename (appended with .json):
    var filename = "_SolucionA";  // → methods_SolucionA.json
}
```

Call in `Program.cs`:
```csharp
await MethodExtractorRoslyn.ExtractAllMethods(@"c:\TEST1\SolucionA\SolucionA.sln", "_SolucionA");
await MethodExtractorRoslyn.ExtractAllMethods(@"c:\TEST1\SolucionB\SolucionB.sln", "_SolucionB");
```

### Output Directory

All outputs are written to:
```
{ProjectRoot}\output\
```

Create this directory if missing:
```powershell
mkdir C:\TEST1\Catalog\output
```

---

## 🧪 Testing the Framework

### Manual Verification

1. **Extract methods:**
   ```powershell
   cd C:\TEST1\Catalog
   dotnet run
   ```

2. **Check methods JSON:**
   ```powershell
   # View extracted methods from SolucionA
   $json = Get-Content output\methods_SolucionA.json | ConvertFrom-Json
   $json | Where-Object { $_.MethodName -eq "GetPorId" }
   
   # View extracted methods from SolucionB
   $json = Get-Content output\methods_SolucionB.json | ConvertFrom-Json
   $json | Where-Object { $_.IsRemoteWrapper -eq $true }
   ```

3. **Inspect execution tree:**
   ```powershell
   # View tree for ObtenerA
   $tree = Get-Content output\SolucionB.Controllers.ObtenerA_execution-tree.json | ConvertFrom-Json
   $tree.Children[0].Children[0]  # Should show ProductosApiAsync wrapper
   ```

### Expected Results

After running, you should see:
- ✅ 9 methods extracted from SolucionA (ProductosApiController, ProductosController, ProductoService)
- ✅ 20+ methods extracted from SolucionB (including NSwag generated methods)
- ✅ 2 execution trees generated (ObtenerA, ObtenerB)
- ✅ 0 unresolved dependencies
- ✅ GetPorId with non-empty Code field
- ✅ CrossSolution links with HttpVerb and RequestedPath populated

---

## 🐞 Troubleshooting

### Issue: "No backend method found"

**Cause:** HTTP verb or route path mismatch

**Solution:**
1. Check `methods_SolucionA.json` - verify backend method has correct HttpVerb and RouteTemplates
2. Check wrapper method code - verify HTTP verb matches exactly
3. Check route normalization - ensure path normalization doesn't strip important parts

### Issue: GetPorId.Code is empty

**Cause:** Backend file not accessible or line numbers incorrect

**Solution:**
1. Verify file exists at `MethodFile` path
2. Check `LineInit` and `LineEnd` are correct
3. Check file encoding (should be UTF-8)

### Issue: ObtenerModeloBAsync not appearing

**Cause:** Method not detected as child invocation

**Solution:**
1. Verify method is actually called in parent method
2. Check method signature matches what Roslyn finds
3. Verify namespace resolution is correct

### Issue: ProductosApiAsync not showing as wrapper

**Cause:** Not detected as RemoteWrapper or namespace not recognized

**Solution:**
1. Verify class namespace contains "Remote" or "OpenAPI"
2. Check if class has GeneratedCodeAttribute with "NSwag"
3. Verify class contains `System.Net.Http.HttpMethod` and `urlBuilder_` patterns

---

## 📈 Future Enhancements

1. **API Validation** - Run actual HTTP requests to verify mapped endpoints
2. **Performance Analysis** - Trace execution time through the chain
3. **Dependency Injection Tracking** - Show how services are injected
4. **Database Call Tracking** - Extend to trace data access patterns
5. **Configuration Export** - Generate integration test fixtures
6. **Web UI** - Interactive tree visualization and search

---

## 📞 Support

For issues or questions:
1. Check `DEBUG_RESOLUTION_LOG.md` for detailed trace examples
2. Review `EXECUTION_TREE_VISUAL.md` for example output
3. Inspect JSON files in `output/` directory
4. Enable verbose logging by adding Console.WriteLine() to key methods

---

**Framework Version:** 1.0
**Last Updated:** 2024-10-23
**Status:** Operational ✅
