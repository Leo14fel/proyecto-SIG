# Pruebas

Ejecute `dotnet test VisorDatosSIG.sln`.

Las pruebas unitarias cubren verificación y generación PBKDF2-SHA256, reconocimiento de capas oficiales, diagnóstico de componentes ausentes y rechazo de capas arbitrarias. La comprobación manual de autenticación requiere SQL Server local y una cuenta oficial activa; no modifica la base.

Valores de inyección como `'`, `' OR 1=1 --`, `%` y `AGUILAR'--` se transmiten únicamente como parámetros de LINQ/EF Core; no se construye SQL con nombres ni valores del cliente. Falta una prueba de integración con SQL aislado que lo demuestre en ejecución.

Pendiente: pruebas de integración HTTP con una base SQL aislada para búsqueda, exportación, usuario inactivo, logout y autorización Administrador/Consultor. Las pruebas responsive, autenticadas y de rendimiento requieren navegador y base local; no se han registrado resultados para 360, 768 o 1366 px.

## Evidencia local

- La cuenta local `demo.consultor` se creó fuera del repositorio con PBKDF2-SHA256, 100.000 iteraciones, salt aleatorio y rol `Consultor` activo.
- Mediante HTTP autenticado, Consultor recibió 200 en Inicio, Visor, Mi Perfil y `/api/capas`. La solicitud directa a `/Administracion/Usuarios` fue rechazada por la autorización de rol.
- Datos migrados verificados: Manzanas 863/863; Lotes 15.281 SHP, 1 geometría vacía omitida, 15.280 SQL; Códigos Fijos 6.271/6.271; Vías 578/578. SRID incorrecto 0 y `STIsValid() = 0` en las cuatro capas. La geometría vacía no fue reparada ni inventada.

## Rendimiento

| Operación | Resultado |
| --- | --- |
| Dashboard | Pendiente de medición repetida en navegador |
| Visor inicial | Pendiente de medición repetida en navegador |
| AGUILAR | Pendiente de medición repetida en navegador |
| Lote | Pendiente de medición repetida en navegador |
| GeoJSON Manzanas | Pendiente de medición repetida en navegador |
| Lotes por bbox | Pendiente de medición repetida en navegador |
