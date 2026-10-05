# VisorDatosSIG

## Beta técnica

Esta entrega presenta las cuatro capas oficiales desde SQL Server, un visor Leaflet autenticado, migración SHP a SQL y búsqueda segura.

El visor consume SQL Server. Los SHP permanecen como fuente de migración y diagnóstico en Development.

Los archivos oficiales se mantienen sin cambios en `data/shapefiles/`. La Web y el migrador los ubican automáticamente desde la raíz de la solución. Si se ejecutan fuera de ella, configure la variable de entorno `VISORDATOSSIG_SHAPEFILES_PATH` con la carpeta que contiene los SHP.

Algunas geometrías poligonales oficiales no cumplen la orientación estricta de anillos ESRI. El lector las corrige solamente en memoria para poder diagnosticarlas y visualizarlas; los SHP no se modifican.

## Ejecución

```powershell
dotnet run --project src/VisorDatosSIG.Web
```

Abra `/Visor` para acceder al mapa. Las capas Manzanas, Códigos Fijos y Vías se cargan directamente; Lotes se consulta bajo demanda dentro de la extensión visible y desde nivel de zoom 16, con un máximo de 3.500 entidades por solicitud. Esta estrategia evita bloquear el navegador con la capa completa, que contiene más de 15.000 geometrías.

`VisorDatosSIG.Migrador` valida e importa SHP a SQL Server en una transacción, con cancelación. Consulte `docs/MIGRATOR.md`.

## Dependencias geoespaciales

- `NetTopologySuite` 2.6.0
- `NetTopologySuite.IO.Esri.Shapefile` 1.2.0

EF Core se usa para consultas SQL parametrizadas. Los scripts originales se conservan en `database/official/`; extensiones compatibles se ubican en `database/extensions/`.

## Autenticación

La Web requiere autenticación mediante los usuarios y roles de SQL Server. Configure `ConnectionStrings:DefaultConnection` mediante User Secrets antes de iniciar. La guía completa se encuentra en `docs/AUTHENTICATION.md`.

Consulte `docs/API.md`, `docs/USER_MANUAL.md`, `docs/TESTING.md` y `docs/TRACEABILITY.md` para operación y límites conocidos.
