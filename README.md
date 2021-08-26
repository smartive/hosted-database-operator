# Hosted Database Operator

This is a Kubernetes Operator that manages databases on DB-Servers.

There are three CRDs that get installed:

- `ClusterDatabaseHost`: Defines access (with a secret) to a
  database server (currently MySql and Postgres supported)
- `HostedDatabase`: Defines an instance of a database
  on a host
- `DanglingDatabase`: When a hosted database gets deleted
  and the "OnDelete" action is to create a dangling database,
  this element gets created by the operator. When the dangling DB
  is deleted, the real database gets deleted as well.

This Operator can create users with passwords with a database
on MySql and PostgreSQL servers. It creates a secret with
the access data to the specific database.

## Example

1. Create the database host.

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: mysql-host-credentials
  namespace: default
stringData:
  username: root
  password: MySuperSecretPassword
---
apiVersion: hdo.smartive.ch/v2
kind: ClusterDatabaseHost
metadata:
  name: test-mysql-host
spec:
  type: MySql
  host: 127.0.0.1
  port: 3306
  credentialsSecret:
    name: mysql-host-credentials
    namespace: default
```

After the host is created, a database can be instantiated:

```yaml
apiVersion: hdo.smartive.ch/v2
kind: HostedDatabase
metadata:
  name: test-mysql-db
  namespace: default
spec:
  host: test-mysql-host
```

When the HostedDatabase is deleted, the default operation of the
Operator is to create a dangling database. With a recreate of
the hosted database, the db remains the same. If the dangling database
is deleted as well, the data is lost.

It works on MySql > 5.7 and PostgreSQL > 9.2 (tested).

## Installation

To install the operator, you may use the Kustomize files in
`./src/HostedDatabaseOperator/config`. The simplest way is
to use the `kustomization.yaml` in the `install` subdirectory.

An example file that installs the operator into the
predefined namespace (`hosted-database-operator-system`)
would be:

```yaml
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization

namespace: hosted-database-operator-system

resources:
  - github.com/smartive/hosted-database-operator/src/HostedDatabaseOperator/config/install?ref=v2.0.0
```
