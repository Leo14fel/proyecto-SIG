# Migrador

El migrador valida SHP/SHX/DBF/PRJ, WGS84, SRID 4326 y geometrías antes de insertar por lotes en una transacción. El modo Reemplazar se ejecutó con los datos oficiales. La cancelación de la interfaz propaga un `CancellationToken`; la transacción se revierte al cancelar.

Un Lote vacío fue omitido: origen 15.281, válidos SQL 15.280. No se fabricó geometría.
