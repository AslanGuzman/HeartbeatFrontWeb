# AGENTS.md

## Alcance del repositorio

Este repositorio contiene el portal web de Heartbeat para Control de Misión, desarrollado con Blazor. El trabajo de este repo corresponde a `@IsaiasMorillo` y a los tickets del portal asignados a esa persona en `AliaJimenez/HeartbeatBackend`.

Implementa aquí las vistas, componentes, navegación, presentación y consumo de API del portal. No desarrolles la aplicación de escritorio WPF en este repositorio.

## Antes de empezar

1. Revisa la estructura y las instrucciones existentes del repositorio antes de modificar archivos.
2. Consulta los issues asignados a `@IsaiasMorillo` en [HeartbeatBackend](https://github.com/AliaJimenez/HeartbeatBackend), sus criterios de aceptación, dependencias y estado actual. Los tickets del proyecto viven en ese repositorio aunque el código del portal viva aquí.
3. Lee los contratos vigentes en `external/HeartbeatBackend/contracts/CONTRACTS.md` y los documentos relacionados que afecten al ticket. Si el submódulo no está presente, inicialízalo según las instrucciones del repositorio o consulta el repo fuente.
4. Comprueba que las dependencias del ticket estén resueltas o marcadas como listas antes de comenzar el trabajo que depende de ellas. No asumas que la lista de tickets aquí está actualizada: verifica GitHub.

## Responsabilidad del portal

Los issues de portal asignados a `@IsaiasMorillo` incluyen:

- `#36 PT-SHELL`: estructura Blazor, cliente de Mission Control API y navegación por permisos.
- `#37 PT-LOGIN`: inicio y cierre de sesión.
- `#38 PT-CREW`: vista general de la tripulación.
- `#39 PT-DETAIL`: detalle del astronauta, estado actual e historial.
- `#40 PT-ALERTS`: alertas críticas y reconocimiento.
- `#41 PT-MSG`: mensajes por astronauta.
- `#42 PT-USERS`: administración de usuarios.
- `#43 PT-ROLES`: roles y permisos.
- `#44 PT-CHARTS`: gráficas por indicador.
- `#57 WR-PORTAL`: mostrar y filtrar lecturas según su origen.

La lista es orientativa; confirma en GitHub qué sigue abierto, asignado y desbloqueado. Sigue las dependencias entre tickets y prioriza los issues listos según el flujo del equipo.

## Contratos e integración

- Trata los contratos compartidos como fuente de verdad para rutas, modelos, permisos y formatos de datos. No inventes endpoints, campos ni comportamientos.
- El portal consume la Mission Control API `/api/mc/v1` según los contratos vigentes. Usa clientes tipados y patrones de autenticación ya establecidos en el proyecto.
- Si una API necesaria todavía no existe, evita simular que la integración está terminada. Aísla la dependencia detrás de una interfaz o estado pendiente y documenta qué falta.
- No modifiques archivos del submódulo `external/HeartbeatBackend` desde este repositorio. Los cambios a contratos o al backend deben proponerse mediante un PR separado en `AliaJimenez/HeartbeatBackend`.
- Respeta el control de acceso: oculta las opciones de navegación que el usuario no puede usar y protege las acciones y páginas según los permisos definidos por el backend. La visibilidad de la UI no sustituye la autorización del servidor.

## Forma de trabajar

- Haz cambios pequeños y enfocados en el ticket actual; conserva las convenciones, arquitectura y estilo existentes.
- Reutiliza componentes y estilos compartidos para los estados normal, `watch` y `critical` (verde, ámbar y rojo) cuando corresponda.
- Incluye estados de carga, error, vacío y falta de permiso donde sean relevantes.
- Para historiales y lecturas, respeta los filtros, rangos y reglas definidos por los contratos y criterios de aceptación.
- No agregues dependencias nuevas sin comprobar primero si el proyecto ya tiene una solución adecuada.
- No marques un ticket como terminado si quedan criterios de aceptación sin cumplir o integraciones representadas solo con datos ficticios.

## Verificación

- Ejecuta la compilación y las comprobaciones existentes que correspondan a los cambios antes de proponerlos.
- Si no puedes ejecutar una comprobación, indícalo junto con el motivo y las áreas que no se verificaron.
- Revisa el diff para evitar cambios accidentales, secretos, archivos generados o ediciones al submódulo.

## Pull requests y tickets

- Abre los PR del portal en este repositorio.
- En la descripción del PR incluye la referencia completa para cerrar el issue del backend, por ejemplo: `Closes AliaJimenez/HeartbeatBackend#36`.
- Para referenciar sin cerrar un issue, usa `Refs AliaJimenez/HeartbeatBackend#<n>`.
- Resume los criterios cubiertos, las dependencias pendientes y las comprobaciones realizadas.

Consulta [el flujo de tickets](https://github.com/AliaJimenez/HeartbeatBackend/blob/main/docs/FLUJO-DE-TICKETS.md) para las reglas vigentes del equipo.
