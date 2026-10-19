# Módulo 2: sistema visual

Fecha: 2026-10-16. Rama: `feat/modulo-2-diseno`. Responsable único: Juulren.
Contenido generado por IA, pendiente de revisión visual y de código del responsable.

## Entrega

Portada nocturna de Chongqing con dirección de lujo futurista, acento cian,
tipografía editorial y fondo ambiental único. Tarjeta destacada con tres conceptos
seleccionables, sección de experiencias, destino, FAQ y pie con atribuciones.
React Bits SpotlightCard integrado; patrones Reacticx adaptados con React DOM/CSS.
Véase [THIRD_PARTY.md](../THIRD_PARTY.md) para fuentes, revisión y licencias.

Selector español/inglés con preferencia local y atributo HTML lang actualizado.
El cambio no borra fechas, huéspedes ni pestaña seleccionada. Navegación móvil
plegable con cierre al navegar y Escape. Buscador de vista previa, popover de
huéspedes, pestañas con flechas/Home/End y diálogos HTML nativos con Escape y
restitución de foco. Enlaces de salto y foco visible. No se añade autenticación,
catálogo persistente, disponibilidad ni reservas antes de sus módulos.

Precios, capacidades y superficies de las tarjetas del home son ejemplos ficticios.
La misma imagen ilustrativa acompaña sus conceptos. La página independiente de
Habitaciones utiliza los siete tipos oficiales y sus fotografías, como se documenta
en la revisión del 19 de octubre. Cuenta y exploración abren explicaciones, no simulaciones
de una reserva confirmada. Sin cookies de proveedores ni claves en el frontend.

## Componentes y movimiento

- Tokens y puntos de corte en `frontend/src/styles.css`.
- Diccionario ES/EN en `frontend/src/content.js`.
- SpotlightCard fijado a commit de React Bits, con licencia íntegra incluida.
- Ambiente CSS pausado fuera de vista y cuando la pestaña está oculta.
- Entrada breve del contenido, pestañas deslizantes, respuesta al foco/puntero.
- `prefers-reduced-motion` elimina animaciones/transiciones y scroll suave.
- Diseño legible sin imágenes y sin depender de WebGL.

## Validación

- 7 pruebas Vitest: conectividad, fallo/reintento, respuesta inesperada, traducción
  conservando estado, pestañas por teclado, vista previa sin petición de reserva
  y fechas inválidas.
- 19 pruebas Chromium con Playwright: matriz de 360/768/1440 px × ES/EN, ausencia
  de desbordamiento, imagen cargada, navegación, diálogos/Escape/foco, teclado,
  movimiento reducido, fallo de imágenes y persistencia de idioma.
- El caso de reflujo equivalente a zoom 200% usa 640 píxeles CSS para una ventana
  de 1280; no se confunde CSS zoom con el zoom real del navegador.
- Capturas de portada y tarjeta revisadas en el navegador local; capturas
  completas de los seis tamaños/idiomas disponibles en artefactos de Playwright.
- Compilación Vite y publicación a wwwroot verificadas. La API de base permanece
  sin cambios; se conservan sus seis pruebas de integración y smoke test en CI.
- No se declara una certificación completa de accesibilidad: estas comprobaciones
  cubren la base del sistema visual y se ampliarán al agregar flujos reales.

## Reproducir

```powershell
npm ci --prefix frontend
npm test --prefix frontend
npm exec --prefix frontend -- playwright install chromium
npm run test:browser --prefix frontend
node scripts/build-web.mjs
dotnet run --project src/Rcc.Api
```

Playwright inicia Vite automáticamente en 4173 y simula únicamente `/api/status`
para aislar la UI; esto no acredita integración con proveedores. La página real
en 5080 sí consume la API C#. CI instala Chromium y guarda reporte, capturas y
trazas de fallos. Módulos 3 a 9 siguen pendientes.

## Incidencias de revisión

Se evitó cerrar el diálogo desde la limpieza de efectos: React StrictMode puede
repetirlos y emitir un cierre involuntario. Las comprobaciones reales de diálogo
y restitución de foco pasan con StrictMode activo.

La primera comprobación de ampliación usaba CSS zoom, que no cambia media queries.
Se corrigió el método de prueba al reflujo equivalente con viewport reducido;
no se ocultó desbordamiento con overflow-x hidden para aprobar la comprobación.

La aprobación visual humana queda pendiente en el PR. El módulo 3 no se inicia
automáticamente. El PR parte del módulo 1 mientras su PR siga abierto.

## Revisión de movimiento: 2026-10-19

Tras la observación del responsable de que la página se sentía estática, se
reforzó el movimiento del módulo 2. Se añade Motion 14.3.1 para entradas de
secciones al hacer scroll, tarjetas escalonadas, transición de salida/entrada
entre habitaciones, parallax de la ciudad y barra de progreso.

El fondo pasa de un gradiente casi imperceptible a un ambiente compuesto de
auroras CSS, tres trazos de luz Canvas, partículas y una órbita. Se limita a
30 dibujos/segundo y densidad de píxeles máxima 1.5; usa 18 puntos en móvil y
36 en escritorio. No se mide ni promete una tasa de fotogramas universal.
Se pausa fuera de vista, con la pestaña oculta y con movimiento reducido.
El botón «Pausar fondo» permite detenerlo manualmente.

Botones con reflejo y desplazamiento, tarjetas con iluminación e inclinación
suave al pasar el puntero, zoom de imágenes y apertura animada de FAQ/diálogos.
Los efectos no se requieren para leer ni operar el sitio. La versión reducida
omite entradas, parallax y Canvas; el contenido conserva contraste y navegación.

La décima prueba de navegador comprueba que el Canvas cambia realmente entre
fotogramas, responde al control de pausa y deja de dibujar cuando sale del viewport.
La prueba de movimiento reducido comprueba que Canvas no es visible ni está activo.
Se conserva la matriz de tamaños/idiomas y los recorridos de teclado existentes.

## Páginas independientes: 2026-10-19

Por solicitud del responsable, el navbar deja de saltar a secciones y abre
`/habitaciones`, `/experiencias` y `/destino`. La portada `/` conserva las secciones,
animaciones, fotografías ilustrativas y tarifas de demostración. React Router
marca el enlace activo y permite historial,
recarga directa y retorno al inicio. ES/EN persiste entre páginas.

Habitaciones presenta siete tipos oficiales (cuatro habitaciones, tres suites),
con nombres, camas y superficies de la página de Regent Chongqing facilitada.
Incluye filtros por categoría/vista al río, búsqueda, estado vacío y galería modal
con navegación y restitución de foco. Se incorporan once fotos WebP del CDN
oficial; trazabilidad y derechos en THIRD_PARTY.md. No se inventan tarifas,
capacidades ni disponibilidad para esta colección informativa.

Experiencias tiene una composición editorial con tres momentos y sus detalles;
los horarios se identifican como sugerencias. Destino presenta Hongyadong,
Qiansimen y Jialing, referencia Jiangbeizui y un esquema expresamente conceptual
con enlace a Google Maps. El mapa integrado corresponde al módulo 7.

Validación: 7 pruebas Vitest, 18 recorridos Playwright (incluida matriz de las tres
páginas en 360/768/1440 px y ES/EN), build de producción y acceso directo a rutas
servidas por C#. Las pruebas cubren búsqueda, filtros, cambio de foto, foco,
recarga, historial, regreso al home y persistencia de idioma. Las fotos oficiales
se sirven desde el proyecto, sin llamadas externas durante las pruebas.

## Consistencia de navegación y transición: 2026-10-19

Se comparte NavigationLinks entre home y páginas interiores, con Inicio/Home
siempre presente, enlace activo y el mismo espaciado. CityHero unifica fotografía,
altura (590 px), recorte (50% 62%), degradado y posición de títulos de Experiencias
y Destino. Se elimina la discrepancia entre fondo CSS e imagen HTML.

Cada cambio de ruta aplica un fundido de 360 ms sin traslación ni escalado;
movimiento reducido lo desactiva. Las páginas se importan juntas para que el
cambio no intercale una pantalla de carga. El bundle de producción pesa unos
148 kB gzip de JavaScript, frente a unos 142 kB iniciales con carga diferida.

La matriz ES/EN a 360/768/1440 px compara geometría y recorte de ambas portadas,
además de comprobar Inicio activo en el home. Un recorrido adicional valida el
fundido y su alternativa con movimiento reducido: 19 pruebas Playwright en total.
