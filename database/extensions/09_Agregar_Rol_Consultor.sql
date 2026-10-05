USE VisorDatosSIG;
GO

/* Extensión del equipo: dato compatible; no modifica el diseño físico oficial. */
IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE NombreRol = 'Consultor')
BEGIN
    INSERT dbo.Roles (NombreRol, Descripcion, Estado)
    VALUES ('Consultor', 'Consulta de capas y resultados sin administración', 1);
END;
GO
