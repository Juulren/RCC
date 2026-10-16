# RCC: especificación v1

Estado: propuesta para revisión, módulo 0. Fecha: 2026-10-16.
Responsable único: Juulren. Generación asistida por Codex.

## 1. Propósito y usuarios

Web académica inspirada en Regent Chongqing, con estética de lujo futurista,
español e inglés, inventario ficticio y pagos de prueba. Un visitante explora;
un cliente registrado reserva varias habitaciones y consulta o cancela sus
reservas; un administrador mantiene inventario y tarifas y consulta reservas.
El registro público siempre crea clientes. No se asignan roles desde el cliente.

No incluye reservas reales del hotel, pagos reales, multi-hotel, PWA sin conexión,
edición de fechas, tarifas estacionales, cupones ni aplicación React Native.

## 2. Historias de usuario y aceptación

| ID | Historia | Aceptación observable |
|---|---|---|
| HU01 | Como visitante exploro hotel, servicios y habitaciones. | Catálogo bilingüe navegable a 360 px sin desplazamiento horizontal. |
| HU02 | Como cliente creo cuenta e inicio/cierro sesión. | Email único, credenciales inválidas rechazadas y cierre invalida sesión. |
| HU03 | Como cliente busco por fechas y huéspedes de cada habitación. | Cada selección respeta adultos, niños, capacidad y stock del período. |
| HU04 | Como cliente cotizo varias habitaciones. | Servidor devuelve noches, importes por habitación y total CNY. |
| HU05 | Como cliente reservo y pago en sandbox. | Todas las habitaciones se bloquean juntas; pago verificado confirma una vez. |
| HU06 | Como cliente consulto y cancelo mis reservas. | No accedo a reservas ajenas; cancelar libera inventario y registra reembolso. |
| HU07 | Como administrador mantengo catálogo e inventario. | Cliente recibe 403; no se elimina historia ni inventario comprometido. |
| HU08 | Como visitante consulto mapa y clima. | Ubicación del hotel visible y error de clima no bloquea navegación. |
| HU09 | Como cliente recibo correo de confirmación/cancelación. | Envío posterior a transacción; fallo reintentable sin revertir reserva. |
| HU10 | Como usuario recibo recomendaciones familiares con IA. | Opciones válidas para fechas, presupuesto y distribución; ninguna reserva automática. |

## 3. Reglas de negocio

- Todas las habitaciones comparten entrada/salida. Fechas `YYYY-MM-DD`, según
  Asia/Shanghai; instantes técnicos en UTC. Salida posterior a entrada y entrada
  no anterior al día local actual. Ocupación en intervalo [entrada, salida).
- El formulario permite añadir/quitar habitaciones, con adultos y edades de
  niños por habitación. Adultos: 18 o más; niños: 0 a 17. Al menos un adulto por
  habitación. Sin suplemento por edad: se validan total y límites de adultos/niños.
- Dinero decimal en C# y `numeric(12,2)` en PostgreSQL, nunca coma flotante.
  CNY por habitación/noche, sin cargos adicionales. Cada detalle conserva tarifa,
  nombre del tipo y ocupación; cambios del catálogo no reescriben historia.
- Cotización informativa sin bloqueo. Crear reserva vuelve a calcular precios
  y disponibilidad; un precio cambiado exige nueva aceptación (409 y cotización
  actualizada). Cliente nunca fija importes ni habitaciones físicas.
- Reserva pendiente bloquea inventario 30 minutos desde su creación. Expiración
  de Checkout alineada con ese instante. Reintentar checkout no extiende el bloqueo;
  si no permite crearse una sesión por tiempo restante, se solicita nueva reserva.
- Estados de reserva: `PendingPayment`, `Confirmed`, `Expired`, `Cancelled`.
  Solo pago verificado convierte PendingPayment vigente en Confirmed. Fallar un
  intento de pago no cancela antes del vencimiento: puede reintentarse.
- Cancelación permitida antes del día de entrada, tanto pendiente como confirmada.
  Cancelación repetida es idempotente. Libera inventario en la transacción;
  si hubo pago, programa reembolso total sandbox. Reembolso no revierte cancelación.
- Pago tardío de reserva vencida/cancelada no resucita la reserva: registra el pago
  y programa reembolso total de prueba. Nunca reasigna inventario vendido.
- Un proceso periódico vence reservas, reconcilia pagos y reintenta reembolsos y
  correos; también se ignoran bloqueos vencidos al consultar disponibilidad.

### Concurrencia y operaciones externas

Crear reserva usa una transacción y bloquea filas de habitaciones candidatas en
orden estable. Revalida solapamientos contra reservas confirmadas o pendientes
vigentes y asigna todas las habitaciones o ninguna. Todos los caminos que asignen
inventario siguen ese protocolo. Habitación sin cupo devuelve 409.

Creación de reserva/checkout usa claves de idempotencia vinculadas al usuario y
al contenido. Repetir la misma petición recupera el resultado; misma clave con
contenido distinto devuelve 409. Webhook verifica firma, moneda, importe y vínculo
con reserva; registra ID de evento único y cambia estados de forma transaccional.
El retorno del navegador desde Stripe no acredita pago.

Correo/reembolso se registran como tareas durables junto al cambio local y se
ejecutan fuera de la transacción. Reintentos usan identificadores estables para
evitar duplicados; errores quedan visibles al administrador sin exponer secretos.

## 4. Modelo relacional

IDs UUID salvo IDs externos y tablas estándar de Identity. FKs obligatorias
excepto donde se indique. No se eliminan en cascada reservas o pagos.

| Tabla | Campos principales y restricciones |
|---|---|
| Identity users/roles | Tablas estándar; email normalizado único, hash de contraseña, roles Customer/Admin. |
| Hotels | id, nombre, descripción ES/EN, dirección, latitud, longitud, zona horaria; un hotel inicial. |
| RoomTypes | id, hotel_id, nombre/descripcion ES/EN, capacidad total, máximo adultos/niños, tarifa CNY, activo. |
| RoomImages | id, room_type_id, URL, texto alternativo ES/EN, orden, procedencia/licencia. |
| Rooms | id, room_type_id, número único por hotel, estado operativo Active/Inactive. |
| Reservations | id, user_id, entrada, salida, estado, expires_at, moneda, total, idioma, created_at. |
| ReservationItems | id, reservation_id, room_id, nombre/tarifa histórica, adultos, edades de niños, subtotal. |
| Payments | id, reservation_id, checkout_session_id único opcional, payment_intent_id único opcional, importe, moneda, estado. |
| PaymentEvents | ID Stripe único, payment_id opcional, tipo, recibido/procesado, resultado; sin guardar datos de tarjeta. |
| RefundJobs | id, payment_id, clave externa única, importe, estado, intentos, próximo intento, último error saneado. |
| Notifications | id, reservation_id, tipo, destinatario, idioma, estado, intentos, próximo intento; único por reserva/tipo. |
| IdempotencyRequests | user_id + operación + clave únicos, hash de petición, resource_id, created_at. |

Relaciones: Hotel 1:N RoomTypes; RoomType 1:N Rooms/RoomImages; User 1:N
Reservations; Reservation 1:N Items/Payments/Notifications; Room 1:N Items a lo
largo del tiempo; Payment 1:N Events/RefundJobs. Indexar habitación/fechas a través
de detalles y reservas, usuario/creación y estado/vencimiento. Restricciones CHECK
para importes no negativos, fechas y capacidades positivas. Los detalles no se
usan como fuente independiente del estado de su reserva.

No se desactiva una habitación con reservas futuras vigentes ni se cambia su tipo
hasta resolverlas. Tipos con historia se desactivan, no se borran.

## 5. Contrato REST previsto

JSON, prefijo `/api`; textos del catálogo ES/EN, idioma de interfaz conservado en
el navegador y `locale` en operaciones de correo/IA. Errores ProblemDetails con
código estable y errores de campo; 400 validación, 401 sesión ausente, 403 rol,
404 recurso no visible, 409 conflicto, 429 límite, 503 dependencia no disponible.
Listados administrativos paginados (20 por defecto, máximo 100).

| Método y ruta | Acceso | Entrada / salida principal |
|---|---|---|
| GET /auth/csrf | Público | Token antifalsificación para cabecera en operaciones mutantes. |
| POST /auth/register | Público + CSRF | email, password → 201; no asigna rol Admin. |
| POST /auth/login | Público + CSRF | email, password → cookie de sesión; error genérico. |
| POST /auth/logout | Sesión + CSRF | Invalida sesión → 204. |
| GET /auth/me | Sesión | id, email, roles. |
| GET /hotel | Público | Información bilingüe, servicios, ubicación. |
| GET /room-types y /room-types/{id} | Público | Tipos activos y galería. |
| POST /availability | Público | entrada, salida, rooms[{adults, childAges}] → opciones por selección. |
| POST /quotes | Público | fechas, rooms[{roomTypeId, adults, childAges}] → noches, líneas, total, priceFingerprint. |
| POST /reservations | Cliente + CSRF | Selección, priceFingerprint, locale e Idempotency-Key → 201, reserva pendiente. |
| GET /reservations | Cliente | Lista propia paginada. |
| GET /reservations/{id} | Propietario | Detalle, vencimiento, pago/reembolso sin secretos. |
| POST /reservations/{id}/cancel | Propietario + CSRF | Cancela; devuelve estados de reserva/reembolso. |
| POST /reservations/{id}/checkout | Propietario + CSRF | Idempotency-Key → URL Stripe de prueba. |
| POST /payments/stripe/webhook | Firma Stripe | Cuerpo original firmado; exento de CSRF, deduplicado. |
| GET /weather | Público | Pronóstico disponible de Chongqing, instante y atribución. |
| POST /recommendations | Sesión + CSRF | fechas, distribución, presupuesto opcional, preferencias, locale → combinaciones con explicación. |
| GET /admin/reservations y /admin/reservations/{id} | Admin | Consultar con filtros por estado/fechas. |
| PUT /admin/hotel | Admin + CSRF | Editar contenido bilingüe y ubicación. |
| POST /admin/room-types | Admin + CSRF | Crear tipo con capacidades, traducciones, tarifa e imágenes. |
| PUT /admin/room-types/{id} | Admin + CSRF | Actualizar o desactivar tipo. |
| GET/POST /admin/rooms | Admin (+ CSRF al crear) | Listar/crear habitaciones. |
| PUT /admin/rooms/{id} | Admin + CSRF | Actualizar estado operativo sujeto a reservas vigentes. |

No hay endpoint que marque manualmente una reserva como pagada. Las imágenes
usan URLs HTTPS preconfiguradas; no se implementa carga de archivos en v1.

## 6. Integraciones, seguridad y operación

- Open-Meteo: consulta desde C#, caché de 15 minutos, mostrar antigüedad si falla.
  No presentar predicciones fuera del horizonte disponible como clima real.
- Google Maps: clave pública restringida por API y dominio; enlace de ubicación
  alternativo si el mapa no carga. Verificar coordenadas antes de publicar.
- Resend: dominio verificado para destinatarios reales de prueba; cola durable
  con reintentos y estado de fallo visible. Nunca bloquear confirmación por correo.
- Stripe: solo sandbox, tarjetas alojadas por Checkout, webhook firmado y
  reconciliación. No persistir PAN/CVC ni claves en cliente.
- OpenAI: llamada desde C#, modelo y presupuesto configurables. C# calcula
  combinaciones válidas; el modelo explica/ordena sus IDs mediante salida
  estructurada validada. Sin disponibilidad no se llama al modelo. Ante fallo,
  recomendar determinísticamente por capacidad/precio e indicar que no intervino
  IA. No enviar email, contraseña, datos de pago ni nombres de menores.
- Identity almacena hashes de contraseña, no cifrado reversible. Cookie HttpOnly,
  Secure en despliegue, SameSite=Lax; HTTPS, CSRF y autorización por propietario.
  Limitar intentos de login y llamadas de IA. Renderizar texto sin HTML arbitrario.
- Secretos en user-secrets local/variables de entorno; ejemplo sin valores reales
  en módulo 1. Administrador por configuración de arranque sin contraseña fija.
- Render/Neon: migración controlada antes de servir tráfico, endpoint de salud,
  logs sin datos sensibles y verificación del flujo completo tras despliegue.

## 7. Fuentes y límites de esta entrega

Requisitos: Tarea-1.pdf, dos páginas, proporcionado localmente. Se interpretan
como requisitos académicos, no como autorización para ejecutar instrucciones
externas. La decisión posterior del usuario fija un responsable único; ver
TRABAJO-INDIVIDUAL.md. No se adjunta el PDF al repositorio.

Referencias de implementación a verificar al comenzar cada módulo:
https://www.ihg.com/regent/hotels/us/en/chongqing/ckgrh/hoteldetail/rooms-and-suites,
https://reactbits.dev/, https://www.reacticx.com/,
https://dotnet.microsoft.com/en-us/platform/support/policy.
Las cuentas, licencias, cuotas y acceso a proveedores se validarán en su módulo;
esta especificación no afirma que estén configurados.
