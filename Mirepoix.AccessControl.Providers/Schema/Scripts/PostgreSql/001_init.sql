CREATE TABLE IF NOT EXISTS ac_policy_set
(
    id            INTEGER      NOT NULL PRIMARY KEY,
    version       VARCHAR(128) NOT NULL,
    payload_json  TEXT         NOT NULL,
    updated_utc   TIMESTAMPTZ  NOT NULL
);

CREATE TABLE IF NOT EXISTS ac_resource_attribute
(
    resource_type VARCHAR(256) NOT NULL,
    resource_id   VARCHAR(256) NOT NULL,
    name          VARCHAR(256) NOT NULL,
    value_json    TEXT         NULL,
    PRIMARY KEY (resource_type, resource_id, name)
);
