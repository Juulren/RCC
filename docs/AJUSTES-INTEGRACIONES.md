# Ajustes de integraciones — 22 de octubre de 2026

Google Maps carga automáticamente al recibir una URL configurada en Destino. Conserva el enlace externo, el mensaje de carga y la alternativa ante errores. El componente mantiene su estado al actualizar el clima y cambiar idioma; cambia de instancia si cambia la URL.

Las recomendaciones informan expresamente cuando OpenAI está desactivado o sin configurar y conservan las opciones del cálculo local. Se solicitan desde «Pedir recomendaciones» después de buscar disponibilidad, con sesión iniciada. No confirman reservas ni participan automáticamente en el pago.

El responsable informó que funcionan pago Stripe de prueba, correo Resend, cancelación y Maps. Esto no acredita por sí solo la liquidación de un reembolso en el panel de Stripe. OpenAI continúa pendiente de configuración y prueba real; no se ha declarado una respuesta real con mode=ai.

Validación de este ajuste: Vitest, suite de servicios Playwright con proveedores simulados y compilación del frontend. El asistente privado de configuración se comprueba sintácticamente y queda fuera del repositorio; no se guardan secretos ni se consume OpenAI durante estas pruebas.
