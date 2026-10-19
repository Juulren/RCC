# Módulo 3: identidad y acceso

Fecha: 2026-10-19. Responsable único: Juulren. Rama: `codex/modulo-3-identidad`.
Código generado por IA; revisión humana pendiente. PR apilado sobre módulo 2.

## Entrega

«Mi estancia» abre `/cuenta` desde cualquier header, también en tablet y móvil.
Permite crear una cuenta, entrar, consultar email/rol, conservar sesión al recargar
y cerrar todas las sesiones de ese usuario. Formularios ES/EN, requisitos de
contraseña, mostrar/ocultar contraseña, estados de carga, errores y reintento.
Registro exitoso pide iniciar sesión; no crea una sesión silenciosamente.

`/admin` consulta autorización real en C#: anónimo recibe 401, cliente 403 y
administrador 200. Esta página acredita acceso; no incorpora catálogo ni botones
ficticios de gestión del módulo 4. El espacio de cliente explica que las reservas
llegarán en el módulo 5. No se añaden pagos ni proveedores externos.

## API y datos

- GET `/api/auth/csrf`: cookie antiforgery y token para `X-CSRF-TOKEN`.
- POST `/api/auth/register`: email y password, 201; solo asigna Customer.
- POST `/api/auth/login`: 204 y cookie; credenciales inválidas/bloqueadas → 401 genérico.
- POST `/api/auth/logout`: sesión + CSRF, 204; cambia security stamp y elimina cookie.
- GET `/api/auth/me`: sesión → id, email y roles; anónimo → 401.
- GET `/api/admin/access`: política Admin → 200; 401/403 según corresponda.
- `/api/status` informa módulo 3, etapa `identity`; conserva `application: RCC`.

Errores propios usan ProblemDetails con `code` estable, traducido en frontend;
400 validación/CSRF, 409 email no disponible, 429 límite de intentos. Contratos de
registro/login aceptan únicamente email/password, nunca una asignación de roles.

RccDbContext hereda IdentityDbContext. Migración `AddIdentity` crea tablas estándar
Identity y roles Customer/Admin con IDs estables, sin usuarios ni contraseñas de
muestra. Índice único sobre email normalizado, además del username único estándar;
registro + rol en una transacción, con conflictos concurrentes devueltos como 409.
La migración conserva Hotels y los datos anteriores.

## Sesión y protección

- Hash de contraseña administrado por Identity, sin cifrado reversible.
- Contraseña de 12 a 128 caracteres, mayúscula, minúscula, número y carácter no
  alfanumérico; email validado y normalizado, máximo 254 caracteres.
- Cookies HttpOnly, Path=/, SameSite=Lax para sesión, Strict para antiforgery.
  Secure obligatorio y prefijo `__Host-` en producción. HTTP solo para desarrollo
  y entorno Testing; pruebas verifican también configuración Production vía HTTPS.
- Sesión de navegador con ticket máximo de 8 horas, sin remember-me ni renovación
  deslizante. Se conserva al recargar; no se promete continuidad tras cerrar navegador.
- Todos los POST de auth validan CSRF, incluidos registro/login. El frontend pide
  token nuevo por operación, pues está ligado a la identidad actual.
- Logout actualiza security stamp; validación en cada petición invalida también
  cookies copiadas y otras sesiones. Implica consulta a la base por petición autenticada.
- Bloqueo de cuenta por 15 minutos tras 5 contraseñas fallidas; respuesta genérica.
  Límite adicional de 12 intentos/minuto por IP y endpoint de login/registro.
- Respuestas de identidad con `no-store`; tokens y contraseñas fuera de localStorage.
  Al volver a enfocar el navegador, el cliente vuelve a comprobar la sesión.
- No se registra el cuerpo de credenciales ni se habilita sensitive-data logging.

Confirmación de correo, recuperación de contraseña y MFA no forman parte de esta
entrega. Los emails no se marcan confirmados; el acceso no exige confirmación.
El correo real corresponde al módulo 7. Antes de publicar, el módulo 9 debe
configurar HTTPS/proxy confiable, persistencia de claves Data Protection y revisar
la política de cuentas. Los límites de intentos actuales viven en cada proceso;
la publicación multiinstancia requerirá coordinación adicional.

## Ejecutar y crear administrador

Aplicar migraciones y compilar frontend como indica README; no hay migración
implícita al arrancar la API. Para administrador se usa un comando explícito que
aplica migraciones y crea la cuenta desde configuración, sin credenciales fijas.
Ejemplo PowerShell, introduciendo datos sin guardar contraseña en historial:

```powershell
$env:Administrator__Email = Read-Host 'Correo del administrador'
$rccAdminPassword = Read-Host 'Contraseña (12–128 caracteres)' -AsSecureString
$env:Administrator__Password = [System.Net.NetworkCredential]::new('', $rccAdminPassword).Password
try {
    dotnet run --project src/Rcc.Api -- --initialize-identity
} finally {
    Remove-Item Env:Administrator__Password -ErrorAction SilentlyContinue
    Remove-Item Env:Administrator__Email -ErrorAction SilentlyContinue
    $rccAdminPassword = $null
}
```

Usa la misma conexión local de user-secrets del README. El comando termina sin
levantar el servidor; iniciar después con `dotnet run --project src/Rcc.Api`.
Si el administrador ya existe, conserva contraseña y roles. Rechaza promover una
cuenta cliente existente: elegir un email nuevo. No se creó administrador por
defecto en el equipo del responsable. Tampoco hay endpoint público para promover.

## Evidencia y reproducción

1. `dotnet test RCC.sln --configuration Release`: 18 pruebas con PostgreSQL,
   incluyendo las seis de base. Configurar RCC_TEST_POSTGRES según README.
2. `npm test --prefix frontend`: siete pruebas del frontend existente.
3. `npm run test:browser --prefix frontend`: 19 recorridos de diseño, aislados de
   sesión/API. Incluyen la matriz ES/EN a 360/768/1440 px y movimiento reducido.
4. Con API real en 5080, migraciones y `node scripts/build-web.mjs`:
   `npm run test:auth --prefix frontend`: tres recorridos Chromium, sin mocks de
   autenticación. Registro, fallo de contraseña, login, recarga, cookie HttpOnly,
   rol cliente/403, logout/401, sesión ausente y recuperación tras fallo de red.
   ES móvil 360 px y EN escritorio 1440 px. Datos ficticios `@example.test`.

47 comprobaciones locales aprobadas. Las pruebas .NET crean bases `rcc_test_*`
aisladas y las eliminan al finalizar. Los recorridos auth crean usuarios ficticios
en la base elegida; CI utiliza su propia base efímera. No apuntarlos a producción.
Reportes en artifacts/test-results, frontend/playwright-report y
frontend/playwright-report-auth; capturas y trazas en sus test-results respectivos.

Se probaron CSRF faltante/falso/de otro navegador/de identidad anterior, hashing,
correo duplicado y concurrencia, rechazo de roles enviados en registro, bloqueo,
límite de intentos, cookie Production, invalidación de cookie copiada, permisos y
bootstrap repetible que no promueve clientes. Build sin errores ni advertencias.
En Windows se detuvo la API antes de recompilar su apphost, que estaba bloqueado
por el proceso de ejecución; no fue un fallo de las pruebas de identidad.

CI conserva el diseño aislado y ejecuta el nuevo recorrido auth contra la API
publicada, PostgreSQL y migraciones reales. No se declara integración de proveedores.

## Referencias

- [Configuración de Identity](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-configuration?view=aspnetcore-10.0).
- [Antiforgery](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0).
