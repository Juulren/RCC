# Módulo 1: base técnica

Fecha: 2026-10-16. Rama: `feat/modulo-1-base`.
Responsable único: Juulren. Cambios generados por Codex, revisión humana pendiente.

## Implementado

- Solución .NET 10 con API ASP.NET, EF Core/Npgsql y entidad inicial Hotel.
- Migración `InitialFoundation`: campos bilingües, slug único, coordenadas decimales
  y restricciones PostgreSQL. Sin datos reales ni inventario inventado como oficial.
- Health checks de proceso y de base/migraciones con respuestas públicas mínimas.
- React/Vite conectado a `/api/status`, con carga, error y reintento. Es una pantalla
  de arranque, no la entrega del sistema visual del módulo 2.
- Compilación frontend a wwwroot y publicación bajo un mismo servidor.
- PostgreSQL en Compose y alternativa nativa Windows con credenciales generadas,
  datos ignorados y user-secrets. Plantilla de entorno sin secretos reales.
- GitHub Actions en cada PR: dependencias, compilación, pruebas frontend/backend,
  revisión de migraciones, publicación y smoke test.

## Verificaciones locales realizadas

| Comprobación | Resultado |
|---|---|
| SDK instalado | .NET 10.0.402, conserva SDK 8 existente. |
| Dependencias frontend | `npm ci --prefix frontend` correcto; auditoría sin vulnerabilidades reportadas. |
| Frontend | 3 pruebas: contrato, fallo/reintento y respuesta inesperada. |
| Backend | `dotnet build RCC.sln --configuration Release`: 0 errores y 0 advertencias. |
| Integración PostgreSQL 17.6 | 6 pruebas aprobadas, 0 omitidas: estado, readiness/migraciones, caída DB/liveness, 404 API, persistencia y restricciones reales. |
| Migración | Aplicada a base local; sin cambios pendientes de modelo; reaplicación cubierta por prueba. |
| Compilación y publicación | Vite y `dotnet publish` correctos. |
| Smoke HTTP del publicado | `/`, `/api/status`, live, ready y ruta SPA devuelven 200; API inexistente 404; recurso JS 200. |
| Compose | Configuración válida; tras actualizar WSL, contenedor local PostgreSQL healthy y seis pruebas de integración aprobadas contra Docker. |

## Incidencias reales y límites

Se detectó mezcla de EF Core 10.0.4 (dependencia transitiva de Npgsql) y 10.0.13
(herramientas de diseño), con error CS1705 al compilar pruebas. Se fijó explícitamente
EF Core Relational 10.0.13 para propagar la misma versión al proyecto de integración;
la compilación y las seis pruebas posteriores pasaron.

Docker Desktop se instaló por solicitud del usuario. Inicialmente Virtual Machine
Platform estaba deshabilitado y DISM requería administrador (740); la primera
validación se realizó con PostgreSQL nativo real. Después el responsable habilitó
los permisos y reinició Windows. Se actualizó WSL de 2.6.3 a 2.7.13 con elevación,
cerrando Docker y las consultas WSL para evitar el bloqueo de archivos en uso.
Se verificó el motor Linux de Docker 29.8.2 y se levantó PostgreSQL 17.6 mediante
Compose en 127.0.0.1:5432 con volumen persistente y estado healthy.

Se aplicó InitialFoundation al contenedor, se repitieron las seis pruebas de
integración con resultado aprobado y se confirmó `/api/health/ready` Healthy.
La API local usa ahora esa conexión mediante user-secrets. `.env` contiene una
contraseña generada y está excluido de Git. La base nativa se conserva sin cambios.

La configuración sin perfil Development requiere variable de conexión explícita;
se verificó que faltar configuración impide iniciar con mensaje claro. No hay aún
autenticación, reservas, APIs externas, IA ni despliegue público. Las claves de
proveedores no son necesarias en esta entrega.

## Revisión y continuidad

PR del módulo 1 apilado sobre la rama del módulo 0 mientras PR #1 siga abierto,
para mostrar solo cambios de la base técnica. Tras integrar la especificación,
retargetear a main. No se fusiona automáticamente ni se registra auto-revisión de
Juulren como realizada. Consultar el PR para resultado remoto de CI.
