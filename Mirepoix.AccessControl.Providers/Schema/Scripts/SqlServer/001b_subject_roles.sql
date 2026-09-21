IF OBJECT_ID(N'dbo.ac_subject_role', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ac_subject_role
    (
        subject_id NVARCHAR(256) NOT NULL,
        role       NVARCHAR(256) NOT NULL,
        CONSTRAINT PK_ac_subject_role PRIMARY KEY (subject_id, role)
    );
END
GO
