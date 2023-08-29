[![codecov](https://codecov.io/gh/Tech-Fabric/CloudFabric.EventSourcing/graph/badge.svg?token=NR55NTMBTP)](https://codecov.io/gh/Tech-Fabric/CloudFabric.EventSourcing)

# Event Sourcing Concepts

Event sourcing is a design pattern that captures all changes made to an application's state as a sequence of events. It stores a log of events instead of the current state of the application. This log can be used to recreate the application's state at any point in time.

Event sourcing provides a complete audit trail of all changes made to the application's state, which is useful in applications where data integrity and compliance are crucial. It also helps with debugging and troubleshooting as it shows what changes were made to the application's state before a particular issue.

However, storing a log of events can require more storage space than storing the current state of the application. Additionally, reconstructing the application's state from a log of events can be complex.

In summary, event sourcing is a powerful tool for building complex applications that require strict data integrity and auditing capabilities. It's important to weigh the benefits and downsides before deciding to use this design pattern.

## Domain Driven Design

Event sourcing is a good fit for the Domain-Driven Design (DDD) methodology, which utilizes Domain Aggregates to define domain models. These aggregates don't change their state directly; rather, they emit domain events that are saved in an event repository. In order to retrieve an aggregate from the repository, it's necessary to retrieve the entire sequence of events from the repository and apply them in order.

## Projections

An important challenge with event-sourced domain models is the inability to search or filter the dataset because only a stream of events is persisted, with no state stored in the database. To address this, a concept of "Projections" is used. A Projection is a separate storage for an aggregate state with only the required properties, usually just those needed for filtering or display on a user interface. Since all state modifications to aggregates are made through events, one could have another entity called a Projection Builder that listens to the same events and modifies the aggregate's "projection" by applying the same logic to the record stored in "projection".

# CloudFabric.EventSourcing

The library is designed as a framework for building event-sourced applications.

Main building blocks are:

AggregateBase - base class for DDD-style aggragates. Provides methods for raising events and defining event handlers.



### Test database

```
CREATE ROLE cloudfabric_eventsourcing_test WITH
	NOSUPERUSER
	NOCREATEROLE
	INHERIT
	NOREPLICATION
	CONNECTION LIMIT -1
    LOGIN
	PASSWORD 'cloudfabric_eventsourcing_test';

DROP DATABASE IF EXISTS cloudfabric_eventsourcing_test;
CREATE DATABASE cloudfabric_eventsourcing_test
    WITH 
    OWNER = cloudfabric_eventsourcing_test
    ENCODING = 'UTF8'
    CONNECTION LIMIT = -1;
GRANT ALL ON DATABASE cloudfabric_eventsourcing_test TO postgres;
```