# RCC

Proyecto académico de reservas de un hotel en Chongqing.

Repositorio: https://github.com/Juulren/RCC

## Estado

Módulo 0: especificación. Todavía no hay aplicación ejecutable, migraciones,
pruebas de aplicación ni despliegue. No se requieren claves para leer esta entrega.

## Documentación

- [Especificación funcional, datos y API](docs/ESPECIFICACION.md)
- [Diseño y bocetos](docs/DISENO.md)
- [Módulos y aceptación](docs/MODULOS.md)
- [Trabajo individual y bitácora](docs/TRABAJO-INDIVIDUAL.md)

## Arquitectura prevista

React + Vite (JavaScript y CSS) consume una API REST C# con ASP.NET Core 10,
Identity y Entity Framework Core sobre PostgreSQL. ASP.NET servirá la compilación
de React bajo el mismo dominio. Integraciones desde C#: Stripe de prueba,
Open-Meteo, Resend y OpenAI; Google Maps se carga en el navegador con clave
pública restringida por dominio.

## Instalación y pruebas

Se documentarán comandos ejecutados y verificados al implementar el módulo 1;
esta entrega no afirma que existan comandos de arranque o pruebas funcionales.
Requisitos previstos: SDK .NET 10 LTS, Node.js LTS compatible con Vite,
PostgreSQL y Docker para pruebas de integración. El equipo actual tiene .NET 8;
la instalación del SDK 10 corresponde al módulo 1.

## Flujo de trabajo

Repositorio oficial: https://github.com/Juulren/RCC.git. Una rama y un PR por
módulo; commits con `[IA]`, `[HUMANO]` o `[MIXTO]`. Responsable único: Juulren.
El autor técnico de los commits generados en esta sesión es Codex; la etiqueta
declara generación por IA y no acredita revisión humana.

## Contexto académico

Demostración inspirada en Regent Chongqing; no es el sitio oficial ni conecta
con sus reservas reales. Habitaciones, tarifas e inventario serán ficticios;
pagos exclusivamente de prueba. Fuente de requisitos: `Tarea-1.pdf`, facilitado
por el usuario. La modalidad individual difiere del equipo de cuatro personas
pedido por la consigna; véase la documentación de trabajo individual.
