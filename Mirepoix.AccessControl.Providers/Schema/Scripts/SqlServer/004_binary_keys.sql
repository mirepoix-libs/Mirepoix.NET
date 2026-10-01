-- Existing databases created before Latin1_General_BIN2 was on the key columns.
-- New databases already use that collation in the create scripts, so each block no-ops.

IF OBJECT_ID(N'dbo.ac_subject_attribute', N'U') IS NOT NULL
   AND EXISTS
   (
       SELECT 1
       FROM sys.foreign_keys
       WHERE name = N'FK_ac_subject_attribute_subject'
         AND parent_object_id = OBJECT_ID(N'dbo.ac_subject_attribute')
   )
   AND
   (
       EXISTS
       (
           SELECT 1 FROM sys.columns
           WHERE object_id = OBJECT_ID(N'dbo.ac_subject')
             AND name = N'subject_id'
             AND collation_name <> N'Latin1_General_BIN2'
       )
       OR EXISTS
       (
           SELECT 1 FROM sys.columns
           WHERE object_id = OBJECT_ID(N'dbo.ac_subject_attribute')
             AND name IN (N'subject_id', N'name')
             AND collation_name <> N'Latin1_General_BIN2'
       )
   )
BEGIN
    ALTER TABLE dbo.ac_subject_attribute DROP CONSTRAINT FK_ac_subject_attribute_subject;
END
GO

IF OBJECT_ID(N'dbo.ac_sod_constraint_role', N'U') IS NOT NULL
   AND EXISTS
   (
       SELECT 1
       FROM sys.foreign_keys
       WHERE name = N'FK_ac_sod_constraint_role_constraint'
         AND parent_object_id = OBJECT_ID(N'dbo.ac_sod_constraint_role')
   )
   AND
   (
       EXISTS
       (
           SELECT 1 FROM sys.columns
           WHERE object_id = OBJECT_ID(N'dbo.ac_sod_constraint')
             AND name = N'constraint_id'
             AND collation_name <> N'Latin1_General_BIN2'
       )
       OR EXISTS
       (
           SELECT 1 FROM sys.columns
           WHERE object_id = OBJECT_ID(N'dbo.ac_sod_constraint_role')
             AND name IN (N'constraint_id', N'role_id')
             AND collation_name <> N'Latin1_General_BIN2'
       )
   )
BEGIN
    ALTER TABLE dbo.ac_sod_constraint_role DROP CONSTRAINT FK_ac_sod_constraint_role_constraint;
END
GO

IF OBJECT_ID(N'dbo.ac_subject_role', N'U') IS NOT NULL
   AND EXISTS
   (
       SELECT 1 FROM sys.columns
       WHERE object_id = OBJECT_ID(N'dbo.ac_subject_role')
         AND name IN (N'subject_id', N'role')
         AND collation_name <> N'Latin1_General_BIN2'
   )
BEGIN
    ALTER TABLE dbo.ac_subject_role DROP CONSTRAINT PK_ac_subject_role;
    ALTER TABLE dbo.ac_subject_role ALTER COLUMN subject_id NVARCHAR(256) COLLATE Latin1_General_BIN2 NOT NULL;
    ALTER TABLE dbo.ac_subject_role ALTER COLUMN role NVARCHAR(256) COLLATE Latin1_General_BIN2 NOT NULL;
    ALTER TABLE dbo.ac_subject_role ADD CONSTRAINT PK_ac_subject_role PRIMARY KEY (subject_id, role);
END
GO

IF OBJECT_ID(N'dbo.ac_role', N'U') IS NOT NULL
   AND EXISTS
   (
       SELECT 1 FROM sys.columns
       WHERE object_id = OBJECT_ID(N'dbo.ac_role')
         AND name = N'role_id'
         AND collation_name <> N'Latin1_General_BIN2'
   )
BEGIN
    ALTER TABLE dbo.ac_role DROP CONSTRAINT PK_ac_role;
    ALTER TABLE dbo.ac_role ALTER COLUMN role_id NVARCHAR(256) COLLATE Latin1_General_BIN2 NOT NULL;
    ALTER TABLE dbo.ac_role ADD CONSTRAINT PK_ac_role PRIMARY KEY (role_id);
END
GO

IF OBJECT_ID(N'dbo.ac_subject', N'U') IS NOT NULL
   AND EXISTS
   (
       SELECT 1 FROM sys.columns
       WHERE object_id = OBJECT_ID(N'dbo.ac_subject')
         AND name = N'subject_id'
         AND collation_name <> N'Latin1_General_BIN2'
   )
BEGIN
    ALTER TABLE dbo.ac_subject DROP CONSTRAINT PK_ac_subject;
    ALTER TABLE dbo.ac_subject ALTER COLUMN subject_id NVARCHAR(256) COLLATE Latin1_General_BIN2 NOT NULL;
    ALTER TABLE dbo.ac_subject ADD CONSTRAINT PK_ac_subject PRIMARY KEY (subject_id);
END
GO

IF OBJECT_ID(N'dbo.ac_subject_attribute', N'U') IS NOT NULL
   AND EXISTS
   (
       SELECT 1 FROM sys.columns
       WHERE object_id = OBJECT_ID(N'dbo.ac_subject_attribute')
         AND name IN (N'subject_id', N'name')
         AND collation_name <> N'Latin1_General_BIN2'
   )
BEGIN
    ALTER TABLE dbo.ac_subject_attribute DROP CONSTRAINT PK_ac_subject_attribute;
    ALTER TABLE dbo.ac_subject_attribute ALTER COLUMN subject_id NVARCHAR(256) COLLATE Latin1_General_BIN2 NOT NULL;
    ALTER TABLE dbo.ac_subject_attribute ALTER COLUMN name NVARCHAR(256) COLLATE Latin1_General_BIN2 NOT NULL;
    ALTER TABLE dbo.ac_subject_attribute ADD CONSTRAINT PK_ac_subject_attribute PRIMARY KEY (subject_id, name);
END
GO

IF OBJECT_ID(N'dbo.ac_subject', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.ac_subject_attribute', N'U') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM sys.foreign_keys
       WHERE name = N'FK_ac_subject_attribute_subject'
         AND parent_object_id = OBJECT_ID(N'dbo.ac_subject_attribute')
   )
BEGIN
    ALTER TABLE dbo.ac_subject_attribute
        ADD CONSTRAINT FK_ac_subject_attribute_subject
        FOREIGN KEY (subject_id) REFERENCES dbo.ac_subject (subject_id);
END
GO

IF OBJECT_ID(N'dbo.ac_sod_constraint', N'U') IS NOT NULL
   AND EXISTS
   (
       SELECT 1 FROM sys.columns
       WHERE object_id = OBJECT_ID(N'dbo.ac_sod_constraint')
         AND name = N'constraint_id'
         AND collation_name <> N'Latin1_General_BIN2'
   )
BEGIN
    ALTER TABLE dbo.ac_sod_constraint DROP CONSTRAINT PK_ac_sod_constraint;
    ALTER TABLE dbo.ac_sod_constraint ALTER COLUMN constraint_id NVARCHAR(256) COLLATE Latin1_General_BIN2 NOT NULL;
    ALTER TABLE dbo.ac_sod_constraint ADD CONSTRAINT PK_ac_sod_constraint PRIMARY KEY (constraint_id);
END
GO

IF OBJECT_ID(N'dbo.ac_sod_constraint_role', N'U') IS NOT NULL
   AND EXISTS
   (
       SELECT 1 FROM sys.columns
       WHERE object_id = OBJECT_ID(N'dbo.ac_sod_constraint_role')
         AND name IN (N'constraint_id', N'role_id')
         AND collation_name <> N'Latin1_General_BIN2'
   )
BEGIN
    ALTER TABLE dbo.ac_sod_constraint_role DROP CONSTRAINT PK_ac_sod_constraint_role;
    ALTER TABLE dbo.ac_sod_constraint_role ALTER COLUMN constraint_id NVARCHAR(256) COLLATE Latin1_General_BIN2 NOT NULL;
    ALTER TABLE dbo.ac_sod_constraint_role ALTER COLUMN role_id NVARCHAR(256) COLLATE Latin1_General_BIN2 NOT NULL;
    ALTER TABLE dbo.ac_sod_constraint_role ADD CONSTRAINT PK_ac_sod_constraint_role PRIMARY KEY (constraint_id, role_id);
END
GO

IF OBJECT_ID(N'dbo.ac_sod_constraint', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.ac_sod_constraint_role', N'U') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM sys.foreign_keys
       WHERE name = N'FK_ac_sod_constraint_role_constraint'
         AND parent_object_id = OBJECT_ID(N'dbo.ac_sod_constraint_role')
   )
BEGIN
    ALTER TABLE dbo.ac_sod_constraint_role
        ADD CONSTRAINT FK_ac_sod_constraint_role_constraint
        FOREIGN KEY (constraint_id) REFERENCES dbo.ac_sod_constraint (constraint_id)
        ON DELETE CASCADE;
END
GO
