# Instalación local

1. Ejecute los scripts oficiales de `database/official` contra SQL Server 2022, sin modificar su diseño.
2. Configure `ConnectionStrings:DefaultConnection` con User Secrets o una variable de entorno. Consulte `AUTHENTICATION.md`.
3. Ejecute `dotnet restore VisorDatosSIG.sln` y `dotnet run --project src/VisorDatosSIG.Web`.

La cadena local con autenticación Windows no contiene secretos, pero se mantiene fuera del repositorio para permitir cambiar servidor e instancia por entorno.
