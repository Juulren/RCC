# Fuentes y atribuciones

## Motion: transiciones y desplazamiento

- Paquete `motion` 14.3.1, importación `motion/react`, licencia MIT.
- Documentación: https://motion.dev/docs/react-scroll-animations y
  https://motion.dev/docs/react-animate-presence.
- Aviso de licencia distribuido en `frontend/public/licenses/motion.txt`.
- Se utiliza para entradas al hacer scroll, parallax, progreso de lectura y
  transiciones entre habitaciones. El ambiente Canvas/CSS es código propio de RCC.

## React Bits: SpotlightCard

- Autor: David Haz. Componente JavaScript/CSS incorporado y adaptado para RCC.
- Fuente fijada: https://github.com/DavidHDev/react-bits/tree/1eeb6f105c68b964289d85dabbe84a1d551f3797/src/content/Components/SpotlightCard
- Licencia de esa revisión: MIT + Commons Clause License Condition v1.0.
- Texto íntegro: `frontend/licenses/react-bits.txt`; se distribuye también en
  `frontend/public/licenses/react-bits.txt` (ruta web `/licenses/react-bits.txt`).
- Cambios: tamaño, colores y bordes mediante CSS de RCC; el seguimiento del puntero
  se desactiva con movimiento reducido y el efecto se oculta en dispositivos táctiles.
- Uso integrado en la aplicación, sin vender ni distribuir una biblioteca de componentes.

## Reacticx: inspiración adaptada a web

https://www.reacticx.com/ presenta componentes React Native con Reanimated/Skia.
Los patrones Border Beam, Gooey Search Tabs y Bouncy Accordion inspiran el borde
luminoso del buscador, indicador deslizante de pestañas y acordeones de RCC.
Se implementan desde cero con React DOM/CSS y elementos HTML nativos; no se copia
código de Reacticx ni se instalan dependencias de React Native. No se afirma
compatibilidad directa de sus componentes nativos con Vite.

## Fotografías

### Chongqing nocturno

- Archivo local: `frontend/public/images/chongqing-night.webp`.
- Obra: Chongqing Skyline At Night.png, Maple Doctor.
- Fuente: https://commons.wikimedia.org/wiki/File:Chongqing_Skyline_At_Night.png
- Licencia: https://creativecommons.org/licenses/by-sa/4.0/
- Cambios: resolución reducida (1280 px), conversión a WebP; recorte y capas
  oscuras mediante CSS en la página. La versión WebP se ofrece bajo CC BY-SA 4.0.
- El crédito y enlace de licencia están disponibles en «Créditos visuales» del pie.

### Interior ilustrativo

- Archivo local: `frontend/public/images/room-preview.webp`.
- Autor: Markus Spiske; Hotel bedroom windows (Unsplash).
- Fuente: https://commons.wikimedia.org/wiki/File:Hotel_bedroom_windows_(Unsplash).jpg
- Licencia: CC0 1.0, https://creativecommons.org/publicdomain/zero/1.0/
- Cambios: resolución reducida y conversión a WebP; recorte mediante CSS.
- No es una fotografía del Regent Chongqing. Se identifica como ilustrativa.

### Habitaciones oficiales Regent / IHG

- Página solicitada por el responsable: https://www.ihg.com/regent/hotels/us/en/chongqing/ckgrh/hoteldetail/rooms-and-suites
- Consulta: 2026-10-19. Nombres, superficies y camas contrastados con esa página.
- Once fotografías alojadas localmente en `frontend/public/images/regent/`.
  Se solicitó al CDN oficial una versión WebP de 1400 px; recorte visual con CSS.
- Copyright de sus respectivos titulares Regent / IHG. Estas imágenes no están
  cubiertas por las licencias CC de las otras fotografías ni por la del código.
  Se incorporan al prototipo académico por solicitud del responsable, con enlace
  visible a la fuente; no se afirma licencia libre ni afiliación con el hotel.
- Descripciones redactadas para RCC; no se reproducen los textos promocionales.

Correspondencia de archivos y recursos originales (prefijos debajo de la tabla):

| Archivo `.webp` | CDN | Recurso |
|---|---|---|
| classic | scene7 | roomssuites-chongqing-deluxeroom |
| classic-bathroom | scene7 | roomssuites-chongqing-deluxeroom-2 |
| classic-river | scene7 | roomssuites-chongqing-deluxeriverview |
| club | scene7 | roomssuites-chongqing-clubroom |
| club-river | digital | chongqing-regent-club-river-view |
| junior | scene7 | roomssuites-chongqing-juniorsuite-2 |
| junior-living | digital | Junior_Suite |
| executive | digital | Executive-Suite-bedroom |
| executive-living | digital | Chongqing_AccomondationRegent-Executive-Suite-Living-Room-1920x942 |
| regent | scene7 | roomssuites-chongqing-presidentialsuite |
| regent-living | scene7 | roomssuites-chongqing-presidentialsuite-2 |

Prefijos: `https://ihg.scene7.com/is/image/ihg/` y
`https://digital.ihg.com/is/image/ihg/`; parámetros de descarga:
`?wid=1400&fmt=webp&qlt=85`. Los nombres locales son identificadores técnicos,
no etiquetas de la escena fotografiada. La galería identifica habitación y número.

Las imágenes se sirven localmente para evitar dependencia de servidores de fotos
durante la navegación. El home conserva su interior ilustrativo. El nombre RCC
y la marca tipográfica son propios del proyecto académico; no se copian logotipos.
