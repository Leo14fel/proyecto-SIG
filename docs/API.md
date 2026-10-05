# API

Las rutas requieren autenticación: `GET /api/capas`, `GET /api/capas/{capa}/geojson`, `GET /api/capas/{capa}/{id}` y `GET /api/busqueda`.

`geojson` limita Lotes por `bbox` y devuelve una FeatureCollection. `busqueda` acepta `capa`, `q`, `pagina` y `tamanoPagina`; solo permite las capas y campos definidos en el servidor.
