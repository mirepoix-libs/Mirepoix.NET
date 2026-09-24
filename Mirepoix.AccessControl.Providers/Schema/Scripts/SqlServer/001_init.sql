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
