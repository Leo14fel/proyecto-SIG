# Registros geográficos

La sección `/Registros` consulta SQL Server con paginación de servidor: nunca carga las 15.280 geometrías de Lotes para construir una tabla.

- Manzanas: `UV_MZA`, `UV` y `MZA`.
- Lotes: `NroLote` y su PK `IdLote`. `IdManzana` se presenta solo como atributo existente; no se infiere ninguna relación adicional.
- Códigos Fijos: `CodF_SIG`, `CodFijo` y `Nombre`.
- Vías: `Nombre`, `TipoVia` y `OSMID`.

Cada acción **Ver en visor** usa la PK SQL y navega a `/Visor?capa={capa}&id={id}`. El visor pide la entidad concreta a `/api/capas/{capa}/{id}`, crea una capa temporal de selección y aplica `fitBounds` a líneas y polígonos, o `flyTo` a puntos. Esto permite abrir un Lote aunque la capa general de Lotes todavía no esté activa por su límite de zoom.

Los parámetros de vuelta contienen únicamente el estado de consulta (capa, búsqueda, filtro y página); no contienen información privada.
