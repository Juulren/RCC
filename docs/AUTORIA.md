# Registro de autoría y participación

Responsable único: **Juulren**. Asistencia técnica: **Codex**. La persona responsable dirige el alcance y diseño, configura cuentas y servicios y realiza comprobaciones manuales; Codex asiste en código, documentación, investigación y pruebas automatizadas.

## Etiquetas de commits

`[IA]` identifica trabajo generado con asistencia de IA; `[HUMANO]`, aportación declarada humana; `[MIXTO]`, trabajo con intervención documentada de ambos. Por solicitud del responsable, los siete commits de módulos 6–8 y sus ajustes se reclasificaron MIXTO, conservando su contenido y actualizando sus ramas. Los commits posteriores de integración y documentación también usan la etiqueta que corresponda.

Distribución del historial de la rama al iniciar módulo 9 (antes de los commits nuevos de este módulo):

| Etiqueta | Commits | Proporción de commits |
|---|---|---|
| IA | 14 | 58.3% |
| HUMANO | 0 | 0.0% |
| MIXTO | 10 | 41.7% |
| SIN ETIQUETA | 0 | 0.0% |

Esta proporción cuenta **commits**, no líneas de código, horas, esfuerzo, propiedad intelectual ni porcentaje exacto de autoría del producto. Un commit grande y uno pequeño cuentan igual. No se inventan porcentajes de código humano/IA a partir de estas etiquetas; el responsable debe añadir su valoración final con evidencia si el docente exige otra métrica. Los commits futuros del módulo 9 no están incluidos en esta fotografía histórica.

## Evidencias humanas informadas

- Elección de hotel, estilo, idioma, módulos y navegación.
- Configuración local de Docker, Maps, Resend y Stripe sandbox.
- Confirmación de pago de prueba, correo recibido, cancelación, mapa e IA local funcionando.
- Decisión inicial de Ollama/túnel y posterior cambio autorizado a Gemini Free para no depender de la PC.
- Configuración del despliegue en Render/Neon y de los proveedores; confirmación del funcionamiento publicado y aceptación final el 24 de octubre de 2026.
- Solicitud de README público en main y de documentación reproducible.

Las pruebas automatizadas y revisiones técnicas de Codex no se presentan como revisión humana independiente. La modalidad individual difiere de la consigna de cuatro integrantes; no se simulan personas ni aprobaciones. Ver TRABAJO-INDIVIDUAL.md.

## Porcentaje del código vigente según historial etiquetado

Corte: 24 de octubre de 2026, rama codex/gemini-gratuito, commit `2a9c1a59f001fa6fbaf30d479e79ef4a3eea4d09`.

Se cuentan líneas no vacías de archivos .cs, .js, .jsx, .css y .html en src/, frontend/src/ y frontend/index.html. Se incluyen API, conectores compartidos, puente opcional y frontend; se excluyen migraciones EF, pruebas, documentación, imágenes, dependencias y archivos generados. Las líneas incluyen comentarios y delimitadores, no son sentencias lógicas. Total: **88 archivos y 11,535 líneas**.

| Etiqueta del último commit de cada línea | Líneas vigentes | Porcentaje |
|---|---:|---:|
| IA | 9,014 | 78.14 % |
| HUMANO | 0 | 0.00 % |
| MIXTO | 2,521 | 21.86 % |
| Total | 11,535 | 100.00 % |

Método: git ls-tree fija el universo del commit; git blame --line-porcelain atribuye cada línea no vacía al último commit que la modificó; se toma [IA], [HUMANO] o [MIXTO] del mensaje. Porcentaje = líneas de la categoría / 11,535 × 100. No hubo líneas sin etiqueta. Reproduce la procedencia declarada por etiquetas, no demuestra quién escribió originalmente cada carácter ni permite separar internamente las líneas MIXTO.

El 0 % HUMANO significa ausencia de líneas atribuidas a commits exclusivamente humanos dentro de este universo. No representa ausencia de trabajo humano: alcance, decisiones, configuración privada, cuentas, pruebas manuales y aceptación están registrados por separado. El historial de la rama al corte contiene 30 commits: 14 IA y 16 MIXTO; esos porcentajes de commits son distintos de los porcentajes de líneas y no los sustituyen. Main tiene una actualización documental separada, fuera de esta medición del código.
