# Módulo 6: pagos de prueba y reembolsos

Fecha local: 2026-10-20. Responsable único: Juulren. Rama
`codex/modulo-6-pagos`, basada en módulo 5. Código generado con asistencia de
Codex; revisión humana e integración pendientes.

## Estado y alcance

Implementación C# de Stripe Checkout alojado, validación de webhook, confirmación,
cancelación de confirmadas, expiración remota, conciliación y reembolsos durables.
Interfaz ES/EN para iniciar pago, consultar el estado verificado y seguir el
reembolso. Se usa el SDK oficial **Stripe.net 53.0.0**, fijado en el proyecto.

El responsable indicó que **aún no tiene sandbox Stripe**. No se han creado una
cuenta, claves, cobros ni reembolsos en Stripe real/sandbox. La aplicación normal
mantiene los pagos desactivados mientras falte configuración. Las pruebas usan
PostgreSQL real y un proveedor aislado; no acreditan una integración externa
operativa. La validación controlada con Stripe queda pendiente y se describe en
[STRIPE-SANDBOX.md](STRIPE-SANDBOX.md). No avanzar a otros módulos por esta entrega.

## Flujo y contratos

| Ruta bajo `/api` | Acceso y respuesta |
|---|---|
| GET `/payments/status` | Público: `{available, mode:"test"}`; nunca claves o configuración privada. |
| POST `/reservations/{id}/checkout` | Propietario, Customer/Admin, CSRF e Idempotency-Key; `{status,url,expiresAt,mode}`. |
| POST `/payments/stripe/webhook` | Sin cookie/CSRF; firma Stripe sobre cuerpo original, tolerancia 300 segundos. |
| GET `/reservations`, `/{id}` | Estado de reserva, pago y reembolso, sin URL/ID de sesión, PI o claves. |
| POST `/reservations/{id}/cancel` | Cancela pendiente/confirmada antes de entrada; programa reembolso de pago verificado. |
| GET `/admin/reservations`, `/{id}` | Estados y errores saneados de conciliación/reembolso, filtros y paginación existentes. |

Checkout no acepta precio, moneda ni URLs del navegador. La solicitud no tiene
campos de negocio en el cuerpo: el contenido idempotente es el ID de reserva
propia. Misma clave para otra reserva devuelve 409. Repetir, incluso con una
clave nueva, reutiliza el único Payment/sesión canónicos de esa reserva.
El importe se deriva del total histórico C# en centavos CNY, sin flotantes.
Máximo Stripe adoptado: 99,999,999 centavos (999,999.99 CNY); importes superiores
reciben `PAYMENT_AMOUNT_LIMIT`, sin llamada externa.

El backend persiste Payment, origen de retorno, expiración solicitada y clave
cliente antes de contactar con Stripe. La clave externa `rcc-checkout-{paymentId}`
y los parámetros permanecen estables tras fallos/reinicio. No se mantienen
transacciones ni bloqueos SQL durante llamadas de red. Timeout por llamada: 10 s;
el SDK no repite automáticamente; las operaciones durables gestionan reintentos.

Solo `sk_test_`, firma `whsec_` y origen HTTPS (HTTP únicamente loopback) activan
pagos. No se admite modo live ni claves `sk_live_`. Origen sin ruta, credenciales,
query ni fragmento; nunca se deriva del Host enviado por el usuario. El servidor
y el cliente solo permiten redirección a HTTPS `checkout.stripe.com`.
Tarjeta alojada por Stripe; RCC no almacena PAN/CVC ni envía edades o contraseñas.
Se desactivan Adaptive Pricing, impuestos automáticos y promociones para conservar
el importe CNY de la cotización, y se permiten únicamente métodos `card`.

## Ajuste necesario del vencimiento

La especificación inicial pedía alinear exactamente el Checkout con el bloqueo
de 30 minutos. Stripe exige que `expires_at` esté **al menos 30 minutos después
de crear la sesión**, por lo que un Checkout abierto después de reservar ya no
puede representar exactamente el vencimiento restante. Se informó este conflicto
al responsable durante implementación.

Se mantiene la regla de inventario: **30 minutos desde crear la reserva, sin
prórroga**. Stripe recibe una expiración fija de 31 minutos desde preparar el
primer intento (un minuto de margen para la petición); nunca se renueva al
reintentar. No se inicia un Checkout si queda menos de un minuto de bloqueo.
Al cancelar/vencer se solicita expiración remota mediante conciliación cada
20 segundos, con reintentos si Stripe falla. Puede existir una ventana en que
Stripe todavía acepte un pago tras liberar inventario; C# siempre lo clasifica
como tardío y programa reembolso, sin reasignar habitaciones ni confirmar.
Este ajuste sustituye la alineación externa exacta, que no se promete.

## Verificación y transiciones

El regreso a `/mis-reservas?...&pago=retorno` solo muestra un aviso y consulta
estado: no confirma nada. La comprobación se realiza mediante webhook firmado
o conciliación autenticada desde C#, consultando la sesión y expandiendo su
PaymentIntent en Stripe.

Se verifican: modo test en sesión/intent, sesión `cs_test_`, modo `payment`,
importe y moneda, metadata de reserva/pago, client_reference_id, vínculo con la
sesión canónica y PaymentIntent `succeeded` con amount_received exacto CNY.
Un desacuerdo queda `Review` y no confirma. Se rechaza firma inválida, evento
live, timestamp fuera de tolerancia o cuerpo excesivo. No se conserva el payload
original de Stripe; solo ID, tipo, resultado saneado, hora y vínculo local.

Eventos de Checkout admitidos: completed, async_payment_succeeded,
async_payment_failed y expired. Eventos ajenos/desconocidos se registran como
Ignored. Eventos repetidos por ID se consumen una vez; eventos distintos sobre
el mismo pago tampoco duplican confirmación/reembolso. Si Stripe no permite
consultar la sesión, se devuelve 503 sin consumir el evento, para reintento.
Un intento rechazado no cancela anticipadamente el bloqueo; se puede reintentar
la misma sesión. La lectura actual de Stripe es la fuente de estado, no el tipo
de un evento antiguo. Snapshots antiguos no degradan un pago ya verificado.

Todos los cambios de reserva/pago usan el bloqueo transaccional por hotel ya
introducido en módulo 5. Un pago vigente confirma si la reserva sigue pendiente;
si cancelación o vencimiento ganó la carrera, solo registra el pago y programa
reembolso. Las reservas confirmadas siguen ocupando aunque expiresAt sea pasado.
Una sesión verificada expirada vence una pendiente; no revierte una confirmada.

## Reembolsos y recuperación

Cancelar una confirmada antes del día de entrada, hora de Chongqing, cambia el
estado a Cancelled, libera inventario y crea RefundJob en la misma transacción.
Repetir cancelación es idempotente. Cancelar no promete reembolso ya completado.
Una confirmada sin pago verificable se bloquea para revisión, sin inventar dinero.

Cada pago tiene como máximo un trabajo de reembolso total. El worker reclama
una lease SQL de dos minutos antes de la llamada y usa `rcc-refund-{jobId}` como
clave externa. Fallos de red se reintentan con backoff hasta una hora; una
respuesta perdida puede recuperarse sin crear un segundo reembolso. Si Stripe
devuelve pending, se consulta el ID existente hasta resolverlo. Succeeded exige
ID/vínculo/importe/moneda correctos. Failed/canceled y discrepancias se muestran
al administrador para revisión; nunca reactivan la reserva.

No se repiten creaciones de resultado desconocido después de 23 horas, para no
arriesgar duplicados cuando el proveedor pueda retirar sus claves idempotentes.
Se marca Review con error estable. Un trabajo con ID externo conocido puede
seguir consultándose. Revisión manual mediante Dashboard y metadata/filas
locales; no hay botón de nuevo reembolso automático para un resultado ambiguo.

La conciliación revisa hasta 20 pagos y 20 reembolsos por ciclo de 20 segundos,
sin llamadas de red dentro de transacciones. Consulta pagos recientes durante
24 h y trabajos de creación/abiertos no resueltos; los eventos de Stripe siguen
admitiéndose después. No se detectan por sondeo todas las operaciones manuales
ajenas a RCC realizadas en el Dashboard (por ejemplo, reembolsar externamente una
confirmada). La guía solicita efectuar cancelaciones desde RCC en esta versión.
Correos de confirmación/cancelación corresponden al módulo 7.

## Datos y pruebas

Migración AddTestPayments: Payments (único por reserva, sesión y PI), PaymentEvents
(ID único) y RefundJobs (uno por pago, ID externo único), con FKs RESTRICT,
checks de estado/moneda/importes e índices de trabajo pendiente.

84 pruebas .NET/PostgreSQL y de transporte SDK: permisos, CSRF, configuración,
importes, clave estable, duplicados simultáneos, firmas, replay, eventos live,
errores de importe/moneda/referencia/intent, fallo/reintento, confirmación,
cancelación/reembolso, pérdida de respuesta, conciliación sin webhook,
concurrencia, pago tardío tras revender la misma unidad, snapshots antiguos,
límites de tiempo/importe y revisión tras ventana idempotente.
También se cubre un webhook recibido antes de guardar la respuesta de Checkout:
se vincula mediante metadata persistida y una respuesta antigua no intenta
expirar una sesión ya pagada.

Regresiones: 7 Vitest, 21 de diseño, 3 recorridos de autenticación real, 2 de
catálogo y 4 de reservas. Con los 3 recorridos de pagos simulados y las 84 pruebas
.NET, la matriz contiene **124 comprobaciones**. Release sin advertencias y
modelo EF sin cambios pendientes. Los reportes/capturas locales son generados
e ignorados por Git; CI conserva sus artefactos.

Playwright de pagos ejecuta tres recorridos ES/EN a 360/1440 px con PostgreSQL y
RCC reales, pero **Stripe simulado**. Un ejecutable separado
`tests/Rcc.PaymentHarness` sustituye únicamente IStripeGateway y emite callbacks
firmados al webhook real. El navegador intercepta la página externa de Checkout
con una página identificada como simulación. No solicita números de tarjeta.
Los controles de prueba exigen token aleatorio y escuchan solo en loopback;
no forman parte del API publicado. Sus workers se controlan explícitamente para
que los escenarios sean deterministas; la lógica del worker se prueba en .NET.

```powershell
dotnet build RCC.sln --configuration Release
node scripts/build-web.mjs
# RCC_TEST_POSTGRES: instancia de pruebas con permiso CREATEDB.
dotnet test RCC.sln --configuration Release --no-build
npm test --prefix frontend
npm run test:browser --prefix frontend
./scripts/test-payments.ps1
```

El script crea/migra una base `rcc_e2e_<UUID>` y administrador efímeros, ejecuta el
harness en 5084 y elimina solo esa base al terminar. Para el resto de regresiones
se conservan test:auth, test:catalog y test:reservations. CI ejecuta todos los
recorridos y conserva reportes. Ninguna clave Stripe real se utiliza en CI.

Durante pruebas se inició Docker Desktop, que estaba detenido. Se corrigió el
aislamiento de datos monetarios entre escenarios: cada cuenta de proveedor
simulada necesita su propia cola, evitando que un worker global procese trabajos
dejados por un escenario anterior. Las tablas solo se limpian en bases aleatorias
creadas por el fixture, verificadas por nombre; nunca en la base del responsable.

Referencias oficiales:
[Checkout y expires_at](https://docs.stripe.com/api/checkout/sessions/create),
[webhooks](https://docs.stripe.com/webhooks),
[reembolsos](https://docs.stripe.com/api/refunds/create),
[idempotencia](https://docs.stripe.com/api/idempotent_requests),
[límites de moneda](https://docs.stripe.com/currencies),
[SDK fijado](https://github.com/stripe/stripe-dotnet/tree/v53.0.0).
