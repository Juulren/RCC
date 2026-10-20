# RCC

Proyecto académico de reservas de un hotel en Chongqing.

Repositorio: https://github.com/Juulren/RCC

## Estado

Módulo 5: disponibilidad por fechas, cotización C# y reservas de varias habitaciones
con inventario PostgreSQL. `/reservar` distribuye adultos y edades de niños,
revalida precios y crea un bloqueo de 30 minutos; `/mis-reservas` permite consultar
y cancelar pendientes propias. `/admin` mantiene catálogo e inventario y consulta
reservas. Roles, CSRF, idempotencia y transacciones protegen las escrituras.
Se conserva el diseño responsivo ES/EN y la portada. Tarifas y stock son ficticios.
Pagos y confirmación verificada corresponden al módulo 6.
Todavía no hay despliegue público ni integraciones externas configuradas.

## Documentación

- [Especificación funcional, datos y API](docs/ESPECIFICACION.md)
- [Diseño y bocetos](docs/DISENO.md)
- [Módulos y aceptación](docs/MODULOS.md)
- [Trabajo individual y bitácora](docs/TRABAJO-INDIVIDUAL.md)
- [Evidencias del módulo 1](docs/MODULO-1.md)
- [Sistema visual y pruebas del módulo 2](docs/MODULO-2.md)
- [Identidad, administrador y pruebas del módulo 3](docs/MODULO-3.md)
- [Catálogo, inventario y administración del módulo 4](docs/MODULO-4.md)
- [Disponibilidad, cotización y reservas del módulo 5](docs/MODULO-5.md)
- [Fuentes y licencias de imágenes/componentes](THIRD_PARTY.md)

## Arquitectura prevista

React + Vite (JavaScript y CSS) consume una API REST C# con ASP.NET Core 10
y Entity Framework Core sobre PostgreSQL. ASP.NET sirve la compilación de React
bajo el mismo dominio. Identity almacena usuarios y roles y emite cookies de sesión. Integraciones previstas desde C#: Stripe de prueba,
Open-Meteo, Resend y OpenAI; Google Maps se carga en el navegador con clave
pública restringida por dominio.

## Instalación y pruebas

Requisitos: SDK .NET 10 LTS, Node.js 24 LTS y PostgreSQL 17. Docker Desktop es
opcional para ejecutar PostgreSQL con Compose. En Windows, abrir una terminal
nueva después de instalar herramientas para actualizar PATH.

### 1. Restaurar dependencias (desde la raíz)

```powershell
dotnet --version
node --version
dotnet tool restore
dotnet restore RCC.sln
npm ci --prefix frontend
```

### 2. Configurar PostgreSQL: elegir una alternativa

**Docker Compose:** copiar `.env.example` a `.env`, cambiar la contraseña de
ejemplo y ejecutar `docker compose up -d postgres`. El puerto es 5432 y el volumen
conserva datos entre reinicios. ASP.NET no carga `.env`: configurar además la
conexión, sustituyendo `YOUR_LOCAL_PASSWORD` por el valor elegido:

```powershell
dotnet user-secrets set "ConnectionStrings:Rcc" "Host=127.0.0.1;Port=5432;Database=rcc;Username=rcc;Password=YOUR_LOCAL_PASSWORD" --project src/Rcc.Api
```

**PostgreSQL nativo en Windows (alternativa instalada):**

```powershell
./scripts/dev-postgres.ps1
```

El script requiere binarios PostgreSQL instalados. Por defecto busca en
`$env:LOCALAPPDATA/RCC/tools/postgresql17/pgsql/bin`; admite `-PostgresBin` para
otra ubicación. Crea una instancia exclusiva del proyecto en `.local/`, en
127.0.0.1:55432, genera contraseña local y guarda la conexión en user-secrets.
No instala un servicio global ni descarga binarios. No compartir `.local/`:
contiene datos y credenciales locales y está excluido de Git.

Para otra instalación PostgreSQL, crear una base `rcc` y guardar su conexión con
el mismo comando user-secrets. En producción usar `ConnectionStrings__Rcc` como
variable de entorno, no user-secrets ni credenciales incluidas en archivos.

### 3. Migrar e iniciar

```powershell
dotnet ef database update --project src/Rcc.Api
node scripts/build-web.mjs
dotnet run --project src/Rcc.Api
```

Abrir http://localhost:5080. El frontend y `/api/status` se sirven juntos.
La aplicación no migra automáticamente al iniciar: debe aplicarse la migración
explícitamente. La migración inicial crea `Hotels`; el inventario y las tablas
de autenticación/reservas se agregarán en sus módulos.

Para desarrollar React con recarga automática, mantener la API ejecutándose y,
en otra terminal, usar `npm run dev --prefix frontend`. Abrir http://localhost:5173;
Vite reenvía `/api` a 5080. La compilación y publicación no dependen del servidor Vite.

`dotnet run` usa el perfil Development y permite leer user-secrets. Si se usa
`--no-launch-profile` o un DLL publicado, establecer `ASPNETCORE_ENVIRONMENT=Development`
para user-secrets o proporcionar `ConnectionStrings__Rcc` explícitamente.

### 4. Ejecutar pruebas

```powershell
npm test --prefix frontend
npm exec --prefix frontend -- playwright install chromium
npm run test:browser --prefix frontend
dotnet build RCC.sln --configuration Release
```

Las 53 pruebas de integración necesitan una instancia **dedicada a pruebas** y
un usuario con `CREATEDB`. Cada fixture crea dos bases `rcc_test_<uuid>` y elimina solo esas
bases al terminar; nunca se deben apuntar a producción. Para la instancia nativa
de este proyecto, cargar su configuración sin imprimir la contraseña:

```powershell
$rccSettings = Get-Content .local/postgres.json -Raw | ConvertFrom-Json
$env:RCC_TEST_POSTGRES = "Host=127.0.0.1;Port=$($rccSettings.Port);Database=postgres;Username=rcc;Password=$($rccSettings.Password)"
dotnet test RCC.sln --configuration Release --logger trx --results-directory artifacts/test-results
```

Con Compose u otro servidor de pruebas, establecer `RCC_TEST_POSTGRES` con su
conexión antes de `dotnet test`. Si falta, las pruebas fallan con instrucciones;
no se omiten ni se reemplazan con EF InMemory. CI proporciona PostgreSQL efímero.

### 5. Publicación local y salud

```powershell
node scripts/build-web.mjs
dotnet publish src/Rcc.Api --configuration Release --output artifacts/publish
```

Para ejecutar el resultado, configurar `ConnectionStrings__Rcc` y pasar un
`--contentRoot` absoluto hacia `artifacts/publish`. En PowerShell:

```powershell
dotnet artifacts/publish/Rcc.Api.dll --contentRoot "$((Get-Location).Path)/artifacts/publish" --urls http://127.0.0.1:5081
```

- `/api/status`: contrato de conectividad frontend/API.
- `/api/health/live`: 200 si el proceso responde, independientemente de PostgreSQL.
- `/api/health/ready`: 200 con base accesible y migraciones aplicadas; 503 en caso contrario.
- `/api/*` desconocida: 404 ProblemDetails, nunca HTML de la SPA.

GitHub Actions compila ambos proyectos, ejecuta 7 pruebas Vitest, 21 de diseño,
53 de integración PostgreSQL, 3 recorridos de autenticación, 2 de catálogo y 4 de reservas reales.
Verifica migraciones y la publicación conjunta. Las pruebas de diseño sirven UI
en 4173 con respuestas de catálogo/estado/ausencia de sesión simuladas; las de identidad y administración usan el servidor
C# publicado en 5080, sin simular registro/login/logout ni las modificaciones del catálogo. Se guardan reportes y
capturas. Para repetir estas últimas, con API y migraciones listas:

```powershell
npm run test:auth --prefix frontend
```

La contraseña PostgreSQL de CI es pública, ficticia y exclusiva del servicio efímero
del job. El administrador de pruebas usa una contraseña aleatoria y enmascarada.

El módulo visual incluye auroras y trazos de luz animados, entradas al hacer
scroll y transiciones entre habitaciones y páginas. «Pausar fondo» detiene el
ambiente; la preferencia del sistema de movimiento reducido desactiva los efectos.

### Docker Desktop en este equipo

Docker Desktop ya está operativo después de habilitar Virtual Machine Platform,
reiniciar Windows y actualizar WSL a 2.7.13 con elevación de administrador.
Se verificó `docker version`, `docker compose up -d --wait postgres` y estado
`healthy`. La API local usa ahora PostgreSQL del contenedor en 127.0.0.1:5432,
con conexión guardada en user-secrets y contraseña generada en `.env` (ignorado).
La instancia nativa conserva sus archivos; no se transfirieron ni borraron datos.

Para pruebas usando la configuración local de Compose, sin imprimir credenciales:

```powershell
$rccEnv = @{}
Get-Content .env | ForEach-Object {
  if ($_ -match '^([^#=]+)=(.*)$') { $rccEnv[$Matches[1]] = $Matches[2] }
}
$env:RCC_TEST_POSTGRES = "Host=127.0.0.1;Port=5432;Database=postgres;Username=$($rccEnv.POSTGRES_USER);Password=$($rccEnv.POSTGRES_PASSWORD)"
dotnet test RCC.sln --configuration Release
```

Para otros equipos donde falten requisitos de Windows, instalar WSL y habilitar
Virtual Machine Platform desde una terminal de administrador, reiniciar cuando
Windows lo solicite y comprobar `wsl --version` y `docker version` antes de Compose.

Referencias: [Docker en Windows](https://docs.docker.com/desktop/setup/install/windows-install/)
y [solución de problemas WSL](https://learn.microsoft.com/en-us/windows/wsl/troubleshooting).

Detener PostgreSQL nativo: `./scripts/dev-postgres.ps1 -Stop`.
Detener Compose conservando datos: `docker compose down`.

## Flujo de trabajo

Repositorio oficial: https://github.com/Juulren/RCC.git. Una rama y un PR por
módulo; commits con `[IA]`, `[HUMANO]` o `[MIXTO]`. Responsable único: Juulren.
El autor técnico de los commits generados en esta sesión es Codex; la etiqueta
declara generación por IA y no acredita revisión humana.

## Contexto académico

Demostración inspirada en Regent Chongqing; no es el sitio oficial ni conecta
con sus reservas reales. El home conserva habitaciones ilustrativas; la nueva
colección presenta siete tipos del hotel con fotografías oficiales y atribución.
Tarifas e inventario de la aplicación serán ficticios;
pagos exclusivamente de prueba. Fuente de requisitos: `Tarea-1.pdf`, facilitado
por el usuario. La modalidad individual difiere del equipo de cuatro personas
pedido por la consigna; véase la documentación de trabajo individual.

## Páginas del módulo visual

El home `/` conserva su contenido. El navbar abre páginas independientes mediante
React Router: `/habitaciones`, `/experiencias` y `/destino`. Se pueden compartir
las URLs y recargar directamente; ASP.NET sirve el fallback de la SPA.

Habitaciones incluye siete tipos oficiales, filtros, búsqueda y galerías con
fotografías de Regent / IHG. No consulta inventario ni ofrece reservas reales.
Experiencias propone momentos editoriales y Destino presenta Chongqing con un
esquema conceptual y enlace externo a Maps. El idioma ES/EN se conserva al navegar.
Fuentes y derechos de las imágenes: [THIRD_PARTY.md](THIRD_PARTY.md).


## Catálogo y administración

La migración `AddCatalog` carga siete tipos, once referencias fotográficas y
catorce habitaciones físicas `DEMO-*`. Tarifas, capacidades e inventario son
ficticios; dirección y referencias del hotel tienen atribución. Las coordenadas
iniciales son aproximadas. Las siguientes migraciones no reinicializan las
ediciones del administrador.

Crear el administrador propio con el procedimiento de [MODULO-3.md](docs/MODULO-3.md)
y entrar a `/cuenta` → «Acceder a administración». No hay credenciales fijas.
La galería usa una biblioteca local acreditada; no incorpora subida de archivos.
Los cambios desactualizados se rechazan con 409 y se conserva el borrador.

Para probar el panel completo en una **base aislada**, con PostgreSQL Compose
activo y `.env` configurado (usuario con CREATEDB), después de compilar Release
y ejecutar `node scripts/build-web.mjs`:

```powershell
./scripts/test-catalog.ps1
```

El script crea una base `rcc_e2e_<uuid>`, un administrador con contraseña aleatoria
y una API temporal en 5082. Ejecuta los dos recorridos de catálogo y limpia solo
esa base al finalizar, incluso si falla una prueba. No modifica las cuentas del
hotel local. La suite de autenticación `test:auth`, en cambio, usa la base de la
API seleccionada y genera clientes ficticios; no debe apuntarse a producción.

## Reservas de demostración (módulo 5)

Abrir `/reservar`, elegir fechas/distribución, consultar opciones, cotizar y
aceptar el importe. Tras iniciar sesión, continuar el borrador desde `/cuenta`;
volver a buscar/cotizar para comprobar datos actuales. La reserva pendiente se
consulta en `/mis-reservas` y bloquea inventario 30 minutos. Cancelar antes del
día local de entrada libera todas las unidades. No hay cobro ni confirmación de
pago en este módulo. El límite es 8 habitaciones, 30 noches y llegada dentro de
365 días, con al menos un adulto por habitación.

Para ejecutar el recorrido real sin tocar reservas del usuario:

```powershell
node scripts/build-web.mjs
dotnet build RCC.sln --configuration Release
./scripts/test-reservations.ps1
```

El script crea una base y administrador temporales a partir de la configuración
local de Compose, inicia una API en 5083 y limpia solo esa base al terminar.
Prueba las reservas a 360/768/1440 px, ES/EN, cancelación, respuesta perdida,
reintento idempotente, precio actualizado y sesión cerrada. Ver MODULO-5.md para
contratos, transacciones y límites antes de incorporar pagos.
