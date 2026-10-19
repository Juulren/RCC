# Módulo 4: catálogo e inventario

Fecha: 2026-10-19. Responsable único: Juulren. Rama: `codex/modulo-4-catalogo`.
Generado por IA; pendiente de revisión humana. PR apilado sobre módulo 3.

## Entrega verificable

- `/habitaciones` carga tipos, fotos, capacidades y tarifas desde la API C# y
  PostgreSQL. Filtros/búsqueda bilingüe y paginación se ejecutan en el servidor.
  Presenta carga, error/reintento, catálogo vacío y precios CNY de demostración.
  No reemplaza un fallo de conexión con datos estáticos.
- Información bilingüe del hotel, dirección, coordenadas aproximadas, zona horaria
  y servicios visibles en la misma página, procedentes de `/api/hotel`.
- `/admin` contiene secciones Hotel, Tipos de habitación y Habitaciones físicas.
  Permite crear, editar, desactivar/reactivar, modificar tarifas/capacidades,
  gestionar servicios, elegir fotos acreditadas, ordenar galería y editar alt ES/EN.
- Inventario con número único por hotel, tipo y estado operativo; consulta y filtros.
  El tipo queda fijo una vez creada la unidad. No hay borrado de unidades ni tipos.
- Los formularios preservan el borrador tras error o conflicto. Una revisión vieja
  no sobrescribe cambios: se ofrece descartar el borrador y recargar explícitamente.
- El panel comprueba sesión/rol en C# y retira los controles si un guardado recibe
  401/403. El cierre de sesión del módulo 3 sigue invalidando cookies copiadas.

El home conserva sus tres conceptos ilustrativos y buscador de vista previa por
la instrucción previa de conservar su contenido. El catálogo administrable está
en Habitaciones; disponibilidad por fechas, distribución real y reservas son módulo 5.

## Datos y migración

`AddCatalog` amplía Hotels con revisión y crea HotelServices, RoomTypes,
RoomImages y Rooms. Conserva Hotels e Identity existentes. Datos iniciales:
7 tipos, 11 fotos y 14 habitaciones `DEMO-*`, con IDs estables. La migración es
repetible y no reinserta datos ni sobrescribe ediciones al arrancar nuevamente.
No hay migración automática en el arranque normal de la API.

Hotel fijo del proyecto: `40000000-0000-0000-0000-000000000001`. No es una app
multihotel. Los demás hoteles que puedan existir no se publican ni administran
por estos endpoints. FKs con RESTRICT y FK compuesta de habitación/tipo/hotel.

Tipos tienen nombre/descripcion ES/EN, categoría, camas, superficie, vista al río,
capacidad total, máximos adultos/niños, tarifa decimal CNY, activo, orden y revisión.
Imágenes con orden único por tipo, alt ES/EN, fuente y copyright. Se utiliza una
biblioteca de 11 fotografías locales acreditadas; no hay subida ni descarga de
URLs arbitrarias. Nombres/camas/superficies iniciales provienen de IHG, pero el
catálogo puede ser editado. Tarifas, capacidad e inventario son ficticios.
La dirección tiene fuente oficial; las coordenadas iniciales son aproximadas.

## Contratos

| Método/ruta bajo `/api` | Acceso | Función |
|---|---|---|
| GET `/hotel` | Público | Hotel, servicios y revisión |
| GET `/room-types` | Público | Activos; page/pageSize/search/category/riverView |
| GET `/room-types/{id}` | Público | Detalle activo; inactivo o desconocido → 404 |
| GET `/admin/hotel` | Admin | Datos para edición |
| PUT `/admin/hotel` | Admin + CSRF | Sustituir datos y servicios con revisión |
| GET `/admin/image-assets` | Admin | Fotos locales con procedencia/copyright |
| GET `/admin/room-types` | Admin | También inactivos; page/pageSize/search/category/active |
| POST `/admin/room-types` | Admin + CSRF | Crear tipo y galería, 201 |
| PUT `/admin/room-types/{id}` | Admin + CSRF | Editar/publicar/desactivar con revisión |
| GET `/admin/rooms` | Admin | page/pageSize/search/active/roomTypeId |
| POST `/admin/rooms` | Admin + CSRF | Crear unidad física, 201 |
| PUT `/admin/rooms/{id}` | Admin + CSRF | Número/estado con revisión; tipo inmutable |

Listados: `{ items, page, pageSize, total, totalPages }`; 20 por defecto, máximo
100. Escrituras son JSON y usan `X-CSRF-TOKEN` fresco del módulo 3. Respuestas de
catálogo con no-store. El endpoint de estado informa módulo 4/etapa catalog.

Errores ProblemDetails: 400 VALIDATION_FAILED/INVALID_FILTER/INVALID_ROOM_TYPE,
401 sin sesión, 403 sin rol, 404 desconocido/inactivo público, 409 CATALOG_CHANGED,
DUPLICATE_CATALOG_KEY, REFERENCE_CHANGED o ROOM_TYPE_IMMUTABLE. Errores de validación
incluyen claves de campo y mensajes traducidos por frontend. SQL interno no se
expone. Se controla versión tanto en API como mediante token de concurrencia EF.

## Reglas y límites

- Dinero decimal en C# y numeric(12,2) en PostgreSQL. Tarifa >0, máximo
  9,999,999.99, exactamente hasta dos decimales; moneda fija CNY.
- Capacidad 1–12, adultos máximos 1–capacidad, niños 0–capacidad-1. Niños 0–17 años;
  al menos un adulto por habitación. Los máximos son independientes: su suma no
  habilita superar la capacidad total al reservar (validación de módulo 5).
- Superficie 1–1000 m², orden 0–9999, categoría room/suite y camas king/choice.
- Nombre ES/EN hasta 180, descripciones hasta 4000, identificador lowercase con
  guiones hasta 80. Número físico de 1–12 letras/dígitos/guiones, normalizado uppercase.
- Galería de 1–12 imágenes distintas (biblioteca actual de 11), alt hasta 250 por
  idioma. Orden según array; procedencia/copyright se asignan en C#.
- Hasta 20 servicios bilingües, nombres hasta 100. Zona horaria Asia/Shanghai fija.
  Coordenadas dentro de rangos geográficos y hasta seis decimales.
- Reemplazar imágenes y servicios es transaccional; si hay conflicto, se revierte
  también la modificación parcial. No se eliminan los archivos fotográficos.
- La biblioteca de selección de tipos del panel carga páginas de 100, hasta
  10,000 tipos. Es un límite del selector de esta aplicación académica.
- Las peticiones de catálogo tienen timeout de 15 segundos; errores preservan el
  formulario. El cierre del diálogo se desactiva solo mientras guarda.

El inventario operativo no es disponibilidad ni una promesa de habitaciones libres.
Las restricciones basadas en reservas vigentes se añadirán con sus entidades y
pruebas en módulo 5. No se simula un bloqueo inexistente. No hay tarifas
estacionales, uploader, reservas, pagos ni consulta de reservas en este módulo.

## Validación

67 pruebas locales aprobadas:

- **34 .NET/PostgreSQL:** 18 anteriores y 16 del catálogo. Migración repetible,
  semilla, roles/CSRF de escrituras, paginación, filtros, metadatos, validaciones,
  tarifa/precisión, alta/edición/desactivación/reactivación, números únicos, FKs,
  precio restringido en DB, concurrencia y ausencia de imágenes parciales.
- **7 Vitest:** regresión del frontend.
- **21 Playwright de diseño:** matriz ES/EN a 360/768/1440 px, movimiento y galerías,
  filtros, búsqueda, fallo/reintento y catálogo vacío. Catálogo simulado solo aquí;
  sus fixtures viven en e2e/fixtures y no se incorporan al frontend de producción.
- **3 Playwright de autenticación real:** registro/login/sesión/logout y permisos.
- **2 Playwright de catálogo real:** panel desktop ES y móvil EN; crear tipo con
  traducciones/tarifa/capacidad, ordenar fotos, comprobar publicación, conflicto
  de versión sin perder borrador, crear/desactivar unidad, persistir tras recarga,
  editar hotel/servicios, despublicar sin borrar inventario y rechazar guardado
  tras cerrar sesión fuera del formulario.

Se revisó la UI pública contra PostgreSQL y capturas del panel real generadas por
Playwright. Build Release/Vite y has-pending-model-changes sin cambios pendientes.
CI ejecuta los mismos grupos sobre PostgreSQL y la API publicada, con credencial
admin aleatoria y temporal para la base efímera; nunca usa credenciales del responsable.

## Reproducir

Preparar dependencias, PostgreSQL y conexión según README. Antes de recompilar en
Windows, detener la instancia de la API que esté usando bin/Release.

```powershell
dotnet build RCC.sln --configuration Release
dotnet ef database update --project src/Rcc.Api --configuration Release --no-build
node scripts/build-web.mjs
npm test --prefix frontend
npm run test:browser --prefix frontend
# Con RCC_TEST_POSTGRES en un servidor dedicado a pruebas:
dotnet test RCC.sln --configuration Release --no-build
# Panel con base y administrador efímeros; requiere .env, PostgreSQL local y CREATEDB:
./scripts/test-catalog.ps1
```

El script utiliza 5082 (configurable con -Port), crea `rcc_e2e_<uuid>`, aplica
migraciones, genera admin aleatorio, levanta una API temporal oculta y limpia solo
esa base al finalizar. Valida prefijo y cadena de conexión antes de limpiarla y
rechaza un puerto ocupado. Logs de servidor en .local; reportes en
frontend/playwright-report-catalog y test-results-catalog. No crea un admin en la
base del hotel local. Para el administrador del responsable, usar MODULO-3.md.

Para repetir `npm run test:catalog --prefix frontend` sin script, configurar
RCC_CATALOG_BASE_URL, RCC_E2E_ADMIN_EMAIL y RCC_E2E_ADMIN_PASSWORD hacia una API/base
**de pruebas** con catálogo migrado y administrador inicializado. La suite modifica
hotel e inventario y no debe apuntar a una base de uso real.

## Correcciones durante validación

Las pruebas detectaron que EF interpretaba imágenes/servicios con GUID nuevo,
añadidos a una navegación de una entidad existente, como registros ya existentes.
Se añadió AddRange explícito para insertar esos hijos dentro de la transacción;
se repitieron pruebas de conflicto y reemplazo completo. El navegador detectó
además etiquetas de select que incluían el texto de sus opciones: se separó
label/control y se vinculó por htmlFor/id, manteniendo mensajes con aria-describedby.
El script Windows pasó a contentRoot absoluto al iniciar la API temporal.

No se registran estas correcciones como errores graves detectados por el humano
ni como una revisión humana independiente. No se inicia automáticamente módulo 5.
