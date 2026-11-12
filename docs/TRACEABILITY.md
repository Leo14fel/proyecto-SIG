# Trazabilidad

| Grupo | Estado | Evidencia |
| --- | --- | --- |
| RF-MIG-01..05, 07, 09, 12 | IMPLEMENTADO | `ShapefileSqlMigrationService`, carga SQL verificada |
| RF-MIG-06, 08, 10, 11, 13, 14 | PARCIAL | Progreso y cancelación implementados; Anexar sin regla natural y bitácora pendientes |
| RF-SEG-01, 04, 05 | IMPLEMENTADO | Cookie, autorización de servidor y sesión |
| RF-SEG-02, 03, 06 | PARCIAL | Consultor demo y administración mínima; falta cobertura de integración y bitácora |
| RF-VIS-01..08 | PARCIAL | Visor SQL, capas, búsqueda y estados básicos |
| RF-VIS-09..10 | PARCIAL | Selección/detalle y CSS responsive; falta validación manual por viewport |
| RF-CON-03, 04, 05, 06, 07, 09, 10 | IMPLEMENTADO | API SQL, filtros whitelist, paginación, visor y CSV servidor |
| RF-CON-01, 02, 08 | PARCIAL | Identificación simple; agrupación de intersecciones pendiente |

## Fase 2 - auditoría 2026-11-12

El estado `código` indica revisión estática y pruebas unitarias; no sustituye una aceptación contra SQL Server, HTTPS y un teléfono reales.

| Requisito | Estado auditado | Evidencia | Pendiente prioritario |
| --- | --- | --- | --- |
| RU-01 Autenticación y roles | Código | `CuentaController`, `SqlAuthenticationService`, autorización MVC | Prueba HTTP con usuarios SQL activos. |
| RU-02 Tablero por estados | Código | `HomeController`, `Home/Index` agrupan `CodigosFijos.Estado` | Comparar contra la base autorizada después de un corte y una reconexión. |
| RU-03 Visor, filtro y búsqueda | Código | `VisorController`, `CapasController`, `BusquedaController` | Prueba autenticada, responsive y de rendimiento. |
| RU-04 Capas, 2D/3D y dimensiones | Código | `Visor/Index`, `Visor/Perspectiva`, `perspective.js` | Validación visual con los datos SQL reales. |
| RU-05 Asignación y ruta vial | Código y prueba unitaria | `OperacionesController`, `StreetRouterTests` | Aplicar extensión autorizada y probar ruta/casos inalcanzables. |
| RU-06 Monitoreo y reproducción | Código | `Recorridos`, `operations.js`, PWA de campo | Recibir GPS desde un teléfono físico con la PWA visible. |
| RU-07 Tareas propias ordenadas | Código | `MyAssignments`, `Tareas` | Verificar aislamiento con dos trabajadores. |
| RU-08 GPS y navegación | Código | `Campo`, `operations.js`, Geolocation API | GPS, permisos y navegación externa en un teléfono por HTTPS. |
| RU-09 Resultado y evidencia | Código | `Resultado`, `WaterEvidence` | Probar fotos, concurrencia y errores de red contra SQL Server. |
| RU-10 Hora, GPS y proximidad | Código y prueba unitaria | UTC de servidor, límites de 100 m y 150 m; `StreetRouterTests` | Validación física de precisión/distancia y observación. |
| RU-11 Preparar corte y roles operativos | Código | `PrepararCorte`, `AdministracionController` | Aplicar extensión autorizada y reiniciar sesión de prueba. |
| RU-12 OCR | No implementado, opcional | — | Fuera de la prioridad obligatoria. |
| RU-13 Entrega PWA/base/enlace | No entregado | No hay respaldo autorizado ni enlace de despliegue | Preparar enlace HTTPS y respaldo autorizado. |

No se encontraron `referencias/`, `references/`, `AlcanceProyecto_2_2026.pptx` ni el documento Word del docente. Esta matriz deberá contrastarse con esos archivos cuando se incorporen al repositorio.
