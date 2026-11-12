# API

Las rutas requieren autenticación: `GET /api/capas`, `GET /api/capas/{capa}/geojson`, `GET /api/capas/{capa}/{id}`, `GET /api/busqueda` y `GET /api/registros`.

`geojson` limita Lotes por `bbox` y devuelve una FeatureCollection. `busqueda` acepta `capa`, `q`, `pagina` y `tamanoPagina`; solo permite las capas y campos definidos en el servidor.

`GET /api/registros?capa={manzanas|lotes|codigos-fijos|vias|todas}&pagina=1&tamanoPagina=25` devuelve registros alfanuméricos paginados desde SQL Server, sin geometrías. Acepta `q`, y para una capa específica `campo` y `operador` (`contiene`, `igual`, `comienza-con`) únicamente dentro de la whitelist de esa capa. `GET /api/registros/{capa}/{id}` devuelve los atributos autorizados de un registro. La geometría individual se obtiene exclusivamente mediante `GET /api/capas/{capa}/{id}`.
