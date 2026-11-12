-- Ejecutar en la base VisorDatosSIG existente con una cuenta autorizada.
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.WaterAssignments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WaterAssignments (
        Id int IDENTITY PRIMARY KEY, WorkerId int NOT NULL REFERENCES dbo.Usuarios(IdUsuario),
        Reconnect bit NOT NULL, CreatedUtc datetime2 NOT NULL, StartLat float NOT NULL,
        StartLng float NOT NULL, RouteJson nvarchar(max) NOT NULL
    );
    CREATE INDEX IX_WaterAssignments_Worker ON dbo.WaterAssignments(WorkerId, CreatedUtc);
    CREATE TABLE dbo.WaterTasks (
        Id int IDENTITY PRIMARY KEY, AssignmentId int NOT NULL REFERENCES dbo.WaterAssignments(Id),
        CodeId int NOT NULL REFERENCES dbo.CodigosFijos(IdCodigo), Sequence int NOT NULL,
        Latitude float NOT NULL, Longitude float NOT NULL, CompletedUtc datetime2 NULL,
        Observed bit NULL, Meter nvarchar(50) NULL, Reading nvarchar(32) NULL,
        Observation nvarchar(500) NULL, ReportLat float NULL,
        ReportLng float NULL, Accuracy float NULL, Version rowversion NOT NULL,
        UNIQUE (AssignmentId, Sequence)
    );
    CREATE UNIQUE INDEX UX_WaterTasks_Pending ON dbo.WaterTasks(CodeId) WHERE CompletedUtc IS NULL;
    CREATE TABLE dbo.WaterEvidence (
        Id int IDENTITY PRIMARY KEY, WaterTaskId int NOT NULL REFERENCES dbo.WaterTasks(Id),
        Photo varbinary(max) NOT NULL
    );
    CREATE INDEX IX_WaterEvidence_Task ON dbo.WaterEvidence(WaterTaskId);
    CREATE TABLE dbo.WorkerPositions (
        Id int IDENTITY PRIMARY KEY, AssignmentId int NOT NULL REFERENCES dbo.WaterAssignments(Id),
        RecordedUtc datetime2 NOT NULL, Latitude float NOT NULL, Longitude float NOT NULL, Accuracy float NOT NULL
    );
    CREATE INDEX IX_WorkerPositions_Recorded ON dbo.WorkerPositions(RecordedUtc, AssignmentId);
END;

INSERT dbo.Roles (NombreRol, Descripcion, Estado)
SELECT r.Nombre, r.Nombre, 1 FROM (VALUES ('Cortador'), ('Reconexion')) AS r(Nombre)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Roles WITH (UPDLOCK, HOLDLOCK) WHERE NombreRol = r.Nombre);

COMMIT;
