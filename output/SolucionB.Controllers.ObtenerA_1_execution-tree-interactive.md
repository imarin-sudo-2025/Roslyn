# SolucionB.Controllers.ObtenerA_1 — Árbol de ejecución (interactivo)

<details style="margin:0;padding:0 0 4px 0;margin-left:0px">
<summary>ServicioLocalController.ObtenerA [9dd9b9bb-d11a-450a-a673-e54b46f053ff]</summary>
<details style="margin:0;padding:0 0 4px 0;margin-left:20px">
<summary>Code</summary>
<pre style="margin:4px 0"><code class="language-csharp">        [HttpGet("obtenera")]
        public async Task&lt;ActionResult&lt;ModeloA&gt;&gt; ObtenerA(int id)
        {
            var resultado = await _servicioLocal.ObtenerModeloAAsync(id);
            return Ok(resultado);
        }</code></pre>
</details>
</details>
