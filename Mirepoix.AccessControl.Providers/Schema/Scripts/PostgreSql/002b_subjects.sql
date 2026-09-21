CREATE TABLE IF NOT EXISTS ac_subject
(
    subject_id VARCHAR(256) NOT NULL PRIMARY KEY
);

CREATE TABLE IF NOT EXISTS ac_subject_attribute
(
    subject_id VARCHAR(256) NOT NULL,
    name       VARCHAR(256) NOT NULL,
    value_json TEXT         NULL,
    PRIMARY KEY (subject_id, name),
    CONSTRAINT fk_ac_subject_attribute_subject
        FOREIGN KEY (subject_id)
        REFERENCES ac_subject (subject_id)
);
