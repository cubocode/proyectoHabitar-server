/* =====================================================================================
   HabitAR - Migración v2: correcciones + Mapa de Riesgo + Fondos
   Ejecutar UNA vez contra (localdb)\MSSQLLocalDB (SSMS o Visual Studio > SQL Server Object Explorer).
   Es re-ejecutable: cada paso verifica si ya está aplicado.
   ===================================================================================== */
USE habitarBD;
GO

/* ---------- 1. Usuarios: espacio para contraseñas con hash BCrypt (60 caracteres) ---------- */
IF COL_LENGTH('dbo.Usuarios', 'Contrasena') IS NOT NULL
   AND (SELECT max_length FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Usuarios') AND name = 'Contrasena') BETWEEN 0 AND 199
BEGIN
    ALTER TABLE dbo.Usuarios ALTER COLUMN Contrasena NVARCHAR(100) NOT NULL;
    PRINT 'Usuarios.Contrasena ampliada a NVARCHAR(100).';
END
GO

/* Nombre de usuario único (solo si no hay repetidos y no existe ya un índice único) */
IF NOT EXISTS (SELECT 1 FROM sys.indexes i
               JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
               JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
               WHERE i.object_id = OBJECT_ID('dbo.Usuarios') AND i.is_unique = 1 AND c.name = 'Usuario')
   AND NOT EXISTS (SELECT Usuario FROM dbo.Usuarios GROUP BY Usuario HAVING COUNT(*) > 1)
BEGIN
    CREATE UNIQUE INDEX UX_Usuarios_Usuario ON dbo.Usuarios(Usuario);
    PRINT 'Índice único UX_Usuarios_Usuario creado.';
END
GO

/* ---------- 2. Beneficiarios: baja lógica ---------- */
IF COL_LENGTH('dbo.Beneficiarios', 'Activo') IS NULL
BEGIN
    ALTER TABLE dbo.Beneficiarios ADD Activo BIT NOT NULL CONSTRAINT DF_Beneficiarios_Activo DEFAULT (1);
    PRINT 'Beneficiarios.Activo agregado.';
END
GO
IF COL_LENGTH('dbo.Beneficiarios', 'FechaBaja') IS NULL
    ALTER TABLE dbo.Beneficiarios ADD FechaBaja DATETIME2 NULL;
GO

/* FechaRelevamiento se completa sola si la tabla no tenía un DEFAULT */
IF NOT EXISTS (SELECT 1 FROM sys.default_constraints dc
               JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
               WHERE dc.parent_object_id = OBJECT_ID('dbo.Beneficiarios') AND c.name = 'FechaRelevamiento')
BEGIN
    ALTER TABLE dbo.Beneficiarios ADD CONSTRAINT DF_Beneficiarios_FechaRelevamiento DEFAULT (SYSDATETIME()) FOR FechaRelevamiento;
    PRINT 'DEFAULT agregado a Beneficiarios.FechaRelevamiento.';
END
GO

/* ---------- 3. Beneficiarios: ubicación para el Mapa de Riesgo ---------- */
IF COL_LENGTH('dbo.Beneficiarios', 'Latitud') IS NULL
    ALTER TABLE dbo.Beneficiarios ADD Latitud DECIMAL(9, 6) NULL;
GO
IF COL_LENGTH('dbo.Beneficiarios', 'Longitud') IS NULL
    ALTER TABLE dbo.Beneficiarios ADD Longitud DECIMAL(9, 6) NULL;
GO
IF COL_LENGTH('dbo.Beneficiarios', 'UbicacionAproximada') IS NULL
    ALTER TABLE dbo.Beneficiarios ADD UbicacionAproximada BIT NOT NULL CONSTRAINT DF_Beneficiarios_UbicacionAprox DEFAULT (0);
GO

/* DNI y CUIL únicos (si ya hay repetidos se avisa y no se crea: hay que depurarlos a mano) */
IF NOT EXISTS (SELECT 1 FROM sys.indexes i
               JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
               JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
               WHERE i.object_id = OBJECT_ID('dbo.Beneficiarios') AND i.is_unique = 1 AND c.name = 'NumeroDocumento')
BEGIN
    IF EXISTS (SELECT NumeroDocumento FROM dbo.Beneficiarios GROUP BY NumeroDocumento HAVING COUNT(*) > 1)
        PRINT 'ATENCIÓN: hay DNI repetidos en Beneficiarios; no se creó el índice único.';
    ELSE
        CREATE UNIQUE INDEX UX_Beneficiarios_Dni ON dbo.Beneficiarios(NumeroDocumento);
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes i
               JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
               JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
               WHERE i.object_id = OBJECT_ID('dbo.Beneficiarios') AND i.is_unique = 1 AND c.name = 'Cuil')
BEGIN
    IF EXISTS (SELECT Cuil FROM dbo.Beneficiarios GROUP BY Cuil HAVING COUNT(*) > 1)
        PRINT 'ATENCIÓN: hay CUIL repetidos en Beneficiarios; no se creó el índice único.';
    ELSE
        CREATE UNIQUE INDEX UX_Beneficiarios_Cuil ON dbo.Beneficiarios(Cuil);
END
GO

/* ---------- 4. Fondos: caja de ingresos y egresos ---------- */
IF OBJECT_ID('dbo.MovimientosFondos', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.MovimientosFondos
    (
        Id               INT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_MovimientosFondos PRIMARY KEY,
        FechaHora        DATETIME2(0)   NOT NULL,
        Tipo             NVARCHAR(10)   NOT NULL CONSTRAINT CK_MovimientosFondos_Tipo CHECK (Tipo IN (N'Ingreso', N'Egreso')),
        Categoria        NVARCHAR(60)   NOT NULL,
        Concepto         NVARCHAR(150)  NOT NULL,
        Detalle          NVARCHAR(500)  NULL,
        Monto            DECIMAL(18, 2) NOT NULL CONSTRAINT CK_MovimientosFondos_Monto CHECK (Monto > 0),
        MedioPago        NVARCHAR(40)   NOT NULL,
        Comprobante      NVARCHAR(60)   NULL,
        UsuarioId        INT            NULL,
        UsuarioNombre    NVARCHAR(120)  NOT NULL,
        FechaRegistro    DATETIME2(0)   NOT NULL CONSTRAINT DF_MovimientosFondos_FechaRegistro DEFAULT (SYSDATETIME()),
        Anulado          BIT            NOT NULL CONSTRAINT DF_MovimientosFondos_Anulado DEFAULT (0),
        MotivoAnulacion  NVARCHAR(200)  NULL,
        FechaAnulacion   DATETIME2(0)   NULL,
        UsuarioAnulacion NVARCHAR(120)  NULL
    );

    CREATE INDEX IX_MovimientosFondos_FechaHora ON dbo.MovimientosFondos(FechaHora) INCLUDE (Tipo, Monto, Anulado, Categoria);
    PRINT 'Tabla MovimientosFondos creada.';
END
GO

PRINT 'Migración v2 aplicada. Las fichas nuevas o editadas se puntúan solas al guardarse.';
GO
