# Módulo 8: recomendaciones de habitaciones

Fecha: 2026-10-20. Rama `codex/modulo-8-recomendaciones`, apilada sobre módulo 7. Responsable único: Juulren. Código y pruebas asistidos por IA; no se inventa revisión humana independiente.

## Comportamiento

En Reservar, después de buscar disponibilidad, el usuario autenticado puede pedir combinaciones para sus fechas y distribución de adultos/niños por habitación. Presupuesto opcional: **total de todas las habitaciones y noches, en CNY**. Preferencias suaves: vista al río, más espacio y menor precio. Una preferencia no garantiza una característica ni ignora el presupuesto.

C# valida los mismos límites de fechas, número de habitaciones, edades y ocupación que reservas. Obtiene tipos activos y habitaciones físicas libres, cuenta las selecciones repetidas contra inventario compartido y descarta combinaciones que exceden el presupuesto. La búsqueda mantiene hasta 48 caminos parciales (24 por menor precio y 24 por puntuación de preferencias), selecciona hasta 12 candidatos y devuelve hasta tres opciones. Es una búsqueda acotada: no promete enumeración exhaustiva ni óptimo global. Sin coincidencias no llama a OpenAI.

Cuando OpenAI está habilitado, recibe solo IDs efímeros y cifras de candidatos: total, número de habitaciones, habitaciones con vista y área sumada, más preferencias enumeradas e idioma. No salen identidad, email, fechas, edades, UUID de habitaciones, nombres del catálogo, contraseñas ni datos de pago. No hay campo libre ni herramientas ejecutables para el modelo.

OpenAI ordena candidatos y explica sus cifras mediante Responses API y JSON Schema estricto. C# rechaza IDs ajenos/repetidos, explicaciones vacías/demasiado largas/con caracteres de control, rechazos e incompletos. Reconsulta la cotización de cada candidato seleccionado al volver de la llamada; descarta inventario o precios cambiados. Las explicaciones siguen siendo texto de IA y pueden contener errores; no son la fuente de precios, características o disponibilidad. React renderiza texto sin HTML.

Sin configuración, ante límite diario, fallo HTTP/timeout o salida inválida se usa cálculo local explícitamente marcado **No intervino IA**. No hay respuestas artificiales presentadas como IA real. Las sugerencias no bloquean stock ni crean reservas. **Usar esta combinación** únicamente selecciona tipos en el borrador; el usuario debe cotizar, aceptar el precio y reservar mediante el flujo existente. La transacción final vuelve a verificar precio/inventario.

Los resultados se invalidan al cambiar fechas, huéspedes, presupuesto, preferencias, idioma o sesión. Se abortan solicitudes anteriores para no aplicar una respuesta a otra estancia. Cambiar manualmente tipos mantiene el borrador; escoger una sugerencia nunca acepta la cotización por el usuario.

## API y límites

`POST /api/recommendations`: sesión Customer/Admin, CSRF y no-store. Entrada: arrival, departure, rooms[{adults,childAges}], budget nullable, preferences[] (river/space/value), locale es/en. Se ignoran IDs de tipo enviados por el cliente al generar opciones; el servidor calcula candidatos.

Respuesta: mode (ai/deterministic), reason, options[{id, quote, explanation}], currency CNY, isDemo=true. quote conserva el contrato de cotización del módulo 5 y precios decimales C#.

| reason | Significado |
|---|---|
| AI_RANKED | Orden y explicación de proveedor, con opciones verificadas. |
| AI_NOT_CONFIGURED | Servicio desactivado o configuración incompleta/inválida. |
| AI_DAILY_LIMIT | Cupo diario consumido; cálculo local. |
| AI_UNAVAILABLE | Error de proveedor/red/timeout; cálculo local. |
| AI_INVALID_RESPONSE | IDs/explicaciones no válidos; cálculo local. |
| NO_MATCH | Búsqueda acotada sin candidatos para los criterios; no se llama al proveedor. |
| AVAILABILITY_CHANGED | Ningún candidato elegido sigue siendo válido al reconsultar. |

Límite por usuario: tres peticiones/minuto por instancia, sin cola; incluye rechazos CSRF que alcanzan el endpoint. Cupo de llamadas externas: 20 intentos/día UTC por defecto, configurable entre 1 y 500. Tabla `AiDailyUsages` con PK Day y Requests; INSERT ON CONFLICT condicional atómico comparte límite entre instancias de la misma base y lo conserva tras reiniciar. Cuenta fallos y cancelaciones tras tomar cupo, sin devolverlo ni reintentar automáticamente. No es un límite monetario en dólares.

HTTP OpenAI: 10 segundos, respuesta máxima 64 KiB y salida configurada entre 256 y 2048 tokens (1024 por defecto). Una sola llamada por consulta. `store=false`, sin conversaciones, archivos, herramientas, búsquedas externas ni historial persistido por RCC. Esto no promete ausencia de toda retención del proveedor: aplican sus políticas y configuración de cuenta.

## Configuración pendiente

OpenAI está desactivado por defecto y **no se fija un modelo automáticamente**. El responsable configurará clave privada, modelo compatible con Responses + Structured Outputs y límites, y comprobará una llamada real después. Se usó la habilidad OpenAI Docs y documentación oficial para el contrato HTTP. Las pruebas automáticas simulan el transporte; no acreditan una inferencia externa real.

Guía completa de activación: [CONFIGURACION-SERVICIOS.md](CONFIGURACION-SERVICIOS.md). Se conservan pendientes reales de Stripe sandbox, Maps Embed y Resend de módulos previos. No se inicia el despliegue del módulo 9.

## Pruebas y demostración

- .NET/PostgreSQL: capacidad, presupuesto total, tres habitaciones con stock compartido, preferencias, pendientes/confirmadas/vencidas, configuración ausente, sin candidatos, fallo, IDs inventados/duplicados, revalidación al cambiar inventario, cupo diario concurrente y permisos/CSRF/rate limit.
- Transporte HTTP simulado: Responses con modelo configurable, store=false, tokens y esquema; datos privados ausentes; rechazo/incompleto/429 no se interpretan como éxito.
- React: sesión requerida, texto HTML tratado como texto, selección sin reserva automática, respuesta vieja descartada y recuperación de 429.
- Navegador real: 1440 ES y 360 EN, presupuesto insuficiente/suficiente, dos habitaciones, cálculo local marcado, aplicar selección, cotizar y mantener cero reservas hasta confirmación explícita. Se conservan regresiones de diseño, autenticación, catálogo y pagos.

La demostración local no requiere claves: iniciar sesión, abrir Reservar, buscar dos habitaciones para dos noches, pedir recomendaciones con presupuesto total 3600 CNY, elegir y cotizar. Los datos son de demostración. Para evidenciar IA externa se habilitará un modelo y se exigirá mode=ai, revisión manual de la explicación y consumo visible en el proyecto OpenAI.

Fuentes oficiales consultadas: [Structured Outputs](https://developers.openai.com/api/docs/guides/structured-outputs?api-mode=responses), [Quickstart](https://developers.openai.com/api/docs/quickstart) y [Spend limits](https://developers.openai.com/api/docs/guides/spend-limits).


Matriz: 119 pruebas .NET/PostgreSQL/transporte, 11 Vitest, 29 Playwright de diseño/servicios, 3 autenticación, 2 catálogo, 6 reservas/recomendaciones y 3 pagos aislados: **173 comprobaciones**. Se revisan capturas 1440 ES y 360 EN y se aplica migración sin cambios pendientes. Las nuevas pruebas de recomendaciones reutilizan la API y PostgreSQL reales; el proveedor OpenAI se simula únicamente en pruebas de transporte/ranking. No se ha realizado una inferencia externa real.
