```sql
DROP DATABASE IF EXISTS fiber_eventsourcing_test;
DROP ROLE IF EXISTS fiber_eventsourcing_test;
CREATE ROLE fiber_eventsourcing_test WITH
NOSUPERUSER
NOCREATEROLE
LOGIN
CREATEDB
INHERIT
NOREPLICATION
CONNECTION LIMIT -1
PASSWORD 'fiber_eventsourcing_test';

CREATE DATABASE fiber_eventsourcing_test
WITH
OWNER = fiber_eventsourcing_test
ENCODING = 'UTF8'
CONNECTION LIMIT = -1;

GRANT ALL ON DATABASE fiber_eventsourcing_test TO postgres;
```