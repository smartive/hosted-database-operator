# hosted-database-operator

Kubernetes operator that provides and orchestrates databases on database servers

## Note on Google Cloudsql Postgres

Since postgres on google cloud sql does not support "superuser" privileges,
this operator cannot create users / databases on any postgres instance that
is a managed could sql instance. For Cloud SQL, only mysql is supported.

If you want to use postgres, use a VM or host it on kubernetes itself and direct
the operator to this location instead.
