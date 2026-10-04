# Pruebas

Ejecute `dotnet test VisorDatosSIG.sln`.

Las pruebas unitarias cubren verificación PBKDF2-SHA256 y reconocimiento/validación de componentes de las cuatro capas oficiales. La comprobación manual de autenticación requiere SQL Server local y una cuenta oficial activa; no modifica la base.

Pendiente: pruebas de integración HTTP con una base SQL aislada para usuario inactivo, logout y autorización de Administrador.
