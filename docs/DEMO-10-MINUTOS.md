# Guion de demostración — diez minutos

Estado: guion preparado. La demostración final en el dominio publicado sigue pendiente. No mostrar secretos, paneles de claves, cadenas Neon, certificado privado ni variables de entorno durante la grabación.

| Tiempo | Mostrar y explicar |
|---|---|
| 0:00–1:00 | Contexto académico, inspiración Chongqing, tarifas ficticias, versión móvil/escritorio, ES/EN y movimiento reducido. |
| 1:00–2:00 | Registro/login de cliente y diferencia con administración; cookies/CSRF sin abrir valores de sesión. |
| 2:00–3:00 | Catálogo, fechas, huéspedes y selección de varias habitaciones. Explicar adulto mínimo, capacidad y fechas UTC+8. |
| 3:00–4:00 | Recomendaciones con Ollama mediante puente. Señalar modo IA y hechos verificados; elegir no confirma reserva. |
| 4:00–5:00 | Cotización del servidor y aceptación del precio; reserva pendiente e inventario bloqueado. |
| 5:00–6:00 | Checkout Stripe sandbox con datos de prueba, webhook y estado confirmado. No confundir retorno del navegador con verificación del pago. |
| 6:00–7:00 | Mis reservas, correo recibido y cancelación; mostrar estado de reembolso sin afirmar liquidación si no está comprobada. |
| 7:00–8:00 | Destino: clima actual con fuente/hora y mapa automático. Explicar fallback y restricciones de correo de prueba. |
| 8:00–9:00 | Arquitectura React/C#/PostgreSQL, Docker, Neon, cifrado persistente de claves de sesión y límites del puente. |
| 9:00–10:00 | GitHub, PR, CI, pruebas, autoría mixta y límites del plan Free. Cierre con alcance académico y pendientes honestos. |

## Preparación

- Abrir la URL pública con anticipación para contemplar el arranque frío del plan Free.
- Mantener PC, Ollama y puente/túnel activos si se va a demostrar IA; configurar la credencial de la sesión en Render antes de compartir pantalla.
- Usar una cuenta/destinatario de prueba autorizado por Resend y fechas futuras con inventario.
- Verificar Stripe sandbox y webhook del dominio; nunca usar datos de tarjeta reales.
- Ejecutar la lista de aceptación del módulo 9 y guardar evidencia saneada. Si una integración falla, mostrar su estado real/fallback y no simular un éxito.
