# Gemini API: nivel gratuito para RCC

Decisión del responsable: 24 de octubre de 2026. Sustituye Ollama/túnel como proveedor del despliegue; los conectores anteriores siguen disponibles para desarrollo. Implementación preparada, **inferencia real pendiente de configurar la clave y probar en Render**.

## 1. Crear la clave sin activar facturación

1. Abre [Google AI Studio](https://aistudio.google.com/api-keys) con tu cuenta Google.
2. Crea un proyecto separado para RCC/Gemini y una clave de API en ese proyecto. No reutilices la clave de Maps ni un proyecto con facturación activada para Maps.
3. Comprueba que el proyecto indique **Free / nivel gratuito**. No actives Billing, Upgrade ni vincules una cuenta de facturación para esta integración. Si te exige pagar o el modelo no tiene cuota gratuita, detente: RCC conserva recomendaciones calculadas sin IA.
4. Si la consola permite restricciones de API, limita la clave a **Generative Language API**. Es una llamada servidor a servidor: no uses restricciones de referentes de navegador como en Maps. No se pide eliminar restricciones a otras claves existentes.
5. Introduce la clave únicamente en Render, nunca en Git, chat, capturas, código React ni variables `VITE_*`.

El código no puede comprobar ni imponer que la cuenta Google sea gratuita: una misma API puede facturarse si el proyecto tiene Billing habilitado. La protección principal contra cargos es mantener el proyecto en Free. El límite diario de RCC reduce el uso, pero no equivale a un límite monetario del proveedor.

## 2. Configurar el servicio existente en Render

En Settings, cambia la rama a **codex/gemini-gratuito** mientras este PR no esté integrado. Conserva Docker y las variables existentes de Neon, certificado, Stripe, Resend y Maps. No crees otro servicio ni reapliques el Blueprint para este cambio.

En Environment agrega:

| Variable | Valor |
|---|---|
| `Gemini__Enabled` | `true` |
| `Gemini__ApiKey` | Clave privada del proyecto Free de AI Studio. |
| `Gemini__Model` | `gemini-3.5-flash-lite` |
| `Gemini__DailyRequestLimit` | `20` |
| `Gemini__MaxOutputTokens` | `1024` |
| `OpenAI__Enabled` | `false` |

Elimina de Render `OpenAI__RelayUrl` y `OpenAI__RelayToken` si los agregaste. No modifiques las claves de otros servicios. Guarda y despliega la nueva rama. La PC, Ollama y el túnel dejan de ser necesarios para Gemini; detén los procesos del puente/túnel que hayas abierto al terminar la transición.

La sección `Gemini` tiene prioridad sobre la antigua sección `OpenAI`, incluso si está desactivada o incompleta. Un error no habilita Ollama ni OpenAI de pago. Para desactivar toda IA durante esta transición basta `Gemini__Enabled=false`, conservando esa sección y `OpenAI__Enabled=false`.

Para desarrollo local, las mismas propiedades se guardan como `Gemini:...` en user-secrets mediante entrada privada. No escribas claves literales en comandos que queden en el historial. No hace falta configurar la clave local para usar el sitio publicado.

## 3. Verificar la intervención real

1. Con sesión iniciada en la URL de Render, abre Reservar, selecciona fechas futuras, huéspedes y preferencias, y pulsa **Pedir recomendaciones**.
2. Debe aparecer la indicación de opciones ordenadas con IA. En DevTools → Network, la respuesta de `POST /api/recommendations` debe contener `mode: "ai"` y `reason: "AI_RANKED"`. Ver la lista de habitaciones por sí sola no demuestra una llamada al modelo.
3. Con cuota agotada, timeout, error de proveedor o respuesta inválida, se conserva la lista validada por C# y se informa que no intervino IA: `mode: "deterministic"`. `AI_DAILY_LIMIT` corresponde al cupo local; El diagnóstico distingue `AI_PROVIDER_QUOTA` (429), `AI_PROVIDER_AUTH` (401/403), `AI_MODEL_UNAVAILABLE` (404), `AI_PROVIDER_REQUEST` (400), `AI_PROVIDER_TIMEOUT` y `AI_INVALID_RESPONSE`. `AI_UNAVAILABLE` queda para otros fallos de conexión/proveedor. No incluye el cuerpo privado del error. Consulta el uso/cuota en AI Studio sin publicar credenciales.
4. Precios, capacidad, fechas, cotización y confirmación de reserva siguen comprobándose en el servidor. La IA no reserva ni cambia tarifas.

El contador diario ya existente en PostgreSQL es compartido por todos los proveedores/instancias y se reinicia al cambiar el día UTC. Los intentos fallidos también consumen ese cupo; cambiar de Ollama a Gemini no borra el uso del día. Las cuotas de Google son independientes y pueden ser inferiores. No hay reintentos automáticos ni cambio de modelo al recibir 429.

## Privacidad y límites

Se envían solo identificadores temporales `option-N`, precio calculado, cantidad de habitaciones, vistas al río, superficie y preferencias enumeradas. No se envían nombres de clientes/habitaciones, correos, identificadores de usuario, fechas, edades, datos de pago ni credenciales dentro del prompt. Google recibe la clave en una cabecera HTTPS, con redirecciones desactivadas y cabecera censurada en logs del cliente HTTP.

Gemini devuelve únicamente IDs. RCC rechaza opciones inventadas, duplicadas, respuestas truncadas y errores; redacta las explicaciones ES/EN con cifras verificadas. Límite de salida 1024 tokens por defecto, respuesta HTTP máxima 64 KiB y timeout 15 segundos. No se habilitan búsqueda, Maps grounding, herramientas, archivos, caché ni servicios adicionales de IA.

El nivel gratuito está disponible para ciertos modelos y tiene cuotas variables. Google indica que el contenido Free puede utilizarse para mejorar sus productos. Revisa los precios y las condiciones antes de cambiar modelo/plan; no se garantiza gratuidad futura ni disponibilidad ilimitada.

Referencias oficiales verificadas el 24 de octubre de 2026: [precios](https://ai.google.dev/gemini-api/docs/pricing), [cuotas](https://ai.google.dev/gemini-api/docs/rate-limits), [generación REST](https://ai.google.dev/api/generate-content), [salida estructurada](https://ai.google.dev/gemini-api/docs/structured-output).

## Evidencia de implementación

Pruebas locales aisladas: **52 aprobadas** de gateways y puente, incluidas 19 de Gemini (ES/EN, solicitud privada y esquema, errores 429/403/503 sin reintento, modelo no permitido, respuesta truncada/inventada/duplicada/vacía, tamaño máximo y prioridad de configuración). Proveedores simulados, sin llamadas reales ni clave de Google. La CI ejecuta la matriz completa con PostgreSQL; no se atribuye esa validación a la ejecución local sin base.

## Diagnóstico del primer despliegue

Si el panel indica cuota del proveedor, comprueba los límites activos del modelo en AI Studio. Un 429 puede corresponder a límites por minuto o por día, o a cuota gratuita no disponible para ese proyecto; no se soluciona cambiando a un proveedor de pago ni aumentando el cupo local de RCC. Si el mensaje es de acceso, revisa que Render tenga la clave del mismo proyecto, sin espacios/comillas, y que sus restricciones permitan Generative Language API. Si aparece solicitud rechazada, comprueba clave/configuración del modelo y disponibilidad del nivel Free para el proyecto/región. No actives Billing para intentar resolverlo sin revisar los costos.

Estos códigos fijos mantienen la alternativa determinista. No se devuelven mensajes crudos de Google, prompts, claves ni datos personales. La clasificación HTTP está cubierta en pruebas de integración con proveedores simulados y los avisos del panel en ES/EN.

## Preferencias comprensibles

El panel muestra noches, número de habitaciones y huéspedes de la búsqueda real. El presupuesto se aplica al total de todas las habitaciones/noches en CNY (yuan chino); al introducirlo se muestra el promedio máximo por habitación y noche, aclarando que no es una tarifa ofrecida. Sin presupuesto no se añade un máximo; sin prioridades el cálculo determinista favorece menor precio.

Vista al río prioriza más habitaciones con esa vista; más espacio, la suma de m²; menor precio, el total de la estancia. Son preferencias de orden y no filtros obligatorios. Capacidad, inventario, fechas y presupuesto se validan como antes. Se conservan las tres preferencias enumeradas; no se añaden servicios o características que el catálogo no pueda comprobar.

Validación del ajuste: 19 pruebas frontend y seis pruebas de navegador con proveedores simulados (360/768/1440, ES/EN, teclado, movimiento reducido, diagnóstico de cuota y respuesta ordenada). Las 52 pruebas aisladas de conectores/puente también pasaron. Ocho casos adicionales de diagnóstico en PostgreSQL quedan a CI porque Docker local no arrancó; no se volvió a intentar abrirlo después del reporte del responsable.

## Corrección de disponibilidad del modelo

El primer despliegue reportó `AI_MODEL_UNAVAILABLE` (HTTP 404). La documentación de Google limita los modelos 2.5 a proyectos con uso anterior y recomienda 3.5 Flash-Lite/3.8 Flash para proyectos nuevos. RCC se actualiza a **gemini-3.5-flash-lite**, cuya tabla de precios mantiene entrada/salida Free. En Render debe cambiarse explícitamente `Gemini__Model` a ese valor y desplegar la versión corregida. La clave y el proyecto Free no cambian.

Se eliminan los parámetros heredados temperature y thinkingBudget=0; se usa el razonamiento mínimo predeterminado de 3.5 Flash-Lite, conservando el máximo de salida, esquema JSON, cuota y validación. El modelo anterior ya no habilita llamadas; no hay migración/fallback silencioso entre modelos. La confirmación real sigue pendiente de una respuesta `AI_RANKED` en el proyecto del responsable.

Fuentes: [disponibilidad de modelos](https://ai.google.dev/gemini-api/docs/deprecations), [3.5 Flash-Lite](https://ai.google.dev/gemini-api/docs/models/gemini-3.5-flash-lite), [precios](https://ai.google.dev/gemini-api/docs/pricing), [razonamiento](https://ai.google.dev/gemini-api/docs/thinking).

Validación local de esta corrección: 53 pruebas de gateways/puente aprobadas, incluido rechazo explícito del modelo 2.5 anterior y comprobación de la URL y parámetros de 3.5. No se usó una clave real ni se realizó inferencia externa.
