# Instalación local

1. Ejecute los scripts oficiales de `database/official` contra SQL Server 2022, sin modificar su diseño.
2. Para habilitar cortes, reconexiones y monitoreo, solicite autorización y ejecute una vez `database/extensions/10_OperacionesAgua.sql` en esa misma base. El script es idempotente: crea las tablas e indices operativos que falten, sin modificar datos de conexiones ni shapefiles. La aplicación no lo ejecuta automáticamente.
3. Configure `ConnectionStrings:DefaultConnection` con User Secrets o una variable de entorno. Consulte `AUTHENTICATION.md`.
4. Instale el SDK indicado por `global.json`, ejecute `dotnet restore VisorDatosSIG.sln` y `dotnet run --project src/VisorDatosSIG.Web`. El repositorio incluye `NuGet.Config` y restaura únicamente desde `nuget.org`; no requiere una instalación local de DevExpress.

La solución compilable corresponde a la Web/PWA y no requiere MAUI ni un SDK Android. El proyecto histórico `src/VisorDatosSIG.Mobile` se conserva fuera de `VisorDatosSIG.sln`; no se compila ni es parte de la entrega actual.

La cadena local con autenticación Windows no contiene secretos, pero se mantiene fuera del repositorio para permitir cambiar servidor e instancia por entorno.
