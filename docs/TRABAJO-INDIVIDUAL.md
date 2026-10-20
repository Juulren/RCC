# Modalidad individual y dirección de IA

## Responsabilidad

Por indicación expresa del usuario, Juulren es la única persona responsable de
arquitectura, backend, frontend, calidad, seguridad, documentación y despliegue.
Son áreas de trabajo de una persona, no cuatro integrantes ni agentes separados.
Codex asiste en generación/revisión técnica; no sustituye una revisión humana.

El PDF pide cuatro integrantes y revisión del trabajo de otro antes de integrar.
La modalidad individual no cumple literalmente esos puntos. Se registra la
diferencia para acordar con el docente la adaptación; no se inventan integrantes,
bitácoras ajenas, aprobaciones ni revisión entre pares. El desarrollo documental
puede avanzar. Ningún PR se fusiona automáticamente en nombre de un revisor.

## Sesiones por módulo

1. Juulren fija tarea pequeña, entradas, reglas y aceptación.
2. Codex genera documentación/código y pruebas aplicables dentro del módulo.
3. Ejecutar verificaciones; registrar comando, resultado y fallos completos saneados.
4. Juulren revisa diff, ejecuta demostración y registra correcciones/decisiones.
5. Commit etiquetado, push y PR con evidencia. Registrar revisión propia como
   auto-revisión, nunca como aprobación independiente. Cerrar módulo tras revisión.

Herramienta de generación: Codex en este proyecto. La función de IA de la app
usará OpenAI desde C# en módulo 8; es distinta de la herramienta que genera código.
Sesiones separadas por tarea/área dentro del módulo, con contexto de especificación
y pruebas. No delegar a subagentes ni crear chats nuevos sin solicitud explícita.

## Convención de autoría

- `[IA]`: contenido generado por IA, aunque una persona lo solicite o revise.
- `[HUMANO]`: escrito/corregido directamente por Juulren.
- `[MIXTO]`: combina cambios materiales de ambos; explicar la mezcla en bitácora.

Al cierre estimar porcentaje por volumen de código aportado y corregido, contrastar
con historial y declarar método y límites. No usar porcentaje de commits como si
fuera porcentaje de líneas ni contar documentación inicial como aplicación terminada.
Los módulos 0 a 2 contienen aportes generados por IA: no se estima aún autoría del código final.

## Bitácora única (objetivo: dos páginas al entregar)

Completar con hechos conforme ocurran; conservar prompts literalmente y eliminar
secretos de salidas. Esta plantilla no acredita requisitos ya completados.

| Prompt clave (5 al cierre) | Texto literal | Resultado | Corrección humana | Evidencia (commit/PR/prueba) |
|---|---|---|---|---|
| 1 | «perfecto, comienza con el modulo 1» | Base C#/React/PostgreSQL, pruebas y CI | No registrada | Rama feat/modulo-1-base |
| 2 | «si ya se termino el modulo 1, podemos seguir con el modulo 2» | Sistema visual ES/EN con React Bits y adaptaciones Reacticx | No registrada | Rama feat/modulo-2-diseno |
| 3 | «la página es bastante estática y sin trancisiones, overall se siente sin vida, necesito componentes más dinámicos, transiciones y fondos animados» | Revisión de movimiento: Motion, auroras/Canvas, parallax y transiciones | El responsable detectó movimiento insuficiente y solicitó reforzarlo | Revisión del módulo 2, 2026-10-19 |
| 4 | Por registrar | | | |
| 5 | Por registrar | | | |

| Error grave observado (2 al cierre) | Qué se esperaba | Cómo se detectó | Solución y verificación |
|---|---|---|---|
| Por registrar, sin inventar incidentes | | | |
| Por registrar, sin inventar incidentes | | | |

Decisión donde el responsable eligió otra opción a la propuesta de IA: por registrar
con propuesta, elección real, motivo y consecuencia. Aprendizajes sobre cuándo
confiar en IA: por registrar a partir de evidencia.

### Registro inicial: 2026-10-16

- Instrucción literal del responsable: «en cuanto al reparto del equipo es
  unicamente una persona encargada de todo, que soy yo».
- Consecuencia: se reemplaza reparto entre cuatro personas por responsabilidad
  individual en todas las áreas, dejando visible la discrepancia con el PDF.
- Estado: especificación generada por IA, pendiente de revisión humana mediante PR.
- No hay errores graves de código documentados: todavía no existe código de app.

### Módulo 1: 2026-10-16

- Instrucción adicional literal: «instala el docker si lo necesitas».
- Instalación: SDK .NET 10 y Docker Desktop. Motor Docker pendiente de habilitar
  Virtual Machine Platform con permisos de administrador y reiniciar manualmente.
- Validación con PostgreSQL nativo: seis pruebas de integración y tres de frontend
  aprobadas, compilación sin advertencias, migración y publicación comprobadas.
- Corrección técnica de la IA durante la sesión: alinear versiones de EF Core
  tras CS1705 en pruebas. Esto es un error de compilación resuelto, no uno de los
  dos errores graves detectados por el humano exigidos por la consigna.
- Evidencia detallada: MODULO-1.md. Revisión y correcciones humanas no registradas
  aún; no se inventan decisiones o aprendizajes del responsable.

### Verificación de Docker después del reinicio: 2026-10-16

- Instrucción literal: «listo, ya se le dio permisos y se reinicio windows».
- Actualización de WSL a 2.7.13 con autorización de administrador; motor Docker
  operativo y PostgreSQL 17.6 healthy mediante Compose.
- Migración aplicada al contenedor; seis pruebas de integración aprobadas de nuevo
  y API local Healthy. Configuración y contraseña locales excluidas del repositorio.
- Se actualiza el mismo PR del módulo 1; no se inicia el módulo 2.

### Módulo 2: 2026-10-16

- Se implementan portada, tarjeta de habitación y secciones de la página con tokens,
  traducciones y componentes accesibles. Se conservan créditos y licencias.
- Pruebas: 7 Vitest y 9 Chromium; no equivalen a funcionalidades de reserva real.
- Correcciones técnicas de IA: limpieza de diálogo compatible con StrictMode y
  método correcto de prueba de reflujo. No se registran como errores graves
  detectados por el humano, ni como aprobación visual humana.
- Evidencias y límites en MODULO-2.md; pendiente revisión del responsable mediante PR.

### Revisión visual del responsable: 2026-10-19

El responsable indicó que el diseño se sentía estático. Se añadieron transiciones
visibles, entradas al desplazarse, ambiente Canvas/CSS, efectos de tarjetas y
control de pausa. Se agrega prueba de cambio real de fotogramas y pausa fuera de
vista. Esta corrección visual no se registra como vulnerabilidad ni aprobación
humana del código. Se actualiza el PR #3; no se inicia otro módulo.

### Navegación por páginas: 2026-10-19

- Solicitud: conservar el home y diseñar páginas propias para el navbar, usando
  imágenes de la página oficial IHG indicada para Habitaciones.
- Se añaden tres rutas, siete tipos oficiales y once fotografías con atribución.
- Experiencias y Destino mantienen el alcance de propuesta visual del módulo 2.
- Se amplía Playwright de 10 a 18 pruebas; no se acredita revisión humana ni
  funcionamiento de reservas por estos recorridos del diseño.

### Consistencia visual y transición: 2026-10-19

El responsable señaló la ausencia de Inicio en el home, el salto de la imagen
entre Destino y Experiencias y la falta de transición de página. Se comparten
NavigationLinks y CityHero y se aplica un fundido sin movimiento geométrico.
Se añaden comprobaciones de alineación por tamaño/idioma y movimiento reducido.

### Fondo común con Inicio: 2026-10-19

El responsable precisó que Inicio tampoco coincidía con las dos páginas de
ciudad. Se comparte CityBackdrop entre las tres, se unifican recorte, alto,
degradado y ambiente animado y se elimina el parallax de la foto de Inicio.
La matriz visual usa ahora el fondo de Inicio como referencia de comparación.

### Módulo 3: 2026-10-19

- Instrucción literal: «ok, empezemos con el modulo 3».
- Se implementan Identity/PostgreSQL, registro, login/logout/me, CSRF y roles.
  Cuenta bilingüe y comprobación de permisos de administración en C#.
- Migración sin usuarios predefinidos. Alta de administrador por configuración
  local y comando explícito; no se inventa revisión o contraseña del responsable.
- Validación: 18 pruebas PostgreSQL, 7 Vitest, 19 Playwright de diseño y 3 recorridos
  de autenticación real. No se consideran autenticación probada los mocks visuales.
- Se corrigió el acceso de cuenta en tablet y se probaron cookies de producción,
  concurrencia de emails, invalidación tras logout y CSRF por identidad.
- Evidencias y limitaciones en MODULO-3.md. Módulo 4 pendiente.

### Módulo 4: 2026-10-19

- Solicitud literal: «empieza con la implementacion del modulo 4» tras revisar
  catálogo, datos, panel y pruebas pendientes.
- Se implementan hotel/servicios, tipos, galerías y habitaciones físicas con EF,
  PostgreSQL, API pública y administración con rol/CSRF y revisiones.
- Se mantienen las siete categorías/fotos; tarifas, capacidades e inventario
  explícitamente ficticios. No se afirma disponibilidad por fechas.
- Validación: 34 PostgreSQL, 7 Vitest, 21 recorridos visuales, 3 auth reales y
  2 de catálogo reales. El panel se prueba con base/admin efímeros; no se inventa
  cuenta ni revisión del responsable.
- Correcciones técnicas: alta explícita de hijos EF en reemplazos, etiquetas
  accesibles de select y contentRoot absoluto para el servidor de pruebas.
- Evidencia, reglas y límites en MODULO-4.md. Módulo 5 pendiente.

### Módulo 5: 2026-10-19

- Solicitud literal: «empieza con la implementacion del modulo 5».
- Implementación asistida de disponibilidad, cotización decimal en C#, asignación
  atómica de varias habitaciones, idempotencia y reservas propias con cancelación.
- Reglas de Chongqing, edades y capacidades; bloqueo de 30 minutos y vencimiento
  periódico. Protección administrativa de inventario comprometido y precios históricos.
- Interfaz ES/EN con distribución por habitación, tarjetas, aceptación de tarifa,
  continuidad del borrador, lista propia y consulta administrativa.
- Pruebas con PostgreSQL real y navegador; casos de último cupo simultáneo,
  respuesta perdida y nueva aceptación de precio. Evidencia en MODULO-5.md.
- Resultado local: 53 PostgreSQL, 7 Vitest, 21 visuales, 3 autenticación,
  2 catálogo y 4 reservas (90 comprobaciones); pruebas reales con bases aisladas.
- Pagos/confirmaciones/reembolsos quedan en módulo 6; no se simulan como completados.
  El responsable sigue siendo único; no se inventa revisión humana.
