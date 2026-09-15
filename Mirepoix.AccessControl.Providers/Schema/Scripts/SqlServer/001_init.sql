IF OBJECT_ID(N'dbo.ac_policy_set', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ac_policy_set
    (
        id            INT            NOT NULL CONSTRAINT PK_ac_policy_set PRIMARY KEY,
        version       NVARCHAR(128)  NOT NULL,
        payload_json  NVARCHAR(MAX)  NOT NULL,
        updated_utc   DATETIME2      NOT NULL
    );
END
GO

IF OBJECT_ID(N'dbo.ac_subject', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ac_subject
    (
        subject_id NVARCHAR(256) NOT NULL CONSTRAINT PK_ac_subject PRIMARY KEY
    );
END
GO

IF OBJECT_ID(N'dbo.ac_subject_role', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ac_subject_role
    (
        subject_id NVARCHAR(256) NOT NULL,
        role       NVARCHAR(256) NOT NULL,
        CONSTRAINT PK_ac_subject_role PRIMARY KEY (subject_id, role),
        CONSTRAINT FK_ac_subject_role_subject FOREIGN KEY (subject_id) REFERENCES dbo.ac_subject (subject_id)
    );
END
GO

IF OBJECT_ID(N'dbo.ac_subject_attribute', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ac_subject_attribute
    (
        subject_id NVARCHAR(256) NOT NULL,
        name       NVARCHAR(256) NOT NULL,
        value_json NVARCHAR(MAX) NULL,
        CONSTRAINT PK_ac_subject_attribute PRIMARY KEY (subject_id, name),
        CONSTRAINT FK_ac_subject_attribute_subject FOREIGN KEY (subject_id) REFERENCES dbo.ac_subject (subject_id)
    );
END
GO

IF OBJECT_ID(N'dbo.ac_resource_attribute', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ac_resource_attribute
    (
        resource_type NVARCHAR(256) NOT NULL,
        resource_id   NVARCHAR(256) NOT NULL,
        name          NVARCHAR(256) NOT NULL,
        value_json    NVARCHAR(MAX) NULL,
        CONSTRAINT PK_ac_resource_attribute PRIMARY KEY (resource_type, resource_id, name)
    );
END
GO
