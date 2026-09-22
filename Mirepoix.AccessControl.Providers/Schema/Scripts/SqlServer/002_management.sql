IF OBJECT_ID(N'dbo.ac_role', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ac_role
    (
        role_id     NVARCHAR(256) NOT NULL CONSTRAINT PK_ac_role PRIMARY KEY,
        description NVARCHAR(MAX) NULL
    );
END
GO

IF OBJECT_ID(N'dbo.ac_sod_constraint', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ac_sod_constraint
    (
        constraint_id NVARCHAR(256) NOT NULL CONSTRAINT PK_ac_sod_constraint PRIMARY KEY
    );
END
GO

IF OBJECT_ID(N'dbo.ac_sod_constraint_role', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ac_sod_constraint_role
    (
        constraint_id NVARCHAR(256) NOT NULL,
        role_id       NVARCHAR(256) NOT NULL,
        CONSTRAINT PK_ac_sod_constraint_role PRIMARY KEY (constraint_id, role_id),
        CONSTRAINT FK_ac_sod_constraint_role_constraint
            FOREIGN KEY (constraint_id)
            REFERENCES dbo.ac_sod_constraint (constraint_id)
            ON DELETE CASCADE
    );
END
GO

IF EXISTS
(
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_ac_subject_role_subject'
      AND parent_object_id = OBJECT_ID(N'dbo.ac_subject_role')
)
BEGIN
    ALTER TABLE dbo.ac_subject_role DROP CONSTRAINT FK_ac_subject_role_subject;
END
GO
