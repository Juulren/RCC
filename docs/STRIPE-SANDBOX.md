# Activar y validar Stripe sandbox en RCC

La implementación está preparada para modo de prueba. Al 2026-10-20 el
responsable aún no tiene sandbox; no hay una validación real de Stripe realizada.
No publiques claves en el repositorio ni las compartas por chat.

## Preparación local

1. Crear una cuenta y un sandbox desde el
   [Dashboard oficial](https://dashboard.stripe.com/test/dashboard). Comprobar que
   se trabaja en test/sandbox y que la cuenta admite tarjeta y CNY.
2. Configurar la clave secreta de ese sandbox en user-secrets del API. Estos
   comandos son plantillas; sustituir los valores localmente:

```powershell
dotnet user-secrets set "Stripe:SecretKey" "sk_test_REEMPLAZAR" --project src/Rcc.Api
dotnet user-secrets set "Stripe:PublicOrigin" "http://localhost:5080" --project src/Rcc.Api
```

3. Instalar [Stripe CLI](https://docs.stripe.com/stripe-cli) y ejecutar `stripe
   login` para autorizar la cuenta elegida. En otra terminal:

```powershell
stripe listen --events checkout.session.completed,checkout.session.async_payment_succeeded,checkout.session.async_payment_failed,checkout.session.expired --forward-to http://localhost:5080/api/payments/stripe/webhook
```

4. Tomar el secreto `whsec_...` que muestra **ese listener**, no uno de otro
   endpoint/cuenta. Configurarlo localmente:

```powershell
dotnet user-secrets set "Stripe:WebhookSecret" "whsec_REEMPLAZAR" --project src/Rcc.Api
```

5. Reiniciar el API después de configurar. En Development lee user-secrets; en
   despliegue usar variables Stripe__SecretKey, Stripe__WebhookSecret y
   Stripe__PublicOrigin. Para despliegue registrar un endpoint HTTPS real y usar
   el secreto de ese endpoint, no el temporal del CLI. El origen es únicamente
   esquema/host/puerto, sin ruta; debe coincidir con la web.

6. Verificar `/api/payments/status`: available=true indica configuración presente,
   **no prueba por sí solo conectividad, permisos o moneda admitida por Stripe**.
   Una clave live se rechaza y deja los pagos desactivados.

## Comprobación controlada pendiente

Usar una cuenta cliente y datos ficticios. Reservar para una fecha futura, abrir
Mis reservas y pulsar “Pagar en Stripe · prueba”. Debe redirigir al dominio de
Checkout de Stripe; ingresar únicamente sus
[tarjetas de prueba oficiales](https://docs.stripe.com/testing), nunca tarjetas
reales. RCC no ofrece campos de número/CVC.

- Aprobado: verificar importe CNY en Checkout, completar y comprobar Confirmed
  después de procesar el webhook. El retorno aislado no debe confirmar.
- Rechazado: permanece PendingPayment; volver a intentar sin extender expiresAt
  ni crear otra sesión/PI canónicos.
- Duplicado: reenviar el evento original desde Dashboard/CLI; no duplica estados
  ni trabajos. `stripe trigger` con una sesión ajena no confirma una reserva RCC.
- Cancelación: cancelar desde RCC antes del día de entrada; inventario libre,
  RefundJob pendiente y posteriormente Succeeded tanto en RCC como en Dashboard.
  Una respuesta inicial pending no debe mostrarse como reembolso completado.
- Vencimiento: comprobar liberación a los 30 minutos y expiración remota por el
  worker. Stripe puede tardar más en cerrar la sesión; un pago tardío debe quedar
  Expired/Cancelled y reembolsado, sin recuperar inventario.
- Webhook interrumpido: detener el listener después de crear Checkout, completar
  el pago y comprobar conciliación autenticada. Si ya venció el bloqueo, verificar
  reembolso, no confirmación retroactiva.
- Registrar evidencia saneada en MODULO-6.md: fecha, tipos de escenario y estados
  finales, sin claves, payloads completos, datos de tarjeta ni cookies.

Conservar el API/worker activo durante los escenarios. No reembolsar manualmente
desde Dashboard durante el flujo normal: esta versión concilia trabajos iniciados
por RCC y no todas las modificaciones externas. Un estado Review/Failed requiere
revisar la operación externa y la fila local antes de cualquier nuevo intento;
no borrar filas, claves idempotentes ni recrear reembolsos a ciegas.

El harness de CI solo simula el proveedor y no sustituye esta comprobación. La
demostración académica del módulo debe distinguir ambos tipos de evidencia.
