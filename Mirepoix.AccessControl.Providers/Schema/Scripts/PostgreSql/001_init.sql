CREATE TABLE IF NOT EXISTS ac_policy_set
(
    id            INTEGER      NOT NULL PRIMARY KEY,
    version       VARCHAR(128) NOT NULL,
    payload_json  TEXT         NOT NULL,
    updated_utc   TIMESTAMPTZ  NOT NULL
);

CREATE TABLE IF NOT EXISTS ac_subject
(
    subject_id VARCHAR(256) NOT NULL PRIMARY KEY
);

CREATE TABLE IF NOT EXISTS ac_subject_role
(
    subject_id VARCHAR(256) NOT NULL,
    role       VARCHAR(256) NOT NULL,
    PRIMARY KEY (subject_id, role),
    CONSTRAINT fk_ac_subject_role_subject FOREIGN KEY (subject_id) REFERENCES ac_subject (subject_id)
);

CREATE TABLE IF NOT EXISTS ac_subject_attribute
(
    subject_id VARCHAR(256) NOT NULL,
    name       VARCHAR(256) NOT NULL,
    value_json TEXT         NULL,
    PRIMARY KEY (subject_id, name),
    CONSTRAINT fk_ac_subject_attribute_subject FOREIGN KEY (subject_id) REFERENCES ac_subject (subject_id)
);

CREATE TABLE IF NOT EXISTS ac_resource_attribute
(
    resource_type VARCHAR(256) NOT NULL,
    resource_id   VARCHAR(256) NOT NULL,
    name          VARCHAR(256) NOT NULL,
    value_json    TEXT         NULL,
    PRIMARY KEY (resource_type, resource_id, name)
);
