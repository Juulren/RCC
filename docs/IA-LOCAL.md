# IA local sin cargos por consulta

RCC admite Ollama como alternativa a OpenAI. No requiere cuenta, tarjeta ni clave. Ejecuta el modelo en el equipo del backend; consume sus recursos y electricidad. Se eligió `qwen2.5:1.5b` para el equipo de desarrollo con unos 8 GB de RAM: descarga aproximada de 986 MB, español/inglés y licencia Apache 2.0. No equivale en calidad a un modelo grande.

## Preparación en Windows

1. Instala [Ollama para Windows](https://ollama.com/download/windows) y ábrelo desde Inicio.
2. Desde la raíz de RCC ejecuta `powershell -ExecutionPolicy Bypass -File .\scripts\configurar-ia-local.ps1`.
3. El script descarga el modelo y configura `OpenAI:Provider=Ollama`, `OpenAI:Model=qwen2.5:1.5b`, `OpenAI:Enabled=true`, 1024 tokens de salida y 20 intentos diarios en user-secrets. El nombre de sección OpenAI se conserva por compatibilidad; con Provider=Ollama no se llama a OpenAI ni se necesita ApiKey.
4. Reinicia la API después de compilar los cambios. En este equipo usa `.\.local\iniciar-rcc.ps1`, que importa también `OpenAI:Provider` al proceso. Otros equipos pueden usar `dotnet run --project src/Rcc.Api` con su conexión PostgreSQL configurada.
5. Inicia sesión, busca disponibilidad y pulsa **Pedir recomendaciones**. Una respuesta correcta devuelve `mode=ai` y `reason=AI_RANKED`; el cálculo sin modelo muestra `mode=deterministic`.

Ollama debe estar abierto en el mismo equipo que C#. La conexión es fija a `http://127.0.0.1:11434/api/generate`, sin proxy ni redirecciones, con salida JSON estructurada y un máximo de 120 segundos. El frontend espera hasta 130 segundos y conserva la cancelación de solicitudes al cambiar fechas o idioma. El modelo se descarga una vez y se descarga de memoria tras cada respuesta para limitar RAM en este equipo.

## Límites y errores

El servidor sigue validando inventario, capacidades y precios antes y después de la llamada. La IA local solo ordena combinaciones; C# redacta las explicaciones a partir de cifras verificadas para evitar detalles inventados por el modelo pequeño. Si Ollama está cerrado, falta el modelo, vence el tiempo o la salida es inválida, se muestran opciones deterministas. El cupo diario existente es compartido entre proveedores y cuenta intentos, incluso fallidos; reiniciar no lo restablece. No se cambia ni se borra ese historial.

La configuración solo admite `qwen2.5:1.5b` con el proveedor Ollama; rechaza etiquetas diferentes, incluidas las de nube. No exponemos Ollama a Internet. Un despliegue en Render **no puede acceder al localhost de tu PC**: para conservar IA al publicar hará falta un servidor con recursos para ejecutar Ollama junto al backend o un proveedor externo. Sin ese recurso el sitio puede usar el cálculo determinista.

OpenAI permanece como proveedor opcional de pago; su configuración existente no se borra. Para desactivar IA configura `OpenAI:Enabled=false` y reinicia. Para volver a OpenAI debes seleccionar conscientemente `OpenAI:Provider=OpenAI` y su modelo/clave.

Referencias: [Windows](https://docs.ollama.com/windows), [API generate y esquema JSON](https://docs.ollama.com/api/generate), [modelo y licencia](https://ollama.com/library/qwen2.5:1.5b).

## Evidencia de implementación

En el equipo de desarrollo se instaló Ollama 0.40.8 y se descargó Qwen 2.5 1.5B. Se ejecutó el gateway C# real contra Ollama: devolvió IDs válidos y colocó primero la combinación con vista al río para la preferencia river. La versión final tardó aproximadamente 9 segundos en esta prueba con dos combinaciones; el tiempo puede variar. Las explicaciones finales se construyeron desde C#, sin texto libre inventado. Esta prueba de transporte no sustituye la demostración final desde la sesión del responsable después de reiniciar la API.

Validación aislada: 12 pruebas Vitest, 29 Playwright de diseño/páginas/servicios, compilación frontend y 5 pruebas del gateway local (incluidas en el conjunto de integración). No se llama a proveedores reales desde CI.
