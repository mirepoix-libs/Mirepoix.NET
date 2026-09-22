IF OBJECT_ID(N'dbo.ac_subject', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ac_subject
    (
        subject_id NVARCHAR(256) NOT NULL CONSTRAINT PK_ac_subject PRIMARY KEY
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
        CONSTRAINT FK_ac_subject_attribute_subject
            FOREIGN KEY (subject_id)
            REFERENCES dbo.ac_subject (subject_id)
    );
END
GO
