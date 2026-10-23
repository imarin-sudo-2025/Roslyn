#!/usr/bin/env node
/**
 * generate-execution-tree-md.js
 *
 * Genera un árbol interactivo en Markdown (tipo "Watch" de Visual Studio) a partir
 * de un fichero `*_execution-tree.json` producido por MethodReader.cs.
 *
 * Cada nodo del árbol se renderiza como un <details> expandible con:
 *   - Un <summary> con "Clase.Metodo [Id]" (y "(dup)" si el Id ya apareció antes).
 *   - Un sub-<details> "Code" con el código fuente del método en <pre><code>.
 *   - Sus hijos reales como <details> hermanos anidados dentro del propio nodo.
 *
 * Reglas de formato validadas manualmente (ver .github/copilot-instructions.md):
 *   1. NO usar indentación de espacios en el texto fuente delante de las etiquetas
 *      <details>/<summary>. El anidamiento real de las etiquetas HTML (una dentro de
 *      otra) es la ÚNICA fuente de jerarquía. 4+ espacios de indentación hacen que
 *      CommonMark interprete la línea como un bloque de código indentado, rompiendo
 *      el reconocimiento del HTML.
 *   2. El código del método se inserta como HTML puro <pre><code>, nunca como code
 *      fence (```). Un code fence dentro de un <details> anidado, seguido de línea en
 *      blanco, hace que el parser termine el "modo HTML en bruto" ahí, rompiendo la
 *      anidación de los <details> siguientes.
 *   3. El código se escapa manualmente (&amp; &lt; &gt;) porque dentro de <pre><code>
 *      no se interpreta Markdown.
 *   4. Las líneas completamente vacías dentro del código se sustituyen por "&nbsp;",
 *      porque una línea vacía real dentro de <pre><code> hace que el parser trate el
 *      bloque como terminado ahí, generando un segundo bloque de código separado.
 *   5. La sangría visual se consigue con `margin-left: profundidad * 20px` inline en
 *      cada <details> (el sub-<details> "Code" usa un nivel más que su nodo padre).
 *   6. Cada <details> lleva un reset explícito `margin:0;padding:0 0 4px 0;` para
 *      evitar el "margin collapsing" por defecto del navegador, que provocaba un
 *      espaciado inconsistente entre nodos hermanos.
 *   7. Si un mismo Id (GUID) ya apareció antes en el recorrido (recursión/reutilización
 *      del método), se marca con "(dup)" en el <summary> pero se sigue expandiendo
 *      igualmente con sus propios hijos (no se colapsa ni se omite).
 *   8. El recorrido siempre empieza por el nodo raíz real del JSON de entrada.
 *
 * Uso:
 *   node tools/generate-execution-tree-md.js <ruta-al-json-de-entrada> [ruta-salida.md]
 *
 * Si no se indica ruta de salida, se deriva del nombre del JSON de entrada,
 * sustituyendo el sufijo "_execution-tree.json" (o la extensión .json) por
 * "_execution-tree-interactive.md".
 */

"use strict";

const fs = require("fs");
const path = require("path");

const STEP_PX = 20;
const DETAILS_RESET_STYLE = "margin:0;padding:0 0 4px 0;";

function escapeHtml(text) {
    if (text == null) return "";
    return String(text)
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;");
}

/**
 * Escapa el código para HTML y sustituye las líneas completamente vacías por "&nbsp;"
 * para evitar que <pre><code> se corte en dos bloques separados.
 */
function escapeCodeForPre(code) {
    const escaped = escapeHtml(code);
    return escaped
        .split(/\r\n|\n/)
        .map((line) => (line.length === 0 ? "&nbsp;" : line))
        .join("\n");
}

function detailsStyle(depth) {
    return `${DETAILS_RESET_STYLE}margin-left:${depth * STEP_PX}px`;
}

/**
 * Recorre recursivamente el árbol y devuelve el Markdown/HTML del nodo (incluyendo
 * sus hijos), marcando con "(dup)" los Ids ya vistos en el recorrido.
 */
function renderNode(node, depth, seenIds) {
    const isDup = seenIds.has(node.Id);
    seenIds.add(node.Id);

    const label = `${node.MethodClass}.${node.MethodName} [${node.Id}]${isDup ? " (dup)" : ""}`;
    const codeDepth = depth + 1;

    const lines = [];
    lines.push(`<details style="${detailsStyle(depth)}">`);
    lines.push(`<summary>${escapeHtml(label)}</summary>`);
    lines.push(`<details style="${detailsStyle(codeDepth)}">`);
    lines.push(`<summary>Code</summary>`);
    lines.push(`<pre style="margin:4px 0"><code class="language-csharp">${escapeCodeForPre(node.Code)}</code></pre>`);
    lines.push(`</details>`);

    for (const child of node.Children || []) {
        lines.push(renderNode(child, depth + 1, seenIds));
    }

    lines.push(`</details>`);
    return lines.join("\n");
}

function deriveOutputPath(inputPath) {
    const dir = path.dirname(inputPath);
    const base = path.basename(inputPath);
    let stem;
    if (base.toLowerCase().endsWith("_execution-tree.json")) {
        stem = base.slice(0, -"_execution-tree.json".length);
    } else {
        stem = base.replace(/\.json$/i, "");
    }
    return path.join(dir, `${stem}_execution-tree-interactive.md`);
}

function main() {
    const args = process.argv.slice(2);
    if (args.length < 1) {
        console.error("Uso: node generate-execution-tree-md.js <ruta-al-json-de-entrada> [ruta-salida.md]");
        process.exit(1);
    }

    const inputPath = path.resolve(args[0]);
    const outputPath = args[1] ? path.resolve(args[1]) : deriveOutputPath(inputPath);

    if (!fs.existsSync(inputPath)) {
        console.error(`No se encuentra el fichero de entrada: ${inputPath}`);
        process.exit(1);
    }

    const raw = fs.readFileSync(inputPath, "utf-8");
    let root;
    try {
        root = JSON.parse(raw);
    } catch (err) {
        console.error(`Error al parsear el JSON de entrada: ${err.message}`);
        process.exit(1);
    }

    const title = path.basename(inputPath).replace(/_execution-tree\.json$/i, "").replace(/\.json$/i, "");
    const seenIds = new Set();
    const body = renderNode(root, 0, seenIds);
    const md = `# ${title} — Árbol de ejecución (interactivo)\n\n${body}\n`;

    fs.writeFileSync(outputPath, md, "utf-8");
    console.log(`Generado: ${outputPath}`);
}

main();
