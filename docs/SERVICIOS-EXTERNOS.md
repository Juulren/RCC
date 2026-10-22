# Configurar los servicios externos de RCC

El código del módulo 7 funciona sin claves: clima público de Open-Meteo, enlace externo a Google Maps y correos pendientes sin enviar. Las cuentas pertenecen al responsable del proyecto; no se crean cuentas ni se contratan servicios automáticamente.

## Google Maps Embed

1. Entra en [Google Cloud Console](https://console.cloud.google.com/), inicia sesión y crea un proyecto RCC.
2. Sigue la [guía oficial](https://developers.google.com/maps/documentation/embed/get-api-key): vincula facturación si la consola lo solicita, habilita **Maps Embed API** y crea una clave en **APIs & Services / Credentials**.
3. Restringe la clave por aplicación a **Websites / HTTP referrers**. Para desarrollo añade `http://localhost:5080/*` y, si usas esa dirección, `http://127.0.0.1:5080/*`. Para publicar, añade solo tu dominio HTTPS. Restringe también la API a **Maps Embed API**.
4. Desde la raíz del repositorio, con PowerShell 7 y dotnet disponible:

```powershell
$rccMapsKey = Read-Host 'Clave restringida de Maps Embed' -MaskInput
dotnet user-secrets set 'GoogleMaps:EmbedKey' $rccMapsKey --project src/Rcc.Api
Remove-Variable rccMapsKey
```

Reinicia la API en Development. En Destino, pulsa **Cargar mapa de Google**. Comprueba el hotel y su dirección, no solo la aparición del iframe. La clave de Maps es pública en el navegador por diseño; sus restricciones son necesarias. No uses una clave de servidor sin restricciones. El mapa no se descarga hasta pulsar el botón; el enlace externo permanece visible. El navegador no permite inspeccionar errores internos de Google por tratarse de otro origen: una clave inválida puede mostrar el error dentro del iframe aunque ocurra `load`.

## Resend

1. Crea una cuenta en [Resend](https://resend.com/).
2. En **Domains**, añade un dominio o subdominio que controles. No puede ser `gmail.com` ni otro dominio compartido. Copia los registros DNS que indique Resend al administrador DNS de tu dominio y espera a que aparezca **Verified**. [Guía de dominios](https://resend.com/docs/dashboard/domains/introduction).
3. En **API Keys**, crea una clave con permiso de envío y, cuando esté disponible, limitada al dominio verificado. Conserva su valor; no se vuelve a mostrar. [Guía de claves](https://resend.com/docs/dashboard/api-keys/introduction).
4. Usa como remitente una dirección del dominio verificado. No necesitas publicar claves aquí ni en GitHub.

```powershell
$rccResendKey = Read-Host 'Clave privada de Resend' -MaskInput
dotnet user-secrets set 'Resend:ApiKey' $rccResendKey --project src/Rcc.Api
Remove-Variable rccResendKey
dotnet user-secrets set 'Resend:From' 'RCC <reservas@TU-DOMINIO-VERIFICADO>' --project src/Rcc.Api
```

Antes de habilitar, consulta **Administración / Correos de reservas**. Al habilitar se procesará la cola pendiente; usa una base local controlada y destinatarios propios para validar. Nunca habilites el envío real sobre las bases de pruebas automáticas.

```powershell
dotnet user-secrets set 'Resend:Enabled' 'true' --project src/Rcc.Api
```

Reinicia la API. Con una cuenta propia, cancela una reserva pendiente y verifica: fila Pending → Accepted en RCC, correo en Resend y recepción en tu bandeja. Para confirmación, primero configura y valida Stripe sandbox con STRIPE-SANDBOX.md. Accepted significa que Resend aceptó la solicitud; no acredita entrega, lectura ni ausencia de rebote. No hay webhook de entrega en este módulo.

Si no tienes dominio, deja `Resend:Enabled=false`. No hace falta comprar uno para continuar con las pruebas aisladas. La alternativa de remitente de prueba que permita tu cuenta debe validarse contra las restricciones que muestre Resend; no habilita envío general a clientes.

Para detener envíos:

```powershell
dotnet user-secrets set 'Resend:Enabled' 'false' --project src/Rcc.Api
```

Reinicia. Los eventos nuevos siguen quedando registrados como pendientes. Variables equivalentes para otros entornos: `GoogleMaps__EmbedKey`, `Resend__ApiKey`, `Resend__From`, `Resend__Enabled`. No poner claves privadas en variables `VITE_*`.

## Clima

No necesita cuenta ni clave para este uso académico. `/api/weather` consulta condiciones actuales de referencia para Chongqing (coordenadas aproximadas 29.57, 106.57), no la previsión de una estancia ni una estación instalada en el hotel. Se muestra hora local UTC+8 y atribución. Caché 15 minutos por instancia; ante un fallo, dato anterior claramente marcado hasta dos horas, después no disponible. Los reintentos fallidos se espacian al menos un minuto. [Documentación y condiciones de Open-Meteo](https://open-meteo.com/en/docs).

## Resolver un correo en Review

Los errores 4xx permanentes requieren corregir configuración/destinatario y revisar el registro con el administrador técnico; no se repiten ciegamente. Los fallos transitorios usan backoff. Si el resultado sigue siendo desconocido 23 horas después del primer intento, se detiene: Resend conserva claves idempotentes durante [24 horas](https://resend.com/docs/dashboard/emails/idempotency-keys). Consulta el panel de Resend antes de cualquier intervención manual. Este módulo no incluye un botón que pueda reenviar un resultado ambiguo ni promete entrega exactamente una vez fuera de la ventana del proveedor. Conserva el ID de la notificación y la evidencia al resolverlo.
