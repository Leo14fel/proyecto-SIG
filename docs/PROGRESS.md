# Progreso de implementación

## Demo técnica v0.1

- Lectura reutilizable de Shapefiles en Infrastructure con validación de `.shp`, `.shx`, `.dbf` y `.prj`.
- Metadatos reales por capa: registros, geometría, extensión, campos DBF y validación WGS 84.
- Diagnóstico visual en el migrador, sin escritura a SQL Server.
- API temporal: `GET /api/diagnostico/capas` y `GET /api/diagnostico/capas/{capa}/geojson`.
- Panel con conteos reales y visor Leaflet para Manzanas, Lotes, Códigos Fijos y Vías.

**Modo diagnóstico: las capas se leen temporalmente desde los SHP. La persistencia definitiva SQL Server se encuentra en integración.**

## Restricción de rendimiento

Lotes no se carga al abrir el mapa. Se habilita desde zoom 16 y se limita a la extensión visible, con simplificación exclusiva para visualización y un máximo de 3.500 entidades por solicitud. Los archivos originales no se modifican.

## Pendiente

- Persistencia y consultas definitivas en SQL Server.
- Autenticación, administración, búsquedas y filtros avanzados.

## Autenticación alfa

- Cookie Authentication con usuarios, roles y hashes PBKDF2-SHA256 de las tablas oficiales.
- Login, logout y protección de Dashboard/Visor implementados.
- Sin tabla oficial para bitácora de acceso; el registro persistente de login queda pendiente.
