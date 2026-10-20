# Módulo 7: clima, ubicación y notificaciones

Fecha: 2026-10-20. Rama `codex/modulo-7-servicios`, sobre módulo 6. Responsable único: Juulren. Código y documentación asistidos por IA; no se atribuye revisión humana independiente.

## Entrega

- Clima real desde C# con Open-Meteo, cliente HTTP de 8 segundos, caché compartida por instancia y exclusión de peticiones simultáneas. Validación de datos y hora; sin cifras ficticias si falla. No se usa para prometer clima de fechas futuras.
- Destino conserva su portada y navegación. Paneles responsivos ES/EN con temperatura, sensación, viento, hora y atribución CC BY 4.0. Dato obsoleto visible y botón actualizar sujeto a caché/backoff del servidor.
- Google Maps Embed por dirección/nombre del hotel, activado por usuario cuando existe clave pública restringida. Sin clave o ante error: enlace externo funcional. No se presenta el esquema anterior como mapa preciso ni se colocan pines con coordenadas aproximadas.
- Cola SQL durable de confirmación y cancelación. Encolado dentro de la misma transacción que cambia la reserva, sin llamadas externas bajo bloqueo. Confirmación solo tras verificar el pago; no al volver desde Checkout ni ante pago tardío reembolsable.
- Resend desde C#, HTTP 10 segundos, texto ES/EN con referencia, fechas locales, total histórico y aviso académico/pago de prueba. Destinatario y contenido congelados al encolar; remitente congelado al primer intento para reintentos idénticos.
- Worker cada 20 segundos, máximo 20 mensajes por ciclo, lease SQL de dos minutos, backoff de hasta una hora y protección frente a resultados de un lease antiguo. Clave idempotente estable por notificación. Resultado aceptado no se envía otra vez.
- Panel administrativo de solo lectura con estado de configuración, evento/reserva, intentos y error saneado; paginado de 20 registros, protegido por rol y no-store. No expone cuerpo, destinatario, claves ni identificadores del proveedor.

## Modelo y contratos

Tabla `Notifications`: Id, ReservationId (FK restrict), Kind (Confirmed/Cancelled), Recipient, Subject, Body, Sender, State (Pending/Accepted/Review), Attempts, CreatedAt, NextAttemptAt, FirstAttemptAt, LeaseUntil, ProviderId y LastError. Índice único `(ReservationId, Kind)` e índice del trabajo pendiente. Migración `AddServiceNotifications`.

| Ruta | Acceso y resultado |
|---|---|
| GET /api/weather | Público; available, stale, current nullable, timeZone y source. current incluye temperatura °C, sensación °C, código WMO, viento km/h, observedAt local y fetchedAt UTC. |
| GET /api/location | Público; searchUrl y embedUrl nullable. Solo clave pública Maps; nunca secretos de Stripe/Resend. |
| GET /api/admin/notifications?page=1 | Admin; configured, items, total, page y totalPages. Paginación válida desde 1. |

Caché climática 15 minutos; ante error, anterior hasta dos horas y reintento espaciado un minuto. Sin datos se devuelve 200 con available=false para permitir alternativa explícita. Coordenadas de ciudad aproximadas, no precisión del hotel. No se modifica ni envía a proveedores el borrador de reserva.

Resend desactivado por defecto. Pending permanece durable sin intentos mientras falten configuración o habilitación. 429/5xx/red/timeout se reintentan. Otros 4xx pasan a Review. Pasadas 23 horas desde el primer intento ambiguo, Review evita repetir fuera de la retención idempotente de 24 horas del proveedor. La cola no garantiza orden de recepción entre correos: cada correo describe un evento fechado y remite al estado actual en Mis reservas. Cancelar no borra el historial de confirmación. No se afirma entrega al destinatario: Accepted solo es aceptación HTTP válida del proveedor.

## Configuración y límites

Ver [SERVICIOS-EXTERNOS.md](SERVICIOS-EXTERNOS.md), con alta, restricciones, comandos locales y demostración. El usuario indicó que aún no dispone de Resend ni Google Maps. La validación real de ambos queda pendiente; los proveedores HTTP se simulan en CI. Stripe también sigue pendiente de sandbox real según módulo 6. No se han enviado correos externos ni creado cuentas/facturación. No se implementa IA ni despliegue del módulo 8/9.

## Verificación

Pruebas automatizadas: caché/exclusión concurrente, clima inválido, caída y dato obsoleto/vencido; transporte Resend y clave estable, 429/503 frente a 401/422; cola única ES/EN, rollback, desactivación, recuperación tras fallo, remitente persistente, lease entre dos workers, respuesta de un lease vencido y ventana idempotente; permisos y privacidad de la consulta administrativa. Las pruebas monetarias existentes comprueban encolado único tras webhook repetido y dos eventos tras cancelar idempotentemente una confirmada.

Navegador: paneles a 360/768/1440 en ES/EN, atribución, dato obsoleto, ausencia de overflow, recuperación de red, mapa bajo acción explícita y enlace alternativo. Se conserva la matriz de registro, catálogo, reservas y pagos aislados. Las capturas son artefactos locales/CI de Playwright. La apariencia del iframe con clave real requiere comprobación manual porque Google es otro origen.

Comprobaciones locales: 103 pruebas .NET/PostgreSQL/transporte, 7 Vitest y 29 Playwright de diseño/servicios aprobadas. Compilación de producción y migración aplicada sin cambios pendientes de modelo. La primera ejecución Vitest agotó el tiempo al arrancar su worker con otras suites activas; la repetición aislada pasó 7/7.

Consulta controlada de la API local a Open-Meteo el 2026-10-20: available=true, stale=false, temperatura 19.7 °C, sensación 23 °C, viento 1 km/h y hora local 2026-10-21 00:30 UTC+8. Es evidencia puntual, no un dato fijo del producto. `/api/location` sin clave devuelve embedUrl=null y enlace por nombre/dirección; `/api/status` informa módulo 7. No se interpreta una prueba simulada como entrega de correo o mapa externo real.

## Demostración breve

1. Abrir Destino, alternar ES/EN y ver clima actual con fuente/hora.
2. Abrir ubicación externa; con clave restringida, cargar iframe y comprobar hotel.
3. Crear/cancelar una reserva con datos propios; abrir administración y ver Cancelled/Pending sin Resend.
4. Con proveedores de prueba aislados, verificar confirmación de pago → Confirmed/notification única y cancelación → Cancelled/notification única.
5. Cuando se configure Resend, validar aceptación en RCC y recepción por separado en una cuenta propia. No grabar claves.


Regresiones aprobadas: 3 Playwright de autenticación, 2 de catálogo, 4 de reservas y 3 de pagos con proveedor aislado. Matriz final: 103 .NET + 7 Vitest + 29 diseño/servicios + 12 recorridos integrados = **151 comprobaciones**. Tras el ajuste visual del botón se repitieron los ocho escenarios de servicios, sin contar las repeticiones como casos nuevos. Se revisaron capturas de escritorio y móvil.

La prueba de lease vencido motivó capturar el intento dentro de su transacción y usar el contador monotónico para aceptar resultados. No se comparan timestamps C#/PostgreSQL a precisión de ticks; una respuesta antigua no puede degradar Accepted. El caso queda cubierto en NotificationTests.
