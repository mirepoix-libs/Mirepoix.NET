CREATE TABLE IF NOT EXISTS ac_policy_set
(
    id            INTEGER      NOT NULL PRIMARY KEY,
    version       VARCHAR(128) NOT NULL,
    payload_json  TEXT         NOT NULL,
    updated_utc   TIMESTAMPTZ  NOT NULL
);
