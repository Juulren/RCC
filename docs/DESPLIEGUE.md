# Despliegue académico: Render Free + Neon Free

Este procedimiento publica React y ASP.NET bajo el mismo dominio. La imagen Docker ya se prueba localmente; **no existe todavía un despliegue público verificado**. No compartas claves, cadenas PostgreSQL, certificados ni archivos privados en chat, capturas, Git o variables VITE.

## 1. Crear cuentas gratuitas

- [Render](https://dashboard.render.com/register): puedes iniciar sesión con GitHub. Al conectar el repositorio, concede acceso solo a RCC si la interfaz lo permite.
- [Neon](https://console.neon.tech/): crea un proyecto en el plan **Free**. Elige PostgreSQL 17 y una región cercana a la región de Render. No cambies a Launch/Scale.
- En Render elige servicio **Free**, no Starter. No se añade una base Render Postgres ni un disco de pago: los datos estarán en Neon.

Los niveles gratuitos tienen cupos. Render duerme tras 15 minutos sin tráfico, tarda aproximadamente un minuto en despertar y limita horas, transferencia y compilaciones; con método de pago, algunos excesos pueden facturarse. Sin método de pago, los límites pueden suspender servicios/compilaciones. Neon Free tiene límites de almacenamiento y cómputo; revisa el panel antes de aceptar cambios de plan. No se garantiza un servicio ilimitado ni disponibilidad de producción comercial.

## 2. Preparar la conexión privada de Neon

En el proyecto Neon abre **Connect** y selecciona tu base/rol. Para el primer despliegue usa el endpoint directo: desactiva la opción Connection pooling del panel. Npgsql mantiene su propio pool, limitado a 10 conexiones.

Puedes copiar la cadena del ejemplo C# y establecer `SSL Mode=VerifyFull;Maximum Pool Size=10`, o usar el asistente que convierte una URI PostgreSQL de Neon al formato de ASP.NET:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\preparar-neon.ps1 -DesdePortapapeles
```

Ejecuta primero el comando. Cuando lo indique, copia la URI desde Neon, vuelve a la terminal y pulsa Enter **sin pegarla**. No sustituyas textos dentro del script. El asistente no conecta ni crea recursos: guarda el resultado en `%APPDATA%\RCC\deployment\neon-connection.txt`, fuera del repositorio. El archivo contiene una credencial: protege tu perfil Windows. No se sobrescribe si ya existe.

Su contenido será el valor privado de `ConnectionStrings__Rcc` en Render. La API rechaza endpoints `.neon.tech` sin verificación completa de TLS. No uses `Trust Server Certificate=true` para evitar validación.

## 3. Preparar las claves de sesión persistentes

Render Free no conserva archivos locales tras reinicios. RCC almacena las claves de Data Protection en PostgreSQL y las cifra con un certificado cuya clave privada vive en configuración del backend.

```powershell
dotnet run --project src/Rcc.Api -- --prepare-deployment
```

No necesita conexión PostgreSQL ni inicia la API. Genera una vez `%APPDATA%\RCC\deployment\cookie-certificate.env`. **En el equipo de desarrollo ya se generó**; si existe, no lo reemplaces: el comando se detiene para conservarlo. Guarda una copia privada. El archivo contiene dos secretos para importar en Render:

- `DataProtection__CertificateBase64`
- `DataProtection__CertificatePassword`

No es el certificado HTTPS del sitio: Render gestiona ese TLS. Este certificado protege las claves de sesión en la base. No lo borres ni lo cambies en cada despliegue; reemplazarlo sin conservar soporte para el anterior impide descifrar las claves existentes. Antes de su vencimiento debe planificarse una rotación con compatibilidad. Nunca lo incluyas en la imagen Docker.

## 4. Crear el servicio Render

Mientras los módulos no estén integrados en main, utiliza la rama **codex/modulo-9-despliegue**.

Opción Blueprint: New → Blueprint, selecciona `Juulren/RCC`, la rama anterior y `render.yaml`. La configuración fija Docker, plan Free, despliegue automático desactivado y health check `/api/health/ready`. No declara bases ni discos de pago. Comprueba el resumen de recursos/precio antes de confirmar.

Opción manual: New → Web Service → repositorio RCC → rama anterior → Language **Docker** → Dockerfile `./Dockerfile` → contexto raíz → plan **Free** → health check `/api/health/ready`. Deja el comando Docker predeterminado.

Carga las variables privadas requeridas:

| Variable | Valor / origen |
|---|---|
| ConnectionStrings__Rcc | Cadena privada de Neon del paso 2. |
| DataProtection__CertificateBase64 | Archivo privado del paso 3. |
| DataProtection__CertificatePassword | Archivo privado del paso 3. |
| ASPNETCORE_ENVIRONMENT | Production |
| DOTNET_ENVIRONMENT | Production |
| Deployment__RenderProxy | true |
| Deployment__ApplyMigrations | true |
| OpenAI__Enabled | false inicialmente |
| Resend__Enabled | false inicialmente |

Render proporciona `RENDER`, `RENDER_EXTERNAL_HOSTNAME`, `RENDER_EXTERNAL_URL` y `PORT`. RCC escucha en el puerto indicado y usa esa URL HTTPS como origen público. Si incorporas un dominio propio, configura y valida `Deployment__PublicOrigin`; el modo RenderProxy actual verifica el hostname administrado, por lo que requiere adaptación explícita para dominios propios.

No habilites globalmente `ASPNETCORE_FORWARDEDHEADERS_ENABLED`: RCC configura un único salto y no acepta X-Forwarded-Host. El modo RenderProxy confía en las direcciones dinámicas del ingreso administrado de Render y solo se habilita cuando coinciden sus variables de plataforma. **No reproduzcas ese modo exponiendo directamente un contenedor en una red no confiable.** El dominio público permitido se obtiene de configuración validada.

## 5. Migración y primera comprobación

El primer inicio, con `Deployment__ApplyMigrations=true`, aplica las migraciones antes de aceptar tráfico. Si fallan, el despliegue no queda listo. El flag es explícito; no hay borrado/recreación de datos ni migración implícita en Development. El usuario de despliegue necesita permisos DDL. Para una configuración permanente más estricta conviene separar el rol migrador del rol de ejecución.

El plan Free no ofrece las mismas herramientas de predeploy/shell que los planes de pago. Por eso se admite la migración al inicio para esta instancia académica. No aumentes réplicas ni cambies el procedimiento sin revisar la coordinación de migraciones. También existe el comando separado:

```powershell
dotnet artifacts/publish/Rcc.Api.dll --migrate
```

Requiere toda la configuración de producción en el entorno. No lo ejecutes contra una base ajena.

Comprueba en tu URL HTTPS:

1. `/api/health/live`: Healthy; `/api/health/ready`: Healthy.
2. `/api/status`: módulo 9. Inicio y recarga directa de `/habitaciones`, `/cuenta` y `/reservar`.
3. Registro, login, logout; sesión segura y acceso de cliente sin administración.
4. Reserva de prueba, cotización, stock y cancelación.
5. Reinicio del servicio: catálogo/reservas persisten y las claves cifradas pueden recuperarse. La sesión sigue su vencimiento normal.

No consideres la publicación validada solo por ver la portada. Usa la lista de aceptación de MODULO-9.md.

## 6. Administrador propio

Inicializa un administrador explícitamente contra la base Neon siguiendo MODULO-3.md. Debes suministrar en el proceso de inicialización la conexión/certificado de producción y `Administrator:Email` / `Administrator:Password`; no hay usuario/contraseña predeterminados. El comando `--initialize-identity` migra de forma idempotente y nunca promueve una cuenta cliente existente. Retira la contraseña de inicialización del entorno al terminar. No guardes estas credenciales en el Blueprint ni en scripts versionados.

## 7. Activar servicios en el dominio publicado

- **Stripe sandbox:** configura clave de prueba del sandbox y crea un endpoint webhook **nuevo** para `https://TU_HOST/api/payments/stripe/webhook`. Su secreto es diferente al de Stripe CLI local. Usa eventos Checkout documentados en STRIPE-SANDBOX.md. `Stripe__PublicOrigin` toma el origen público si no lo defines; verifica el retorno y pago desde HTTPS.
- **Resend:** activa `Resend__Enabled=true` y configura ApiKey/From privados. El remitente de prueba conserva las restricciones de destinatario de tu cuenta. Comprueba recepción; aceptación HTTP no equivale a llegada a bandeja.
- **Maps:** configura `GoogleMaps__EmbedKey`; agrega el referente HTTPS publicado y restringe la API a Maps Embed. Es una clave pública en el iframe, no un secreto de backend.
- **Clima:** no requiere una clave para esta integración; comprueba fuente/hora y alternativa ante fallo.
- **IA:** la decisión actual es **Gemini en un proyecto Free**, según [GEMINI.md](GEMINI.md). Configura sus variables privadas y despliega la rama del ajuste; no necesita PC ni túnel. La inferencia real queda pendiente hasta comprobar `AI_RANKED`. Ollama/puente queda como alternativa documentada para desarrollo, sin fallback automático.

## 8. Límites, operación y reversión

Al dormir Render Free, los workers de notificaciones, conciliación y reembolsos también se detienen. Retoman trabajo cuando el servicio despierta. Las consultas de disponibilidad consideran el vencimiento del bloqueo; no se promete que todos los trabajos se ejecuten exactamente a la hora prevista mientras el servicio duerme. Stripe puede necesitar reintentos de entrega durante un arranque frío.

Mantén las claves de proveedores fuera de Git e imagen; revisa uso y límites de las cuentas. Un rollback de imagen **no revierte una migración PostgreSQL**. Antes de aplicar migraciones en un entorno con datos importantes, crea un respaldo/snapshot dentro de las opciones de tu plan y verifica su restauración. No borres la base Neon para resolver un fallo de despliegue.

Render/Neon no se declaran operativos hasta ejecutar las comprobaciones HTTPS con tus cuentas. Esta guía y el Blueprint preparan la publicación; no constituyen una contratación ni una comprobación de las cuentas externas.

Referencias: [Render Free](https://render.com/docs/free), [Docker en Render](https://render.com/docs/docker), [variables administradas](https://render.com/docs/environment-variables), [Neon Free](https://neon.com/pricing), [TLS en Npgsql](https://www.npgsql.org/doc/security.html).
