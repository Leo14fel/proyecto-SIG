# Búsqueda y filtros

`GET /api/busqueda` consulta SQL Server y requiere autenticación. Acepta `capa`, `q`, `pagina` y `tamanoPagina` (máximo 100). Las capas admitidas son `manzanas`, `lotes`, `codigos-fijos` y `vias`.

La consulta usa una lista cerrada de columnas reales y parámetros de EF Core; no acepta nombres de columna del cliente. Las comparaciones de texto son parciales y no distinguen mayúsculas de minúsculas.

Para personas se consulta exactamente `dbo.CodigosFijos.Nombre`; no existe una columna `Apellido` en el diseño oficial ni se crea una columna derivada. Un valor real observado en los SHP es `AGUILAR SOCORE AGUSTIN EUGENIO`; una búsqueda parcial por `AGUILAR` debe realizarse después de migrar la capa a SQL Server.

Los filtros avanzados con combinaciones de campo y operador siguen pendientes. La base local está vacía, por lo cual no hay un resultado SQL demostrable todavía.
