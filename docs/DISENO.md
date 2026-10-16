# Diseño: lujo futurista de Chongqing

## Dirección y componentes

Base #070B14, superficies #111B2B, texto #F2F4F8, texto secundario #B6C2D2,
cian #55E6ED y rojo #F26373 como acentos. Validar contraste por combinación:
el color por sí solo no comunica error, selección ni disponibilidad.
Tipografía inicial: pila sans serif del sistema; títulos fluidos con clamp.
Espaciado base 8 px, contenido máximo 1280 px y controles de al menos 44 px.

Fotografía nocturna del skyline y reflejos del río; interiores en el catálogo.
Usar imágenes con permiso/licencia y registrar procedencia. No copiar recursos
del hotel suponiendo que son libres. Evitar presentar la demo como sitio oficial.

React Bits: seleccionar en módulo 2 un fondo ambiental y efectos de entrada/
iluminación compatibles con React DOM, JavaScript y CSS. Reacticx utiliza React
Native, Reanimated y Skia: adaptar patrones Border Beam, pestañas fluidas y
acordeón a React DOM/CSS, sin importar dependencias nativas. El código reutilizado
debe conservar licencia/atribución; referencias conceptuales se documentan como
inspiración. Elegir componentes gratuitos y registrar versión/URL en módulo 2.

Un fondo ambiental como máximo, detenido fuera de vista. Sin cursor personalizado,
scroll forzado ni animación imprescindible. prefers-reduced-motion elimina
movimiento y deja contenido visible. Las pantallas de pago usan fondos sólidos.

## Boceto de inicio: escritorio

```text
┌──────────────────────────────────────────────────────────────┐
│ RCC / DEMO      Habitaciones  Experiencia  Ubicación  ES|EN  │
│                                             Entrar / Cuenta │
├──────────────────────────────────────────────────────────────┤
│ SKYLINE NOCTURNO + CAPA OSCURA                                │
│ CHONGQING, DESDE OTRA PERSPECTIVA                              │
│ Hotel de lujo · inventario de demostración                    │
│ [Entrada] [Salida] [2 habitaciones · 4 adultos · 1 niño]       │
│                                     [Buscar disponibilidad] │
├──────────────────────────────────────────────────────────────┤
│ Habitaciones      [Imagen]        [Imagen]        [Imagen]    │
│                   Nombre         Nombre          Nombre      │
│                   Capacidad      Capacidad       Capacidad   │
│                   Desde CNY      Desde CNY       Desde CNY    │
│                   [Ver detalle]  [Ver detalle]   [Ver detalle]│
├──────────────────────────────────────────────────────────────┤
│ Servicios / ciudad     Mapa + clima     Preguntas frecuentes  │
│ Pie: demo académica · fuentes · contacto de demostración      │
└──────────────────────────────────────────────────────────────┘
```

## Boceto de inicio: móvil (360 px)

```text
┌──────────────────────────┐
│ RCC / DEMO  ES|EN  Menú  │
├──────────────────────────┤
│ Skyline estático/móvil   │
│ Chongqing, desde otra    │
│ perspectiva             │
│ [Entrada]               │
│ [Salida]                │
│ [Habitaciones/huéspedes] │
│ [Buscar disponibilidad] │
├──────────────────────────┤
│ Imagen de habitación    │
│ Nombre y capacidad      │
│ Desde CNY / noche       │
│ [Ver detalle]           │
├──────────────────────────┤
│ Servicios               │
│ Ubicación / clima       │
│ FAQ desplegable         │
│ Pie de demostración     │
└──────────────────────────┘
```

## Flujo y estados

Buscar → resultados por habitación del grupo → selección → resumen de cotización
→ login si hace falta → reserva pendiente → Stripe → estado verificado en servidor
→ detalle de reserva. Preservar selección al iniciar sesión; revalidarla después.
Resumen lateral en escritorio y plegable en móvil. Nunca tapar botones con paneles
fijos. Selector permite añadir/quitar habitaciones y edades individuales.

Cada vista contempla carga, vacío, error/reintento y éxito. Disponibilidad vacía
ofrece cambiar fechas/ocupación. Precio cambiado requiere confirmar cotización.
Pago en verificación muestra estado pendiente, nunca éxito por la URL de retorno.
Recomendaciones muestran qué combinación se propone y botón para seleccionar.

ES predeterminado; selector cambia etiquetas, errores, fechas y moneda formateada
sin perder selección. No traduce códigos de API ni cambia moneda CNY.
Administración comparte tokens, con tablas legibles y filtros; en móvil usa
tarjetas/resumen para evitar depender de tablas anchas.

## Puerta de revisión del módulo 2

Revisar portada y tarjeta antes de extender el diseño. Capturas a 360, 768 y
1440 px, ambos idiomas, teclado, foco visible, zoom 200 %, movimiento reducido,
fallback sin fondo gráfico y contenido sin desbordamiento. Las maquetas de este
documento son bocetos; no equivalen a una UI implementada ni aprobada visualmente.
