export const reservationErrors = {
  es: {
    NETWORK: 'No pudimos conectar. Puedes reintentar; conservamos tu selección.',
    VALIDATION_FAILED:
      'Revisa las fechas y los huéspedes. Elige de 1 a 8 habitaciones, de 1 a 30 noches y llegada dentro de los próximos 365 días.',
    ROOM_UNAVAILABLE:
      'Ya no hay suficientes habitaciones para esta combinación. Busca disponibilidad de nuevo.',
    CAPACITY_EXCEEDED:
      'La distribución supera la capacidad del espacio elegido. Revisa los huéspedes.',
    PRICE_CHANGED: 'La tarifa cambió. Revisa la nueva cotización y acéptala antes de reservar.',
    IDEMPOTENCY_CONFLICT:
      'Este intento tiene datos distintos. Vuelve a cotizar para iniciar una nueva reserva.',
    RESERVATION_EXPIRED:
      'El bloqueo de 30 minutos venció. Busca disponibilidad para una nueva reserva.',
    CANCELLATION_CLOSED: 'Solo puedes cancelar antes del día de entrada en Chongqing.',
    PAYMENTS_NOT_CONFIGURED: 'Los pagos de prueba no están disponibles en este momento.',
    PAYMENT_PROVIDER_UNAVAILABLE:
      'No pudimos consultar Stripe. Reintenta el pago o actualiza el estado antes del vencimiento.',
    CHECKOUT_WINDOW_TOO_SHORT:
      'Queda menos de un minuto para pagar. El bloqueo no se amplía; espera a su vencimiento o cancela y vuelve a reservar.',
    PAYMENT_AMOUNT_LIMIT:
      'El importe supera el límite de pago de prueba. Divide la estancia en reservas menores.',
    PAYMENT_REQUIRES_REVIEW:
      'El pago requiere revisión. Consulta el estado antes de intentar otra reserva.',
    RESERVATION_NOT_PAYABLE: 'Esta reserva ya no admite iniciar un pago. Actualiza su estado.',
    SESSION_EXPIRED: 'Inicia sesión de nuevo para continuar.',
    INVENTORY_COMMITTED:
      'El inventario tiene reservas vigentes. No se puede desactivar ni renumerar.',
  },
  en: {
    NETWORK: 'Could not connect. You can retry; your selection is preserved.',
    VALIDATION_FAILED:
      'Check dates and guests. Choose 1–8 rooms, 1–30 nights, arriving within the next 365 days.',
    ROOM_UNAVAILABLE:
      'There are no longer enough rooms for this combination. Search availability again.',
    CAPACITY_EXCEEDED: 'The selected room cannot accommodate this guest distribution.',
    PRICE_CHANGED: 'The rate changed. Review and accept the updated quote before booking.',
    IDEMPOTENCY_CONFLICT:
      'This attempt has different details. Request a new quote to start another reservation.',
    RESERVATION_EXPIRED:
      'The 30-minute hold expired. Search availability to make a new reservation.',
    CANCELLATION_CLOSED: 'Cancellation is only available before the arrival date in Chongqing.',
    PAYMENTS_NOT_CONFIGURED: 'Test payments are currently unavailable.',
    PAYMENT_PROVIDER_UNAVAILABLE:
      'Could not contact Stripe. Retry payment or refresh the status before expiry.',
    CHECKOUT_WINDOW_TOO_SHORT:
      'Less than one minute remains to pay. The hold cannot be extended; wait until expiry or cancel and book again.',
    PAYMENT_AMOUNT_LIMIT:
      'The amount exceeds the test payment limit. Split the stay into smaller reservations.',
    PAYMENT_REQUIRES_REVIEW:
      'Payment needs review. Check the status before making another reservation.',
    RESERVATION_NOT_PAYABLE:
      'Payment can no longer be started for this reservation. Refresh its status.',
    SESSION_EXPIRED: 'Please sign in again to continue.',
    INVENTORY_COMMITTED:
      'This inventory has active reservations. It cannot be disabled or renumbered.',
  },
}
export const statusText = (value, es) =>
  ({
    PendingPayment: es ? 'Pendiente de pago' : 'Pending payment',
    Cancelled: es ? 'Cancelada' : 'Cancelled',
    Expired: es ? 'Vencida' : 'Expired',
    Confirmed: es ? 'Confirmada' : 'Confirmed',
  })[value] ?? value
export const bookingError = (error, language) =>
  error?.status === 401 || error?.status === 403
    ? reservationErrors[language].SESSION_EXPIRED
    : (reservationErrors[language][error?.code] ?? reservationErrors[language].NETWORK)
