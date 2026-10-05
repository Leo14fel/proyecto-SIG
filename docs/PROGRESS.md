# Progreso de implementación

## Estado beta parcial

- Lectura reutilizable de Shapefiles en Infrastructure con validación de `.shp`, `.shx`, `.dbf` y `.prj`.
- Metadatos reales por capa: registros, geometría, extensión, campos DBF y validación WGS 84.
- Migración transaccional a SQL Server, con cancelación y omisión de geometría vacía.
- API SQL: `GET /api/capas`, `GET /api/capas/{capa}/geojson`, `GET /api/capas/{capa}/{id}`, `GET /api/busqueda` y `GET /api/busqueda/exportar`.
- El dashboard y visor consumen los endpoints SQL. La API de diagnóstico SHP se limita a Development.

La carga oficial se ejecutó mediante el migrador transaccional. El visor consulta SQL Server y muestra 863 Manzanas, 15.280 Lotes, 6.271 Códigos Fijos y 578 Vías.

## Restricción de rendimiento

Lotes no se carga al abrir el mapa. Se habilita desde zoom 16 y se limita a la extensión visible, con simplificación exclusiva para visualización y un máximo de 3.500 entidades por solicitud. Los archivos originales no se modifican.

## Pendiente

- Identificación múltiple de entidades superpuestas, pruebas de integración HTTP y auditoría compatible con un esquema oficial ampliado.

## Autenticación alfa

- Cookie Authentication con usuarios, roles y hashes PBKDF2-SHA256 de las tablas oficiales.
- Login, logout y protección de Dashboard/Visor implementados.
- Administración mínima de usuarios y Mi Perfil implementados con autorización de servidor y cambio PBKDF2.
- La interfaz del migrador informa capa, procesados/total, exitosos, omitidos, fallidos, porcentaje y tiempo transcurrido mediante `IProgress<T>`; cancelar conserva el rollback.
- Sin tabla oficial para bitácora de acceso; el registro persistente de login queda pendiente.
