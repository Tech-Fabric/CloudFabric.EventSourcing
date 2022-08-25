# Database

```sql
DROP DATABASE IF EXISTS cloudfabric_eventsourcing_demos_todolist;
DROP ROLE IF EXISTS cloudfabric_eventsourcing_demos_todolist;
CREATE ROLE cloudfabric_eventsourcing_demos_todolist WITH
    NOSUPERUSER
    NOCREATEROLE
    INHERIT
    NOREPLICATION
    CONNECTION LIMIT -1
    LOGIN
    PASSWORD 'cloudfabric_eventsourcing_demos_todolist';

CREATE DATABASE cloudfabric_eventsourcing_demos_todolist
    WITH 
    OWNER = cloudfabric_eventsourcing_demos_todolist
    ENCODING = 'UTF8'
    CONNECTION LIMIT = -1;
GRANT ALL ON DATABASE cloudfabric_eventsourcing_demos_todolist TO postgres;
```
