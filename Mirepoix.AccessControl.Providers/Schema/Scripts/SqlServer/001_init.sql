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
