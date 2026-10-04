# VisorDatosSIG

## Demo técnica v0.1

Esta entrega presenta las cuatro capas oficiales en un panel, un visor Leaflet y un diagnóstico de archivos geoespaciales.

**Modo diagnóstico: las capas se leen temporalmente desde los SHP. La persistencia definitiva SQL Server se encuentra en integración.**

Los archivos oficiales se mantienen sin cambios en `data/shapefiles/`. La Web y el migrador los ubican automáticamente desde la raíz de la solución. Si se ejecutan fuera de ella, configure la variable de entorno `VISORDATOSSIG_SHAPEFILES_PATH` con la carpeta que contiene los SHP.

Algunas geometrías poligonales oficiales no cumplen la orientación estricta de anillos ESRI. El lector las corrige solamente en memoria para poder diagnosticarlas y visualizarlas; los SHP no se modifican.

## Ejecución

```powershell
dotnet run --project src/VisorDatosSIG.Web
```

Abra `/Visor` para acceder al mapa. Las capas Manzanas, Códigos Fijos y Vías se cargan directamente; Lotes se consulta bajo demanda dentro de la extensión visible y desde nivel de zoom 16, con un máximo de 3.500 entidades por solicitud. Esta estrategia evita bloquear el navegador con la capa completa, que contiene más de 15.000 geometrías.

`VisorDatosSIG.Migrador` permite seleccionar una carpeta o un `.shp`, verificar sus componentes y previsualizar los primeros 20 registros. La escritura a SQL Server no es parte de esta demo.

## Dependencias geoespaciales

- `NetTopologySuite` 2.6.0
- `NetTopologySuite.IO.Esri.Shapefile` 1.2.0

No se agregó Entity Framework ni se modificó el diseño SQL oficial.

## Autenticación

La Web requiere autenticación mediante los usuarios y roles de SQL Server. Configure `ConnectionStrings:DefaultConnection` mediante User Secrets antes de iniciar. La guía completa se encuentra en `docs/AUTHENTICATION.md`.
