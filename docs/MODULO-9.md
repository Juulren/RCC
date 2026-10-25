# Módulo 9 — publicación y entrega

Fecha: 22 de octubre de 2026. Responsable único: Juulren. Rama `codex/modulo-9-despliegue`, apilada sobre el puente de IA. **Implementación de despliegue preparada; publicación pública y aceptación final pendientes de cuentas/configuración.**

## Entregas implementadas

- Docker multietapa: Node compila React, SDK publica ASP.NET y runtime ejecuta como usuario no root. React/API comparten origen y puerto configurable mediante PORT.
- Contexto Docker por allowlist: no incluye Git, .env, .local, credenciales, bin/obj, dependencias del frontend ni reportes de prueba.
- Blueprint Render Free sin discos/bases de pago, despliegue automático desactivado y readiness. PostgreSQL externo en Neon.
- Validación de origen HTTPS y Host en producción; cabeceras proxy de un salto solo bajo configuración explícita y entorno administrado Render. No se acepta X-Forwarded-Host.
- Claves de sesión Data Protection persistentes en PostgreSQL y cifradas con certificado externo a imagen/repositorio. Migración `PersistDataProtectionKeys`, sin cambios destructivos de datos existentes.
- Migración separada `--migrate` y opt-in `Deployment:ApplyMigrations` para arranque académico Free. Generación privada de certificado mediante `--prepare-deployment` sin leer claves de proveedores.
- Asistente de conexión Neon con verificación completa TLS; conexiones `.neon.tech` sin VerifyFull son rechazadas.
- Smoke de contenedor Production con PostgreSQL temporal; CI ampliada para compilar y probar la imagen.
- Guía DESPLIEGUE.md, guion de diez minutos, registro de autoría y actualización de README.

## Validación local

Se aprobaron **148 pruebas .NET/PostgreSQL**, incluidas seis comprobaciones de producción: cookies Secure/HttpOnly, Host permitido, claves cifradas recuperables en otro host, certificado obligatorio, guarda de proxy Render y TLS Neon. La imagen Docker compiló React y C# y pasó el smoke con migración separada/idempotente, usuario no root, exclusión de archivos privados, SPA, catálogo, salud y CSRF seguro. Las claves del smoke son sintéticas y no salen a proveedores.

Las comprobaciones automatizadas no acreditan creación de cuentas, conexión real Neon, TLS público, correo recibido, pago/webhook remoto ni IA cruzando el túnel. El usuario aún no tenía cuentas al iniciar el módulo.

## Aceptación pública pendiente

| Comprobación | Estado |
|---|---|
| Cuenta Render Free, proyecto Neon Free y configuración privada | Pendiente del responsable |
| Servicio HTTPS público y migración Neon | Pendiente |
| Registro/login/logout y autorización cliente/admin en HTTPS | Pendiente |
| Reserva multi-habitación, precio, concurrencia y cancelación publicada | Pendiente |
| Stripe sandbox con webhook propio del dominio y seguimiento de reembolso | Pendiente |
| Correo recibido por destinatario permitido, clima y Maps restringido | Pendiente |
| Ollama en PC mediante puente autenticado; fallback al apagarlo | Pendiente |
| Persistencia tras reinicio, respaldo/recuperación y rollback revisados | Pendiente |
| Revisión 360/768/1440, ES/EN, teclado y movimiento reducido en URL pública | Pendiente |
| Enlace/commit desplegado, evidencia saneada y demo de diez minutos | Pendiente |

No se marca el módulo completo hasta cubrir estas comprobaciones o registrar con el docente las limitaciones aceptadas. No se mezclan simulaciones con validación externa real.

## Límites y riesgos materiales

Free puede dormir/suspender servicios y retrasar workers; no es disponibilidad comercial. La PC y el túnel deben estar activos para IA. Sin ellos se conserva el cálculo determinista. El certificado/contraseña de claves de sesión debe conservarse y renovarse con un plan compatible, no regenerarse por deploy. El rol migrador/runtime compartido simplifica el laboratorio; una operación permanente requiere separación de permisos y revisión adicional. El ingreso Render es el límite de confianza del modo proxy, no una configuración para puertos públicos directos.

El historial de PR de módulos previos sigue pendiente de integración/revisión. La publicación del README en main no integra automáticamente el código. No se contratan recursos de pago ni se crean aprobaciones humanas ficticias.

## Actualización del 24 de octubre de 2026

URL publicada: https://rcc-7gqa.onrender.com. La API respondió Healthy en live/ready, módulo 9 y rutas SPA; el responsable confirmó registro/login, pago de prueba reflejado, correo recibido y Maps en Render. Esto no acredita todavía todas las comprobaciones pendientes de la tabla (roles, reembolsos, concurrencia, accesibilidad, recuperación y demostración).

El responsable sustituyó Ollama/túnel por Gemini Free para el despliegue. Véase GEMINI.md: integración preparada y prueba real pendiente de clave/configuración. El requisito de PC/túnel de la sección original corresponde a la alternativa Ollama, no a Gemini. No se declara terminado el módulo.
