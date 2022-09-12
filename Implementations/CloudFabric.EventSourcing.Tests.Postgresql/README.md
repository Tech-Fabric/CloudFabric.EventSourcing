```sql
DROP DATABASE IF EXISTS cloudfabric_eventsourcing_test;
DROP ROLE IF EXISTS cloudfabric_eventsourcing_test;
CREATE ROLE cloudfabric_eventsourcing_test WITH
NOSUPERUSER
NOCREATEROLE
LOGIN
CREATEDB
INHERIT
NOREPLICATION
CONNECTION LIMIT -1
PASSWORD 'cloudfabric_eventsourcing_test';

CREATE DATABASE cloudfabric_eventsourcing_test
WITH
OWNER = cloudfabric_eventsourcing_test
ENCODING = 'UTF8'
CONNECTION LIMIT = -1;

GRANT ALL ON DATABASE cloudfabric_eventsourcing_test TO postgres;
```