CREATE TABLE IF NOT EXISTS ac_role
(
    role_id     VARCHAR(256) NOT NULL PRIMARY KEY,
    description TEXT         NULL
);

CREATE TABLE IF NOT EXISTS ac_sod_constraint
(
    constraint_id VARCHAR(256) NOT NULL PRIMARY KEY
);

CREATE TABLE IF NOT EXISTS ac_sod_constraint_role
(
    constraint_id VARCHAR(256) NOT NULL,
    role_id       VARCHAR(256) NOT NULL,
    PRIMARY KEY (constraint_id, role_id),
    CONSTRAINT fk_ac_sod_constraint_role_constraint
        FOREIGN KEY (constraint_id)
        REFERENCES ac_sod_constraint (constraint_id)
        ON DELETE CASCADE
);

ALTER TABLE IF EXISTS ac_subject_role
    DROP CONSTRAINT IF EXISTS fk_ac_subject_role_subject;
