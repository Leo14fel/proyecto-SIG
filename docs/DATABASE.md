# Base de datos

## Inspección real

La base local `VisorDatosSIG` contiene las tablas oficiales `Manzanas`, `Lotes`, `CodigosFijos`, `Vias`, `Usuarios`, `Roles`, `UsuariosRoles`, `MenuOpciones` y `UsuarioMenu`.

Las relaciones espaciales oficiales son `Lotes.IdManzana -> Manzanas.IdManzana` y `CodigosFijos.IdLote -> Lotes.IdLote`. Existen índices espaciales para las cuatro columnas `Geom`.

No existen tablas oficiales de historial de migraciones ni bitácora de accesos. No se agregaron tablas o columnas fuera del diseño físico entregado.

## Origen de scripts

`database/official/` contiene exclusivamente los scripts entregados por el docente. `database/extensions/09_Agregar_Rol_Consultor.sql` fue agregado por el equipo porque la especificación exige ese perfil y `dbo.Roles` permite incorporarlo como dato. No altera tablas, columnas, claves ni relaciones.

## Mapeo SHP autorizado

| Capa | Campos SHP usados | Tabla SQL |
| --- | --- | --- |
| Manzanas | `Id`, `UV_MZA`, `UV`, `MZA` | `Manzanas.IdOrigen`, `UV_MZA`, `UV`, `MZA` |
| Lotes | `Id`, `NroLote` | `Lotes.IdOrigen`, `NroLote` |
| Códigos Fijos | `CodF_SQL`, `CodF_SIG`, `CodFijo`, `Nombre`, `Longi`, `Latid` | columnas equivalentes de `CodigosFijos` |
| Vías | `OBJECTID`, `Nombre`, `type`, `OSMID` | `Vias.OBJECTID`, `Nombre`, `TipoVia`, `OSMID` |

## Resultado de migración local

La carga transaccional en modo reemplazar se ejecutó el 05/10/2026. Todas las geometrías SQL tienen SRID 4326 y `STIsValid() = 1`.

| Capa | SHP | SQL | Omitidos | Nulos | SRID incorrecto | Inválidos |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Manzanas | 863 | 863 | 0 | 0 | 0 | 0 |
| Lotes | 15.281 | 15.280 | 1 vacío | 0 | 0 | 0 |
| Códigos Fijos | 6.271 | 6.271 | 0 | 0 | 0 | 0 |
| Vías | 578 | 578 | 0 | 0 | 0 | 0 |

No se detectaron FK huérfanas. La carga usa una transacción: ante excepción o cancelación se ejecuta rollback. La asociación espacial opcional `IdManzana`/`IdLote` se mantiene pendiente de ejecutar el procedimiento oficial `sp_ActualizarLoteCodigosFijos`.
