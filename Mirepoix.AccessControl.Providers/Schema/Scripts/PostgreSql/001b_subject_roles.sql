CREATE TABLE IF NOT EXISTS ac_subject_role
(
    subject_id VARCHAR(256) NOT NULL,
    role       VARCHAR(256) NOT NULL,
    PRIMARY KEY (subject_id, role)
);
