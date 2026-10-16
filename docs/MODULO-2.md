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

Precios, capacidades y superficies de las tarjetas son ejemplos ficticios.
La misma imagen ilustrativa acompaña los conceptos, no un catálogo de fotografías
verificadas del hotel. Cuenta y exploración abren explicaciones, no simulaciones
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
- 9 pruebas Chromium con Playwright: matriz de 360/768/1440 px × ES/EN, ausencia
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
