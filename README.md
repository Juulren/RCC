# RCC · Hotel futurista en Chongqing

Aplicación académica responsiva de reservas, inspirada en **Regent Chongqing**. Combina un diseño de lujo futurista, interfaz en español e inglés, administración del hotel, reservas de varias habitaciones, pagos de prueba y recomendaciones con IA.

Repositorio oficial: [Juulren/RCC](https://github.com/Juulren/RCC). Responsable único: **Juulren**, con asistencia de Codex. No es el sitio oficial del hotel: las tarifas, el inventario y los pagos son de demostración.

## Estado del proyecto

**Código fuente:** esta actualización publica la documentación en `main`. La implementación completa sigue en la [rama de desarrollo](https://github.com/Juulren/RCC/tree/codex/puente-ia-seguro), pendiente de integrar mediante sus PR. Los enlaces técnicos de esta portada apuntan a la versión documentada del proyecto.

Los módulos **0–8 están implementados**. El responsable confirmó en desarrollo local pago Stripe sandbox, recepción de correo Resend, cancelación, Maps y recomendaciones con Ollama. También se implementó y probó localmente un puente autenticado para conectar un backend remoto con la IA de la PC.

**El despliegue público del módulo 9 sigue pendiente:** Docker para la aplicación, Neon, configuración de producción, HTTPS, comprobaciones del sitio publicado y entrega final. El puente no está conectado todavía a un backend publicado: faltan abrir/configurar el túnel y establecer sus variables privadas. No se han fusionado los PR automáticamente.

## Especificaciones y funcionalidades

| Área | Comportamiento |
|---|---|
| Sitio público | Inicio, habitaciones, experiencias y destino; navegación por páginas, filtros, galerías y contenido ES/EN. |
| Diseño responsivo | Adaptación a móvil, tablet y escritorio; comprobaciones a 360, 768 y 1440 px. Fondo ambiental animado, transiciones y alternativa de movimiento reducido. |
| Cuentas | Registro, inicio/cierre de sesión y roles cliente/administrador con ASP.NET Core Identity. |
| Administración | Hotel, servicios, galería, tipos de habitación, habitaciones físicas, capacidades y tarifas persistentes. |
| Disponibilidad | Consulta por fechas y distribución de huéspedes; el servidor comprueba capacidad e inventario. |
| Reservas | Varias habitaciones en una transacción, cotización calculada por C#, aceptación explícita del precio y consulta en Mis reservas. |
| Pagos | Stripe Checkout en sandbox, webhook firmado, confirmación, conciliación y trabajos durables de reembolso. |
| Cancelación | Cancelación antes de la entrada, liberación de inventario y solicitud de reembolso cuando corresponde. |
| Servicios externos | Clima actual, mapa integrado automático y correos de confirmación/cancelación mediante cola SQL y reintentos. |
| IA | Orden de combinaciones según presupuesto, vista al río, espacio y precio. El usuario elige; la IA no reserva ni modifica tarifas. |

### Reglas de negocio

- Una reserva admite entre **1 y 8 habitaciones**, con las mismas fechas, hasta **30 noches** y llegada dentro de **365 días**.
- Cada habitación necesita al menos un adulto y debe respetar su capacidad; se consideran niños los menores de 18 años.
- Las fechas se interpretan en Chongqing, **UTC+8**: entrada inclusiva y salida exclusiva.
- Los precios de demostración están en **CNY por habitación/noche**. El servidor calcula y conserva el importe de la reserva.
- Una reserva pendiente bloquea inventario **30 minutos**. La confirmación depende del pago verificado por C#, no del retorno del navegador desde Stripe.
- Cambiar fechas requiere cancelar y reservar nuevamente. La cancelación gratuita se aplica antes del día local de entrada.
- Las recomendaciones no bloquean inventario. El servidor vuelve a comprobar disponibilidad y precio antes de reservar.

## Tecnologías utilizadas

| Componente | Tecnologías |
|---|---|
| Backend | **C#**, ASP.NET Core Web API sobre **.NET 10 LTS**. |
| Persistencia | Entity Framework Core 10, Npgsql y **PostgreSQL 17**; migraciones y transacciones. |
| Identidad | ASP.NET Core Identity, roles, cookies de sesión y protección CSRF. |
| Frontend | **React 19**, JavaScript, HTML y CSS, con **Vite 8**. |
| Navegación y animación | React Router 7, Motion y animaciones CSS; patrones visuales inspirados/adaptados de React Bits y Reacticx según las atribuciones del proyecto. |
| IA local | **Ollama + Qwen 2.5 1.5B**, con salida estructurada y validación C#. |
| Puente de IA | Proceso ASP.NET separado, autenticación por credencial aleatoria y transporte HTTPS mediante túnel temporal. |
| Pruebas | xUnit, ASP.NET TestHost/WebApplicationFactory, PostgreSQL real, Vitest, Testing Library y Playwright. |
| Herramientas | Git, GitHub Actions, Docker Compose para PostgreSQL y Gitleaks para detección de secretos. |
| Publicación prevista | Frontend y API bajo el mismo dominio en Render; PostgreSQL en Neon. Pendiente del módulo 9. |

Las versiones exactas de dependencias están en los archivos `.csproj`, `frontend/package.json` y `frontend/package-lock.json`. Las fuentes, adaptaciones y licencias de imágenes/componentes están en [THIRD_PARTY.md](https://github.com/Juulren/RCC/blob/927a6ce6a5f1f10634f99812ec9b75e96317e672/THIRD_PARTY.md).

## Integraciones

| Servicio | Uso y estado |
|---|---|
| **Stripe sandbox** | Pagos de prueba, webhook firmado, conciliación y reembolsos. Pago y cancelación comprobados por el responsable en local; no implica cobros reales. |
| **Resend** | Correos transaccionales mediante cola SQL. Recepción comprobada por el responsable; las restricciones del remitente de prueba dependen de la cuenta configurada. |
| **Open-Meteo** | Clima actual de Chongqing, con caché y alternativa ante fallos. No es un pronóstico para las fechas de la reserva. |
| **Google Maps Embed** | Mapa automático en Destino y enlace alternativo. La clave de Embed es pública en el navegador y necesita restricciones de API y referente. |
| **Ollama local** | IA sin cargos por consulta, ejecutada en la PC. C# redacta las explicaciones con cifras verificadas para evitar detalles inventados por el modelo pequeño. |
| **Cloudflare Quick Tunnel** | Conexión temporal prevista entre el backend y el puente local. Cloudflared instalado; falta configurar y comprobar el acceso desde el despliegue. |
| **OpenAI, opcional** | Proveedor alternativo de pago mediante Responses API; no se necesita para usar Ollama. No se ha acreditado una inferencia real con este proveedor. |

Sin proveedor de IA disponible, RCC conserva recomendaciones por cálculo determinista y avisa que no intervino IA. Ningún fallo del puente cambia automáticamente a un proveedor de pago.

## Arquitectura y estructura

React consume `/api` en ASP.NET. En ejecución conjunta, C# sirve también la compilación de React; en desarrollo Vite reenvía las peticiones de API al servidor local. PostgreSQL almacena usuarios, catálogo, reservas, pagos, eventos procesados y notificaciones pendientes.

| Ruta del repositorio | Contenido |
|---|---|
| `frontend/` | Aplicación React, estilos y pruebas de componentes/navegador. |
| `src/Rcc.Api/` | API C#, Identity, EF Core, reglas de reserva y servicios externos. |
| `src/Rcc.Ai/` | Contratos compartidos y conectores de IA local/remota. |
| `src/Rcc.AiRelay/` | Puente autenticado que se ejecuta en la PC junto a Ollama. |
| `tests/Rcc.Api.IntegrationTests/` | Pruebas de API, PostgreSQL, proveedores y seguridad del puente. |
| `tests/Rcc.PaymentHarness/` | Servidor aislado para probar pagos con proveedor simulado. |
| `scripts/` | Compilación web, PostgreSQL local, configuración IA y pruebas aisladas. |
| `docs/` | Especificación, entregas por módulo, configuración, bitácora y evidencias. |
| `.github/workflows/` | CI de compilación/pruebas y escaneo de secretos. |

La API incluye `/api/auth/*`, `/api/hotel`, `/api/room-types`, `/api/availability`, `/api/quotes`, `/api/reservations`, `/api/payments/*`, `/api/weather`, `/api/location`, `/api/recommendations` y `/api/admin/*`. Los detalles de contratos y permisos están en la documentación de cada módulo.

## Requisitos para ejecutar

- **SDK .NET 10**, no solo el runtime.
- **Node.js 24 LTS** y npm.
- **PostgreSQL 17**; el procedimiento recomendado usa Docker Desktop con Docker Compose.
- Git y una terminal PowerShell para los comandos de esta guía. Tras instalar herramientas, abre una terminal nueva para actualizar PATH.
- Opcional: Ollama para IA; Stripe CLI para reenviar webhooks locales; cloudflared para el puente remoto.

No necesitas configurar todos los proveedores para iniciar RCC. Las funciones que requieren credenciales muestran su estado o una alternativa.

## Cómo ejecutarlo localmente

### 1. Obtener el proyecto y restaurar dependencias

Si ya tienes el repositorio, empieza desde su raíz. En otro equipo:

```powershell
git clone https://github.com/Juulren/RCC.git
cd RCC
```

Mientras el PR de estos cambios no esté integrado, usa la rama que contiene la implementación:

```powershell
git switch codex/puente-ia-seguro
```

Una vez integrado en `main`, ese cambio de rama deja de ser necesario. Restaura dependencias:

```powershell
dotnet --version
node --version
dotnet tool restore
dotnet restore RCC.sln
npm ci --prefix frontend
```

### 2. Preparar PostgreSQL con Docker

Inicia Docker Desktop. Crea `.env` solo si aún no existe:

```powershell
if (-not (Test-Path .env)) { Copy-Item .env.example .env }
```

Edita `.env` y establece `POSTGRES_DB`, `POSTGRES_USER` y una contraseña propia en `POSTGRES_PASSWORD`. No uses la contraseña ilustrativa ni subas este archivo. Conserva valores simples sin expansión de variables para el ejemplo siguiente.

```powershell
docker compose up -d --wait postgres
```

PostgreSQL queda en `127.0.0.1:5432`, con un volumen persistente. **ASP.NET no carga `.env` automáticamente**. Guarda también su conexión en user-secrets sin imprimirla:

```powershell
$rccDb = @{}
Get-Content .env | ForEach-Object {
  if ($_ -match '^([^#=]+)=(.*)$') { $rccDb[$Matches[1].Trim()] = $Matches[2].Trim().Trim('"') }
}
$rccConnection = [System.Data.Common.DbConnectionStringBuilder]::new()
$rccConnection.Add('Host', '127.0.0.1')
$rccConnection.Add('Port', '5432')
$rccConnection.Add('Database', $rccDb['POSTGRES_DB'])
$rccConnection.Add('Username', $rccDb['POSTGRES_USER'])
$rccConnection.Add('Password', $rccDb['POSTGRES_PASSWORD'])
@{ 'ConnectionStrings:Rcc' = $rccConnection.ConnectionString } | ConvertTo-Json | dotnet user-secrets set --project src/Rcc.Api
Remove-Variable rccDb, rccConnection
```

Si cambias la contraseña de `.env` después de crear el volumen, PostgreSQL no cambia automáticamente la contraseña del usuario existente. No borres el volumen para resolverlo si contiene datos que quieras conservar.

Alternativa sin Docker en Windows: [scripts/dev-postgres.ps1](https://github.com/Juulren/RCC/blob/927a6ce6a5f1f10634f99812ec9b75e96317e672/scripts/dev-postgres.ps1). Requiere binarios PostgreSQL ya instalados y admite `-PostgresBin`; crea una instancia separada en `.local/`, puerto **55432**, y configura user-secrets. No ejecutes ambos procedimientos contra la misma carpeta de datos.

### 3. Aplicar migraciones, compilar React e iniciar

```powershell
dotnet ef database update --project src/Rcc.Api
node scripts/build-web.mjs
dotnet run --project src/Rcc.Api --launch-profile http
```

Abre **[http://localhost:5080](http://localhost:5080)**. Las migraciones crean las tablas de los módulos implementados y el catálogo inicial de demostración. No se aplican automáticamente al iniciar la API.

Mantén la terminal abierta. Para detenerla, usa **Ctrl+C**. Después de cambiar user-secrets, reinicia el proceso. El perfil `http` utiliza Development; si ejecutas un DLL publicado o usas `--no-launch-profile`, configura el entorno y la conexión explícitamente.

En este equipo existe además `.local/iniciar-rcc.ps1`, un asistente privado que prepara Docker, importa la configuración local e inicia RCC. **No está versionado y no existe al clonar el repositorio**; el procedimiento anterior es el reproducible para otros equipos.

### 4. Desarrollar el frontend con recarga automática

Mantén la API en 5080 y abre otra terminal:

```powershell
npm run dev --prefix frontend
```

Abre **[http://localhost:5173](http://localhost:5173)**. Vite reenvía `/api` a 5080. Para volver a servir React desde C#, ejecuta otra vez `node scripts/build-web.mjs` y recarga 5080.

### 5. Cuentas y administración

Registra un cliente desde `/cuenta`. No hay credenciales fijas de administrador. Para crear el administrador propio, sigue [MODULO-3.md](https://github.com/Juulren/RCC/blob/927a6ce6a5f1f10634f99812ec9b75e96317e672/docs/MODULO-3.md) y ejecuta la inicialización explícita con sus valores privados. Retira la contraseña de inicialización después de usarla.

Páginas principales: `/`, `/habitaciones`, `/experiencias`, `/destino`, `/cuenta`, `/reservar` y `/mis-reservas`. El administrador accede al panel desde su cuenta.

## Cómo habilitar IA local sin pagar una API

Instala y abre [Ollama](https://ollama.com/download/windows). Desde la raíz:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\configurar-ia-local.ps1
```

El script descarga `qwen2.5:1.5b` y configura el proveedor local en user-secrets. Reinicia RCC; después inicia sesión, busca disponibilidad y pulsa **Pedir recomendaciones**. Debe aparecer la indicación de opciones ordenadas con IA. Mantén Ollama abierto.

El modelo ocupa aproximadamente 986 MB de descarga y utiliza memoria/CPU de la PC. No tiene cargos por consulta; la velocidad depende del equipo. [Configuración y límites](https://github.com/Juulren/RCC/blob/927a6ce6a5f1f10634f99812ec9b75e96317e672/docs/IA-LOCAL.md).

### IA de la PC desde un backend publicado

El puente local se inicia con:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\iniciar-puente-local.ps1
```

Esto **no abre un túnel público**: genera una credencial de sesión y escucha únicamente en `127.0.0.1:5092`. El túnel, sus variables privadas en el backend y la prueba desde el sitio público se preparan por separado siguiendo [PUENTE-IA.md](https://github.com/Juulren/RCC/blob/927a6ce6a5f1f10634f99812ec9b75e96317e672/docs/PUENTE-IA.md).

No expongas directamente el puerto de Ollama. La conexión es temporal: PC encendida, credencial con vencimiento inferior a ocho horas y nueva configuración al reiniciar el puente/túnel. La validación del despliegue remoto sigue pendiente.

## Configurar las demás integraciones

| Integración | Guía |
|---|---|
| Stripe test y webhook local | [STRIPE-SANDBOX.md](https://github.com/Juulren/RCC/blob/927a6ce6a5f1f10634f99812ec9b75e96317e672/docs/STRIPE-SANDBOX.md) |
| Google Maps y Resend | [SERVICIOS-EXTERNOS.md](https://github.com/Juulren/RCC/blob/927a6ce6a5f1f10634f99812ec9b75e96317e672/docs/SERVICIOS-EXTERNOS.md) |
| Opciones de proveedores y OpenAI de pago | [CONFIGURACION-SERVICIOS.md](https://github.com/Juulren/RCC/blob/927a6ce6a5f1f10634f99812ec9b75e96317e672/docs/CONFIGURACION-SERVICIOS.md) |
| IA local gratuita | [IA-LOCAL.md](https://github.com/Juulren/RCC/blob/927a6ce6a5f1f10634f99812ec9b75e96317e672/docs/IA-LOCAL.md) |
| Puente remoto autenticado | [PUENTE-IA.md](https://github.com/Juulren/RCC/blob/927a6ce6a5f1f10634f99812ec9b75e96317e672/docs/PUENTE-IA.md) |

En producción las claves se guardan en variables privadas del backend; en Development, en user-secrets. Ninguna clave privada debe ir en variables `VITE_*`, código, capturas, documentación o commits. Las credenciales de Stripe deben ser de sandbox.

## Pruebas y verificación

### Frontend

```powershell
npm test --prefix frontend
npm exec --prefix frontend -- playwright install chromium
npm run test:browser --prefix frontend
```

Vitest comprueba componentes; Playwright revisa diseño, navegación, idiomas y servicios en móvil/escritorio. Las pruebas generales de navegador usan respuestas controladas; no acreditan por sí solas una integración real con proveedores.

### Backend y PostgreSQL

Las pruebas de integración requieren una conexión **de pruebas**, con permiso `CREATEDB`, en `RCC_TEST_POSTGRES`. Cada fixture crea bases con identificador aleatorio y elimina solo sus bases al terminar. Nunca ejecutes estas suites contra producción. Con la instancia local Compose, después de configurar `.env`:

```powershell
$rccTestDb = @{}
Get-Content .env | ForEach-Object {
  if ($_ -match '^([^#=]+)=(.*)$') { $rccTestDb[$Matches[1].Trim()] = $Matches[2].Trim().Trim('"') }
}
$rccTestConnection = [System.Data.Common.DbConnectionStringBuilder]::new()
$rccTestConnection.Add('Host', '127.0.0.1')
$rccTestConnection.Add('Port', '5432')
$rccTestConnection.Add('Database', 'postgres')
$rccTestConnection.Add('Username', $rccTestDb['POSTGRES_USER'])
$rccTestConnection.Add('Password', $rccTestDb['POSTGRES_PASSWORD'])
$env:RCC_TEST_POSTGRES = $rccTestConnection.ConnectionString
Remove-Variable rccTestDb, rccTestConnection
dotnet test RCC.sln --configuration Debug
Remove-Item Env:RCC_TEST_POSTGRES
```

Si falta la conexión, la suite falla con instrucciones; no reemplaza PostgreSQL por EF InMemory. En Windows, detén el proceso correspondiente antes de recompilar/publicar su misma configuración si mantiene DLL bloqueadas.

Para recorridos completos aislados, con `.env` y Docker disponibles, compila Release y React; ejecuta el script apropiado:

```powershell
dotnet build RCC.sln --configuration Release
node scripts/build-web.mjs
.\scripts\test-catalog.ps1
.\scripts\test-reservations.ps1
.\scripts\test-payments.ps1
```

Los scripts crean sus bases y servidores temporales. Pagos usa un harness con Stripe simulado; no cobra tarjetas. La suite `npm run test:auth --prefix frontend` necesita una API configurada y crea clientes de prueba: úsala solo en una base de desarrollo/pruebas.

### CI y detección de secretos

GitHub Actions compila backend/frontend, ejecuta pruebas con PostgreSQL efímero y proveedores simulados, comprueba migraciones y valida la publicación conjunta. Un workflow adicional ejecuta **Gitleaks con salida redactada** sobre el historial disponible.

Las contraseñas públicas del servicio PostgreSQL de CI son exclusivas de su entorno efímero, no claves de producción. Una única excepción de Gitleaks reconoce un marcador ilustrativo histórico de Stripe; no excluye carpetas o commits completos. Si una credencial real se filtra, debe revocarse en el proveedor: eliminarla de un archivo no la revoca.

### Salud y publicación local

- `/api/status`: conectividad y estado general.
- `/api/health/live`: proceso disponible.
- `/api/health/ready`: base accesible y migraciones aplicadas; 503 si falta preparación.
- `/api/payments/status`: disponibilidad del pago y modo de prueba.

Para preparar la publicación conjunta:

```powershell
node scripts/build-web.mjs
dotnet publish src/Rcc.Api --configuration Release --output artifacts/publish
```

Antes de ejecutar el resultado, proporciona `ConnectionStrings__Rcc` y los servicios elegidos en el entorno del proceso. En desarrollo local, con esa conexión ya configurada:

```powershell
dotnet artifacts/publish/Rcc.Api.dll --contentRoot "$((Get-Location).Path)/artifacts/publish" --urls http://127.0.0.1:5081
```

Esto valida una publicación local; **no equivale al despliegue en Render**. Detener PostgreSQL con `docker compose down` conserva el volumen. No uses `down -v` si quieres conservar sus datos.

## Documentación y entregables

| Documento | Contenido |
|---|---|
| [ESPECIFICACION.md](https://github.com/Juulren/RCC/blob/927a6ce6a5f1f10634f99812ec9b75e96317e672/docs/ESPECIFICACION.md) | Historias de usuario, datos, API y reglas. |
| [DISENO.md](https://github.com/Juulren/RCC/blob/927a6ce6a5f1f10634f99812ec9b75e96317e672/docs/DISENO.md) | Dirección visual y bocetos. |
| [MODULOS.md](https://github.com/Juulren/RCC/blob/927a6ce6a5f1f10634f99812ec9b75e96317e672/docs/MODULOS.md) | Entregas, aceptación y trazabilidad académica. |
| [MODULO-1.md](https://github.com/Juulren/RCC/blob/927a6ce6a5f1f10634f99812ec9b75e96317e672/docs/MODULO-1.md) · [MODULO-2.md](https://github.com/Juulren/RCC/blob/927a6ce6a5f1f10634f99812ec9b75e96317e672/docs/MODULO-2.md) | Base técnica y sistema visual. |
| [MODULO-3.md](https://github.com/Juulren/RCC/blob/927a6ce6a5f1f10634f99812ec9b75e96317e672/docs/MODULO-3.md) · [MODULO-4.md](https://github.com/Juulren/RCC/blob/927a6ce6a5f1f10634f99812ec9b75e96317e672/docs/MODULO-4.md) | Identidad, roles, catálogo y administración. |
| [MODULO-5.md](https://github.com/Juulren/RCC/blob/927a6ce6a5f1f10634f99812ec9b75e96317e672/docs/MODULO-5.md) · [MODULO-6.md](https://github.com/Juulren/RCC/blob/927a6ce6a5f1f10634f99812ec9b75e96317e672/docs/MODULO-6.md) | Reservas, pagos y reembolsos. |
| [MODULO-7.md](https://github.com/Juulren/RCC/blob/927a6ce6a5f1f10634f99812ec9b75e96317e672/docs/MODULO-7.md) · [MODULO-8.md](https://github.com/Juulren/RCC/blob/927a6ce6a5f1f10634f99812ec9b75e96317e672/docs/MODULO-8.md) | Servicios externos y recomendaciones. |
| [TRABAJO-INDIVIDUAL.md](https://github.com/Juulren/RCC/blob/927a6ce6a5f1f10634f99812ec9b75e96317e672/docs/TRABAJO-INDIVIDUAL.md) | Responsabilidad, autoría y bitácora. |
| [THIRD_PARTY.md](https://github.com/Juulren/RCC/blob/927a6ce6a5f1f10634f99812ec9b75e96317e672/THIRD_PARTY.md) | Fuentes y licencias de terceros. |

Las evidencias de cada módulo conservan su fecha: pueden describir una configuración que todavía estaba pendiente entonces. El estado actual se resume al inicio de este README y en las guías posteriores de integración.
