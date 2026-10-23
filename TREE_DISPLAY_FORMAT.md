# FORMATO PARA MOSTRAR ÁRBOLES DE EJECUCIÓN

Cuando se pida mostrar un **"árbol de ejecución"** o **"execution tree"**, usar ESTE FORMATO EXACTO:

```
📦 ROOT
├─ "Id": "valor-uuid"
├─ "MethodName": "NombreMétodo"
├─ "MethodClass": "NombreClase"
├─ "MethodFile": "c:\ruta\completa\archivo.cs"
├─ "Origin": "Frontend|Backend"
├─ "Unresolved": true|false
├─ "Code": 
│  "CONTENIDO COMPLETO DEL CÓDIGO - NO ABREVIADO
│   Con saltos de línea, indentación, TODA LA ESTRUCTURA
│   public async Task<ActionResult<ModeloA>> ObtenerA(int id)
│   {
│       var resultado = await _servicioLocal.ObtenerModeloAAsync(id);
│       return Ok(resultado);
│   }"
│
└─ "Children": [n]
   │
   └─ 📦 [0]
      ├─ "Id": "uuid"
      ├─ "MethodName": "NombreMétodo"
      ├─ "MethodClass": "NombreClase"
      ├─ "MethodFile": "ruta"
      ├─ "Origin": "Frontend|Backend"
      ├─ "Unresolved": true|false
      ├─ "Code": 
      │  "CONTENIDO COMPLETO"
      │
      └─ "Children": [n]
         │
         └─ 📦 [0]
            ├─ "Id": "uuid"
            ├─ "MethodName": "NombreMétodo"
            ├─ "MethodClass": "NombreClase"
            ├─ "MethodFile": "ruta"
            ├─ "Origin": "Frontend|Backend"
            ├─ "Unresolved": true|false
            ├─ "Code": "CONTENIDO COMPLETO O [VACÍO] SI ESTÁ VACÍO"
            ├─ "Children": []
            │
            └─ "CrossSolution": (solo si existe)
               ├─ "HttpVerb": "GET|POST|PUT|DELETE"
               ├─ "RequestedPath": "/api/ruta/con/{id}"
               ├─ "MatchedPath": "valor o [VACÍO]"
               ├─ "InventoryCategory": "valor o [VACÍO]"
               ├─ "TargetClassName": "valor o [VACÍO]"
               ├─ "TargetSourceFile": "valor o [VACÍO]"
               └─ "TargetInventoryId": "valor o [VACÍO]"
```

## REGLAS
- ✅ MOSTRAR TODO EL CONTENIDO DE "Code" - NUNCA abreviar con `...`
- ✅ Marcar explícitamente si es `[VACÍO]` cuando esté vacío
- ✅ Expandir TODOS los niveles del árbol
- ✅ Mostrar TODOS los campos de cada objeto
- ❌ NO usar `[TIENE CONTENIDO] ✓` 
- ❌ NO omitir información
- ❌ NO summarizar
