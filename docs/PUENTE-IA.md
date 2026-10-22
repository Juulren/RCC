# Puente autenticado entre el backend y Ollama en la PC

El backend puede usar `OpenAI:Provider=OllamaRelay`. La conexión se limita a una URL HTTPS temporal de Cloudflare y al endpoint `/v1/rank`. La PC ejecuta un proceso C# separado, sin acceso necesario a PostgreSQL, Stripe, Resend o OpenAI. Ollama conserva su dirección loopback. La biblioteca `Rcc.Ai` comparte los contratos y la inferencia local; se conserva el namespace anterior para compatibilidad.

Esta opción es para **demostración temporal**: Cloudflare describe Quick Tunnels como desarrollo/pruebas, sin garantía de disponibilidad, con hostname cambiante. No requiere cuenta ni dominio. La autenticación por correo de Quick Tunnels requiere navegador y no sirve para este cliente servidor-a-servidor; por eso el puente aplica su propia autenticación. Para operación permanente se necesita replantear alojamiento/hostname y un túnel administrado.

## Controles implementados

- Solo `127.0.0.1:5092` para el puente y `127.0.0.1:11434` para Ollama. No abrir puertos del router ni reglas de entrada públicas.
- Credencial aleatoria de 32 bytes, enviada solo por Authorization. Comparación de hashes en tiempo constante y rechazo de cabeceras múltiples y solicitudes con Origin de navegador.
- Credencial nueva por inicio; el puente deja de admitir solicitudes tras menos de 8 horas. Detenerlo revoca el acceso de esa sesión. Las solicitudes ya admitidas pueden concluir dentro del timeout.
- Un modelo fijo, Qwen 2.5 1.5B, con esquema JSON; el cliente no puede elegir prompts, modelos, destinos ni opciones de Ollama.
- Solo cifras de hasta 12 combinaciones y preferencias enumeradas. No se transmiten identidad, fechas, edades, correo, claves de pago ni UUID del catálogo. C# construye explicaciones con cifras verificadas y el backend vuelve a validar inventario/precio.
- Máximo 16 KiB por solicitud, una inferencia simultánea sin cola y 30 intentos durante la vida del proceso. El backend conserva sus controles de sesión, CSRF, tres consultas por minuto y cupo diario en PostgreSQL.
- URLs salientes solo HTTPS a un hostname `*.trycloudflare.com` configurado, sin usuario, rutas, query, fragmento ni puerto alternativo. No se siguen redirecciones ni se usan proxies del sistema para enviar la credencial.
- Timeout de inferencia 110 segundos y de cliente 120 segundos; respuestas máximas de 64 KiB. Sin logs de solicitudes, cuerpos ni credenciales en el proceso relay; errores externos se traducen a códigos genéricos.
- Cloudflare termina TLS y es un tercero de confianza que puede procesar el tráfico del túnel. No se afirma confidencialidad frente a ese proveedor. La URL del túnel no es la credencial.

## Inicio manual revisable

El asistente inicia únicamente el puente local. La apertura del túnel y la carga de secretos del backend son pasos separados. Durante la implementación, la revisión automática bloqueó el asistente combinado de publicación del túnel y copia de configuración privada, con motivo genérico `blocked by policy`; no se ejecutó esa automatización.

1. Mantén Ollama abierto y ejecuta desde la raíz:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\iniciar-puente-local.ps1
```

Este script genera una credencial nueva y la guarda en un almacén **separado** `rcc-ai-relay-local`, fuera del repositorio. No imprime la credencial ni la copia al portapapeles. `user-secrets` protege frente a publicación accidental; sus archivos locales no están cifrados. Requiere un perfil Windows protegido y no compartido.

2. En otra terminal, abre el túnel solo al puente, nunca directamente a Ollama:

```powershell
& "${env:ProgramFiles(x86)}\cloudflared\cloudflared.exe" tunnel --no-autoupdate --url http://127.0.0.1:5092 --metrics 127.0.0.1:20242
```

`cloudflared` se instaló con Winget. La URL HTTPS mostrada cambia cada vez que se inicia el túnel. Guarda esa URL como configuración privada del backend. Una petición sin credencial debe recibir **401**, incluso a `/health`; una URL difícil de adivinar no sustituye autenticación.

3. En tu propia sesión, abre el archivo privado del almacén para copiar únicamente el valor `Relay:Token` al administrador de variables de entorno de Render. No lo abras en el editor de código compartido, no hagas capturas ni lo pegues en chat, comandos guardados, archivos del proyecto o documentación. La ubicación habitual de Windows es `%APPDATA%\Microsoft\UserSecrets\rcc-ai-relay-local\secrets.json`. El archivo también contiene el vencimiento; no contiene claves de otros proveedores.

4. Configura estas variables solo en el **servicio backend**, nunca en VITE/frontend:

| Variable | Valor |
|---|---|
| OpenAI__Enabled | true |
| OpenAI__Provider | OllamaRelay |
| OpenAI__Model | qwen2.5:1.5b |
| OpenAI__RelayUrl | URL HTTPS mostrada por tu túnel |
| OpenAI__RelayToken | Valor privado Relay:Token de esta sesión |
| OpenAI__MaxOutputTokens | 1024 |
| OpenAI__DailyRequestLimit | 20 |

No se necesita `OpenAI__ApiKey` para este proveedor. Reinicia el servicio después de guardar variables. Una clave vieja, URL incorrecta, puente cerrado o presupuesto agotado activa el cálculo determinista, sin llamar a un proveedor de pago.

5. Comprueba desde el sitio publicado: sesión iniciada, buscar disponibilidad, pedir recomendaciones, respuesta `mode=ai` y selección/cotización correctas. Mantén PC, Ollama y ambas terminales activos. **Esta comprobación pública está pendiente hasta disponer del despliegue y configurar el túnel.**

6. Al terminar la demostración, Ctrl+C en la terminal del túnel y del puente. Desactiva IA en Render o elimina su token. Iniciar nuevamente el puente cambia la credencial; actualiza Render con la nueva URL/credencial. Una conexión permanente no está incluida en este mecanismo de demostración.

## Comprobaciones y secretos

Se revisaron código/configuración/documentación y el historial accesible de ramas, PRs y reflogs: Gitleaks detectó únicamente dos versiones del marcador ilustrativo de Stripe. Se conserva una excepción estricta para ese texto y archivo históricos; no se ignoran carpetas ni commits completos. Se compararon además 398 blobs con dos secretos disponibles en el almacén del proceso revisor, sin coincidencias exactas. Esto no cubre claves desconocidas antiguas, copias/cachés externas, artefactos de terceros ni metadatos binarios y no prueba que nunca haya existido una filtración.

El workflow `secrets.yml` ejecuta Gitleaks con salida redactada en cada PR y push a main. El control usa las reglas por defecto, con una única excepción de plantilla; una credencial sintética con formato válido en la misma guía sigue siendo detectada. Ante una filtración real, revocar primero en el proveedor: borrar una línea o reescribir un commit no revoca una clave.

La clave de Maps Embed es pública por diseño en el iframe. Verifica en Google Cloud sus restricciones a Maps Embed API y a los referentes exactos de localhost y el sitio publicado; no se comprobó esa configuración desde el repositorio. Mantén .env, .local y user-secrets fuera de Git.

Validación local final: **142 pruebas .NET/PostgreSQL aprobadas**, incluidas 18 del puente/transporte; inferencia real local del puente con respuesta 200 y comprobación de listeners loopback. La validación remota sigue pendiente.

Referencias: [Quick Tunnels y limitaciones](https://developers.cloudflare.com/tunnel/get-started/quick-tunnels/), [Gitleaks](https://github.com/gitleaks/gitleaks).
