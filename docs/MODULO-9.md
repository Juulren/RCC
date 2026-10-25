# Módulo 9 — publicación y entrega

Inicio: 22 de octubre de 2026. Cierre: **24 de octubre de 2026**. Responsable único: Juulren. Implementación inicial en `codex/modulo-9-despliegue`, ajustes finales en `codex/gemini-gratuito`.

**Módulo concluido por aceptación del responsable**, quien indicó que ya realizó sus pruebas y solicitó dar el proyecto por terminado. Sitio: https://rcc-7gqa.onrender.com. Esta aceptación no se presenta como revisión humana independiente ni como ejecución documentada de cada escenario de la lista inicial. La integración de los PR es una operación separada.


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

## Aceptación y evidencias públicas

| Comprobación | Evidencia de cierre |
|---|---|
| Render y Neon | Cuentas/configuración realizadas por el responsable; sitio HTTPS operativo. |
| API y base de datos | Live y ready respondieron Healthy; rutas SPA y módulo 9 comprobados desde la URL pública. |
| Registro e inicio de sesión | Confirmados manualmente por el responsable en Render. |
| Pago Stripe sandbox | El responsable confirmó el pago reflejado tras configurar el webhook del dominio. |
| Correo Resend | El responsable confirmó recepción tras activar el servicio publicado. |
| Google Maps | El responsable confirmó carga del mapa en Render. |
| Gemini | El responsable confirmó funcionamiento tras cambiar a gemini-3.5-flash-lite. No requiere PC/túnel. |
| Pruebas manuales adicionales y aceptación | El responsable declaró haber probado el proyecto y solicitó su cierre el 24 de octubre de 2026. No aportó un desglose por escenario. |
| CI y secretos | La CI completa y Secret scanning del commit funcional `5d3994c` terminaron correctamente. |

La aceptación general no se convierte en evidencia individual inventada de reembolsos, concurrencia, roles, recuperación de respaldos o ejecución de la demostración. Se conserva el guion y la documentación de entrega como artefactos. Las pruebas automatizadas de PostgreSQL, permisos, pagos, inventario y producción conservan su alcance propio.

## Límites y riesgos materiales

Free puede dormir/suspender servicios y retrasar workers; no es disponibilidad comercial. La IA publicada utiliza Gemini Free; no requiere la PC ni el túnel. Depende de la disponibilidad/cuota del proyecto Google. Ante fallos se conserva el cálculo determinista. No se debe activar Billing para mantener la modalidad gratuita. El certificado/contraseña de claves de sesión debe conservarse y renovarse con un plan compatible, no regenerarse por deploy. El rol migrador/runtime compartido simplifica el laboratorio; una operación permanente requiere separación de permisos y revisión adicional. El ingreso Render es el límite de confianza del modo proxy, no una configuración para puertos públicos directos.

El historial de PR de módulos previos sigue pendiente de integración/revisión. La publicación del README en main no integra automáticamente el código. No se contratan recursos de pago ni se crean aprobaciones humanas ficticias.

## Cambios finales del despliegue

El proveedor final es Gemini Free, modelo `gemini-3.5-flash-lite`. Se corrigió el 404 de los modelos 2.5 restringidos para proyectos nuevos. El responsable confirmó la IA funcionando. Ollama/puente queda como alternativa local documentada.

El aviso de biblioteca GSSAPI de Npgsql se resolvió configurando `GSS Encryption Mode=Disable` en la conexión privada de Render, conservando `SSL Mode=VerifyFull`. No se publicaron cadenas ni credenciales.

El panel explica el presupuesto total CNY, promedio por habitación/noche y preferencias; los fallos de IA muestran códigos/mensajes seguros. Validación local de esos ajustes: 19 pruebas frontend, seis escenarios responsivos ES/EN y 53 pruebas aisladas de conectores/puente. La CI de la versión funcional también completó la matriz con PostgreSQL y contenedor.
