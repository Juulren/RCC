# Entregas y verificación

Estado al 2026-10-19: módulos 0, 1 y 2 publicados en PR #1, #2 y #3.
Módulo 3 implementado en `codex/modulo-3-identidad`, con autenticación real y
pruebas PostgreSQL/Chromium; evidencia en MODULO-3.md. Revisión humana e integración
pendientes. Módulos 4 a 9 no implementados. El responsable autorizó continuar
tras revisar visualmente el módulo 2; no se fusionan PR automáticamente.

Una rama y un PR por módulo. Trabajar en tareas pequeñas, revisar diff y ejecutar
pruebas aplicables antes del commit final. No comenzar el siguiente módulo hasta
revisar el anterior. Ningún servicio externo se declara integrado por un mock.

| Módulo | Rama sugerida | Salida y aceptación |
|---|---|---|
| 0 | docs/modulo-0-especificacion | Especificación, API, datos, bocetos, modalidad individual, matriz de requisitos; enlaces y coherencia documental revisados. |
| 1 | feat/modulo-1-base | SDK 10, solución API, React/Vite, PostgreSQL, migración inicial, instrucciones reales, configuración sin secretos, CI de compilación y pruebas mínimas de integración. |
| 2 | feat/modulo-2-diseno | Tokens y componentes, portada y tarjeta revisadas primero, luego navegación bilingüe y estados; capturas y accesibilidad básica. |
| 3 | codex/modulo-3-identidad | Registro/login/logout/me, CSRF y roles; pruebas de credenciales, email duplicado, permisos y cierre de sesión. |
| 4 | feat/modulo-4-catalogo | Catálogo bilingüe y administración; persistencia, validación y restricciones de cambios a inventario comprometido. |
| 5 | feat/modulo-5-reservas | Disponibilidad/cotización/reserva/cancelación; pruebas con PostgreSQL de fechas, capacidad, precios, idempotencia y concurrencia real. |
| 6 | feat/modulo-6-pagos | Checkout sandbox/webhook/reconciliación/reembolso; firma inválida, aprobado, rechazado, duplicado, pago tardío, vencimiento y cancelación. |
| 7 | feat/modulo-7-servicios | Clima/mapa/correo reales configurados; caché, fallback, colas, reintentos y prueba controlada de correo recibido. |
| 8 | feat/modulo-8-ia | Recomendaciones con LLM real y salida validada; pruebas de capacidad, presupuesto, IDs inventados, timeout y fallback marcado. |
| 9 | chore/modulo-9-entrega | Render/Neon, HTTPS, migración controlada, enlace público, prueba end-to-end, README completo, autoría, bitácora y demo. |

## Trazabilidad con el PDF

| Requisito / entregable | Módulos | Evidencia final |
|---|---|---|
| Interfaz responsiva | 2, 9 | Capturas móvil/escritorio y Playwright. |
| REST y base relacional | 1, 4, 5 | Contrato API, migraciones y pruebas PostgreSQL. |
| Registro/login y dos roles | 3 | Pruebas de identidad y permisos. |
| Dos APIs externas mínimo | 6, 7 | Stripe, clima y correo; mapa adicional. |
| Función IA con API LLM | 8 | Recomendación con proveedor real; pruebas aisladas en CI. |
| Registro/login y dos funciones clave probadas | 3, 5, 6 | Pruebas de registro, login, reserva y cancelación/pago. |
| GitHub Actions en PR | 1 en adelante | Runs de compilación y pruebas por PR. |
| Enlace público | 9 | Smoke test del despliegue HTTPS. |
| Seguridad básica | 1, 3, 5, 6, 9 | Secretos externos, hashes, CSRF, validación, autorización y firma webhook. |
| README instalación/arquitectura/pruebas | 0, 1, 9 | Comandos verificados y diagrama/explicación de arquitectura. |
| Especificación previa | 0 | ESPECIFICACION.md y DISENO.md en PR anterior al código. |
| Ramas, PR y commits etiquetados | Todos | Historial GitHub; modalidad individual declarada. |
| Porcentaje IA/humano/mixto | Todos, cierre 9 | Estimación honesta basada en contribuciones y evidencia. |
| Bitácora y demo de 10 minutos | Todos, cierre 9 | Una bitácora individual y guion de demostración. |
| Cuatro integrantes/revisión entre compañeros | Excepción individual | Diferencia explícita con la consigna; no se declara satisfecha. |

## Estrategia de pruebas y publicación

xUnit para reglas e integración ASP.NET; PostgreSQL aislado para restricciones y
concurrencia (no sustituir por EF InMemory). Playwright para registro/login y
reserva de varias habitaciones en ES/EN y móvil/escritorio. Proveedores simulados
en CI, con pruebas manuales controladas sandbox antes de publicar. Reloj inyectable
para vencimientos y fecha de Chongqing.

GitHub Actions empieza en módulo 1: compilar backend/frontend y ejecutar pruebas
en cada PR; añadir recorridos E2E conforme existan. No inventar pruebas funcionales
para la entrega solo documental. Conservar resultados/capturas de fallos útiles.

Guion final de 10 minutos: contexto/diseño (1), registro/login (1), selección y
reserva/pago (3), cancelación e integraciones/IA (2), arquitectura/pruebas/CI (2),
autoría y límites (1). Preparar cuentas y datos ficticios, nunca claves en pantalla.
