# Activar los servicios pendientes de RCC

Módulos 6–8 implementados; configuración y comprobaciones externas aún pendientes del responsable. No compartas claves en chat, capturas, commits ni variables VITE. Los comandos se ejecutan desde la raíz del repositorio, con PowerShell 7 y dotnet disponible. Reinicia la API en Development después de cambiar user-secrets.

## Orden de configuración

| Servicio | Preparación y guía | Comprobación real |
|---|---|---|
| Open-Meteo | No requiere clave para este uso académico; ya consultado desde C#. | Destino muestra fuente/hora y datos actuales; no previsión de fechas de reserva. |
| Google Maps | Proyecto Cloud, Maps Embed API y clave restringida a API y dominios. [SERVICIOS-EXTERNOS.md](SERVICIOS-EXTERNOS.md). | Carga automática del mapa en Destino y comprobar hotel/dirección; probar enlace alternativo. |
| Stripe test | Cuenta/sandbox, claves de prueba y webhook local. [STRIPE-SANDBOX.md](STRIPE-SANDBOX.md). | Pago válido, rechazo, evento repetido, cancelación y reembolso en sandbox. |
| Resend | Cuenta, dominio verificado y remitente. [SERVICIOS-EXTERNOS.md](SERVICIOS-EXTERNOS.md). | Cancelación con destinatario propio: aceptación y recepción verificadas por separado. Confirmación después de Stripe. |
| OpenAI | Proyecto API, clave y modelo compatible; pasos siguientes. | mode=ai con opciones válidas y consumo visible; después desactivar para comprobar cálculo local. |

Puedes preparar las cuentas en otro orden. No se habilitan pagos/correos/IA por el hecho de crear la cuenta. No hace falta configurarlos para ejecutar las pruebas aisladas.

## OpenAI

1. Accede a [OpenAI Platform](https://platform.openai.com/), crea/selecciona un proyecto exclusivo para RCC y revisa su facturación, acceso a modelos y límites. Sigue el [inicio oficial](https://developers.openai.com/api/docs/quickstart). No se contratan créditos ni se crean claves automáticamente desde este trabajo.
2. Crea una clave privada de proyecto con permiso para Responses. Conserva su valor localmente.
3. Escoge el ID de un modelo al que tu proyecto tenga acceso y que admita **Responses API y Structured Outputs**. Revisa compatibilidad y coste en el catálogo oficial antes de habilitar. No se fija un modelo predeterminado ni se garantiza disponibilidad para tu cuenta.
4. Configura en user-secrets:

```powershell
$rccAiKey = Read-Host 'Clave privada del proyecto OpenAI' -MaskInput
$rccAiModel = Read-Host 'ID del modelo compatible seleccionado'
dotnet user-secrets set 'OpenAI:ApiKey' $rccAiKey --project src/Rcc.Api
dotnet user-secrets set 'OpenAI:Model' $rccAiModel --project src/Rcc.Api
Remove-Variable rccAiKey, rccAiModel
dotnet user-secrets set 'OpenAI:MaxOutputTokens' '1024' --project src/Rcc.Api
dotnet user-secrets set 'OpenAI:DailyRequestLimit' '20' --project src/Rcc.Api
dotnet user-secrets set 'OpenAI:Enabled' 'true' --project src/Rcc.Api
```

El cupo de RCC es de **intentos diarios UTC**, compartido por su base PostgreSQL. No equivale a dinero: coste depende del modelo y tokens. Configura además los límites de gasto y uso de tu proyecto según la [documentación oficial](https://developers.openai.com/api/docs/guides/spend-limits). Usa un proyecto/clave dedicados para no confundir consumo de otras aplicaciones.

5. Reinicia la API, inicia sesión y abre `/reservar`. Introduce fechas futuras y distribución, busca disponibilidad y pulsa **Pedir recomendaciones**. La etiqueta debe indicar **Ordenadas con IA** y la respuesta POST `/api/recommendations` debe tener mode=ai y reason=AI_RANKED. Comprueba que importes y capacidades corresponden al catálogo y a una nueva cotización.
6. Verifica el intento y consumo en el panel de tu proyecto OpenAI. Esta prueba externa no se declara aprobada por obtener solo mode=deterministic. No grabes Authorization ni claves en la demostración.
7. Desactiva y reinicia para validar la alternativa:

```powershell
dotnet user-secrets set 'OpenAI:Enabled' 'false' --project src/Rcc.Api
```

La aplicación debe seguir recomendando por cálculo local con aviso **No intervino IA**. Para reactivar usa true y reinicia.

Variables equivalentes para otros entornos: `OpenAI__Enabled`, `OpenAI__ApiKey`, `OpenAI__Model`, `OpenAI__MaxOutputTokens`, `OpenAI__DailyRequestLimit`. `OPENAI_API_KEY` por sí sola no está enlazada en RCC: usa la sección OpenAI de ASP.NET. No cambies el endpoint HTTP a servicios ajenos.

## Si aparece cálculo local con IA habilitada

- AI_NOT_CONFIGURED: revisa Enabled, clave, modelo y rangos válidos (256–2048 tokens; 1–500 intentos diarios).
- AI_DAILY_LIMIT: espera al siguiente día UTC o revisa conscientemente el cupo de demostración. Reiniciar no borra el contador; no lo reinicies para ocultar consumo.
- AI_UNAVAILABLE: revisa acceso al modelo, credenciales, conexión, límites/cuota y facturación en tu proyecto. No hay reintentos automáticos que multipliquen llamadas.
- AI_INVALID_RESPONSE: salida fuera del contrato; conserva la alternativa local y comprueba la compatibilidad del modelo.
- NO_MATCH: cambia fechas, distribución o presupuesto. No se gastó una llamada de IA.
- AVAILABILITY_CHANGED: inventario/precios cambiaron mientras se consultaba; vuelve a buscar.
- HTTP 429 de RCC: tres consultas por minuto por usuario. Espera y reintenta; no es necesariamente un error del proveedor.

`store=false` evita almacenar la respuesta como recurso persistente de Responses; no implica que toda retención del proveedor desaparezca. RCC no envía datos personales ni historial. Las explicaciones se muestran como texto y deben verificarse; la IA no puede confirmar reservas ni cambiar precios.

## Asistente local de configuración (22 de octubre de 2026)

En este equipo se preparó `.local/configurar-ia.ps1`, excluido de Git. Desde la raíz ejecuta `powershell -ExecutionPolicy Bypass -File .\.local\configurar-ia.ps1`; cuando lo solicite, copia la clave del proyecto desde OpenAI y vuelve a la terminal para pulsar Enter sin pegarla. Guarda y verifica las opciones sin imprimir la clave. Usa `gpt-5.4-mini`, 1024 tokens de salida y 20 intentos diarios; no realiza una llamada de pago. El modelo admite Responses y Structured Outputs según su [ficha oficial](https://developers.openai.com/api/docs/models/gpt-5.4-mini); el acceso depende de tu proyecto.

Detén la API con Ctrl+C y vuelve a ejecutar `.\.local\iniciar-rcc.ps1` para importar los ajustes en el proceso correcto junto con PostgreSQL. La configuración manual de arriba sigue siendo la alternativa reproducible en otro equipo. La API puede generar cargos; revisa facturación antes de probar. El límite diario de RCC cuenta intentos, no dinero.
