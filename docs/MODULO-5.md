# Módulo 5: disponibilidad, cotización y reservas

Fecha local: 2026-10-19. Responsable único: Juulren. Implementación asistida por
Codex, rama `codex/modulo-5-reservas`, basada en el módulo 4. Sin integración
automática de PR ni atribución de revisión humana.

## Comportamiento entregado

`/reservar` permite elegir fechas compartidas, añadir/quitar habitaciones,
distribuir adultos y edades de niños y consultar opciones del inventario
PostgreSQL. Las tarjetas conservan las fotografías acreditadas del catálogo,
tarifas CNY y selección por teclado. Hay estados de carga, error, ausencia de
opciones y nueva aceptación de tarifa. Idioma ES/EN y movimiento reducido.

La cotización muestra noches, ocupación y precio por línea y total, sin cargos
adicionales. Solo el servidor calcula importes. Buscar y cotizar no bloquean
inventario. Después de aceptar la cotización e iniciar sesión se crea una
reserva pendiente; cada habitación física se asigna en la misma transacción.
Una selección que excede el stock compartido recibe 409, sin reserva parcial.

El home conserva su composición y resumen orientativo, con un enlace al nuevo
buscador que transfiere fechas y totales de huéspedes. Allí se revisa la
distribución y las edades (inicialmente 0 cuando solo se conocía el número de
niños). Las tarjetas del catálogo enlazan al buscador con el tipo preseleccionado.
Ninguno de los conceptos ilustrativos del home sustituye la cotización de la API.

`/mis-reservas` muestra exclusivamente reservas del usuario, detalle enlazable,
estados y vencimiento en Chongqing, paginación y cancelación con confirmación.
Consulta de nuevo cada 30 segundos y al volver a la pestaña. `/cuenta` enlaza a
las reservas y permite continuar el borrador; `/admin` incorpora lectura de las
reservas del hotel con filtros de estado y fecha de llegada y paginación.

El borrador de fechas/distribución y el intento de reserva se guardan en
`sessionStorage`, sin cookies, credenciales ni datos de pago. Una respuesta
perdida se reintenta con la misma clave y contenido. La clave se elimina tras
recibir éxito, permitiendo después una nueva reserva intencional. Sin almacenamiento
del navegador, la misma página conserva el intento en memoria.

## Reglas y límites de esta versión

- Entrada inclusiva y salida exclusiva; fechas de Chongqing (`Asia/Shanghai`,
  UTC+8). El backend recibe `DateOnly`; instantes técnicos UTC.
- Entre 1 y 8 habitaciones, 1–30 noches y llegada entre hoy y los próximos
  365 días. Son límites operativos del proyecto académico, no del hotel real.
- Un adulto como mínimo por habitación; adultos >=18, niños 0–17. Se validan
  capacidad total y máximos de adultos/niños del tipo activo; edades sin suplemento.
- Tipos y habitaciones físicas deben estar activos. Las opciones por selección
  comparten stock: la cotización y creación validan la combinación completa.
- Nombres ES/EN, tarifa y ocupación se conservan en los detalles históricos.
  Editar el catálogo después no reescribe las reservas.
- El fingerprint SHA-256 representa fechas, distribución, tipos y tarifas
  decimales normalizadas. Al crear se recalcula todo; cambiar tarifa devuelve
  `PRICE_CHANGED` con la nueva cotización y exige aceptación explícita.
- Pendientes bloquean exactamente 30 minutos desde creación. No se prorrogan
  por reintento. A partir del vencimiento se ignoran inmediatamente en consultas,
  aunque el proceso periódico aún no haya persistido `Expired`.
- Un servicio cada minuto persiste vencimientos; fallos se reintentan al siguiente
  ciclo. Tras reiniciar, la consulta sigue ignorando bloqueos vencidos.
- Cancelar pendiente antes del día local de entrada libera todas sus habitaciones.
  Repetir cancelación devuelve el mismo estado. Cambiar fechas requiere cancelar
  y reservar otra vez. Una reserva ya vencida no se cancela ni se reactiva.
- Con reservas vigentes cuya salida es posterior a hoy se impiden desactivación
  del tipo, desactivación/renumeración de su unidad. Tipo físico sigue inmutable.
  Cambios de tarifa/capacidad no alteran historia; nuevas reservas se revalidan.
- Pagos, confirmación verificada, reembolsos, webhook y checkout corresponden al
  módulo 6. Esta entrega solo puede crear `PendingPayment`; no ofrece un botón
  para confirmar ni simula pagos. Una fila `Confirmed`, si existe por migración
  o prueba, ocupa inventario y su cancelación devuelve `PAYMENT_MODULE_REQUIRED`
  hasta incorporar el trabajo durable de reembolso en el módulo 6.

## Transacciones e idempotencia

PostgreSQL en READ COMMITTED. Para este único hotel las mutaciones de inventario
adquieren primero un bloqueo transaccional de la fila del hotel; creación bloquea
además las habitaciones candidatas en orden UUID estable (`FOR UPDATE`) antes
de consultar solapamientos. Se vuelve a leer catálogo y ocupación, se asignan
unidades distintas y se insertan reserva, detalles y clave juntos. Cualquier
rechazo revierte toda la transacción. El bloqueo se comparte con cambios de
tipo/unidad, cancelaciones y expiraciones; no depende de una sola instancia C#.

El bloqueo por hotel es deliberadamente conservador para 14 unidades de
demostración: serializa las escrituras cortas del hotel. No se hacen operaciones
externas dentro de la transacción. Para mayor volumen se podrá reducir el alcance
manteniendo un protocolo uniforme. Referencia técnica:
[bloqueos de filas en PostgreSQL 17](https://www.postgresql.org/docs/17/explicit-locking.html).

`Idempotency-Key`: 16–100 caracteres ASCII alfanuméricos, guion o guion bajo.
La combinación usuario/operación/clave tiene índice único; conserva hash del
contenido y referencia al recurso. Mismo contenido recupera la reserva (200),
incluso cancelada/vencida; contenido distinto recibe `IDEMPOTENCY_CONFLICT`.
Primer éxito: 201. Para crear otra reserva se utiliza una nueva clave. Las claves
persisten mientras exista la historia; no se purgan en esta entrega.

## API entregada

Todas las rutas bajo `/api`, respuestas sin caché. `rooms` usa
`[{ adults, childAges, roomTypeId }]`; disponibilidad no requiere `roomTypeId`.

| Método y ruta | Acceso / comportamiento |
|---|---|
| POST `/availability` | Público, lectura: fechas/distribución → opciones por habitación y stock compartido. |
| POST `/quotes` | Público, lectura: selección → noches, líneas, total CNY, fingerprint. |
| POST `/reservations` | Customer o Admin, CSRF y clave idempotente; selección, fingerprint y `locale` es/en. |
| GET `/reservations` | Sesión: lista propia, page/pageSize, máximo 100. |
| GET `/reservations/{id}` | Solo propietario; ajenas devuelven 404. |
| POST `/reservations/{id}/cancel` | Propietario + CSRF; cancelación de pendientes. |
| GET `/admin/reservations` | Admin, filtros status/arrivalFrom/arrivalTo (llegada inclusiva), page/pageSize. |
| GET `/admin/reservations/{id}` | Admin, lectura del detalle. |

No se exponen números físicos, email de otros usuarios ni secretos. El rol Admin
no puede cancelar la reserva ajena mediante el endpoint del propietario. POST
de disponibilidad/cotización solo leen; las escrituras autenticadas exigen CSRF.
Errores: `VALIDATION_FAILED`, `CAPACITY_EXCEEDED`, `ROOM_UNAVAILABLE`,
`PRICE_CHANGED`, `IDEMPOTENCY_KEY_REQUIRED`, `IDEMPOTENCY_CONFLICT`,
`RESERVATION_EXPIRED`, `CANCELLATION_CLOSED`, `INVENTORY_COMMITTED`.

Migración `AddReservations`: Reservations, ReservationItems e IdempotencyRequests,
con FKs RESTRICT, CHECK de fechas/estado/importes/edades e índices de propietario,
estado/vencimiento y habitación. Los modelos reservan `Confirmed` para pagos,
sin crear pagos o notificaciones ficticias.

## Verificación y reproducción

Validación local: **90 comprobaciones**: 53 xUnit/PostgreSQL, 7 Vitest, 21
Playwright de diseño, 3 de autenticación real, 2 de catálogo real y 4 de reservas
reales. Compilación Release sin advertencias, producción Vite con rutas separadas,
migración local aplicada y sin cambios de modelo pendientes. Los reportes se
generan en `artifacts/test-results` y `frontend/test-results-*` (ignorados por Git).
Una primera ejecución de autenticación móvil excedió la espera mientras había
otras pruebas simultáneas; el caso se ejecutó de nuevo aisladamente y pasó, sin
alterar sus esperas ni saltarse verificaciones. El resto del recorrido pasó.

Pruebas PostgreSQL: fechas locales y límites, capacidad, tarifas decimales,
varias habitaciones, stock compartido, intervalos adyacentes, permisos/propiedad,
CSRF, repetición y conflicto idempotente, dos compradores por el último cupo,
duplicados simultáneos, actualización de precios, historia, vencimiento exacto,
cancelación, inventario comprometido y consulta administrativa.

Playwright real: reserva de dos habitaciones y niños, recarga, cancelación y
liberación a 360/768/1440 px, ES/EN; pérdida de respuesta después del commit,
reintento sin duplicados; tarifa cambiada con nueva aceptación; sesión cerrada
antes de crear. Sin mocks de disponibilidad, cotización o creación (la prueba
de respuesta perdida corta solo la respuesta después de ejecutar la API real).

```powershell
node scripts/build-web.mjs
dotnet build RCC.sln --configuration Release
# RCC_TEST_POSTGRES apunta a un servidor de pruebas con CREATEDB.
dotnet test RCC.sln --configuration Release --no-build
npm test --prefix frontend
npm run test:browser --prefix frontend
./scripts/test-reservations.ps1
```

El último script usa `.env` local, genera una base y administrador efímeros,
migra, inicia servidor oculto en 5083, ejecuta los cuatro recorridos y limpia
solo la base aleatoria creada por él. La base del usuario y su administrador
no se modifican. `scripts/test-catalog.ps1` conserva su ejecución por defecto
y admite `-Suite reservations`. CI ejecuta también los recorridos reales de
autenticación y catálogo, conserva reportes y comprueba migraciones/publicación.

Correcciones durante validación: los selectores de prueba esperan la respuesta
de disponibilidad antes de enumerar opciones y las tarjetas radio tienen nombre
accesible explícito. El reloj de la prueba de cancelación se fija antes del login
para no vencer también la cookie al avanzar varios días. Se separan las rutas
administrativas/reservas en chunks para evitar cargar el formulario al entrar al home.
