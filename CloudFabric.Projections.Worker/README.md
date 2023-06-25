# CloudFabric.Projections.Worker

Background worker process which polls projection state index to check if the index needs to be rebuilt because of schema changes.

Rebuilding projections is a complicated task that can take hours.

But at the same time, to completely avoid rebuilds we would need to forbid removing and renaming any properties on the schema which will not be intuitive for users,
they expect to be able to remove a property and add new property with the same name later.

So CloudFabric.EventSourcing does not put any limits on projections, schema changes etc. 
It tries it's best for projections to just work by implementing following algorithm:

When new projection schema is detected, the library will create a new index and run a background process to populate it while still
using old index for document queries and updates. During this process everything works as usual. 
One can even deploy a new projection schema during the process - that will 
simply stop existing index rebuild process and start another one while still using old index for queries and updates.
Once the rebuild is done, all queries and updates will be switched to a new index.
