# Análisis de la relación espacial Lote/Código Fijo

## Problema observado

`dbo.sp_ActualizarLoteCodigosFijos` excedió 120 segundos en la base migrada y no completó. No se reejecutó. No se alteraron los scripts oficiales.

## Lectura de la definición

El procedimiento aplica `CROSS APPLY TOP (1)` para cada Lote sin `IdManzana` y `OUTER APPLY TOP (1)` para cada Código Fijo. Ambos candidatos se filtran con `Filter` y después con `STIntersects`; el segundo además ordena por `STArea()`.

## Hipótesis

- No hay creación de índices espaciales para `Manzanas.Geom` ni `Lotes.Geom` en el script.
- La evaluación de `STPointOnSurface()` se repite para cada candidato de Manzana.
- `TOP (1) ... ORDER BY l.Geom.STArea()` puede requerir ordenar múltiples Lotes candidatos por cada punto.
- La selectividad real de `Filter` y el plan de ejecución no han sido capturados, por lo que no se afirma una causa única.

## Próximo diagnóstico seguro

Capturar el plan de ejecución real en una copia de base, comprobar índices espaciales y tradicionales, contar filas pendientes y probar primero con un subconjunto. Cualquier índice u optimización debe ir en un script nuevo de `database/extensions/`, tras medir antes y después.

## Estado

PENDIENTE. No se propone una modificación automática ni se fabrican asociaciones.
