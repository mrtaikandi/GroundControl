# Server Configuration

GroundControl is configured through `appsettings.json`, environment variables, or any .NET configuration provider. Environment variables use `__` (double underscore) as section separators.

## MongoDB

| Setting | Default | Description |
|---|---|---|
| `ConnectionStrings:Storage` | _(required)_ | MongoDB connection string. Must point to a replica set. |
| `Persistence:MongoDb:DatabaseName` | `GroundControl` | Name of the MongoDB database. |
| `Persistence:MongoDb:CollectionPrefix` | _(none)_ | Optional prefix for all collection names. Useful for sharing a database across environments. |
| `Persistence:MongoDb:ConnectionStringKey` | `Storage` | Key in `ConnectionStrings` section to use. Override if your connection string has a different key. |

Example:

```json
{
  "ConnectionStrings": {
    "Storage": "mongodb://mongo1:27017,mongo2:27017,mongo3:27017/?replicaSet=rs0"
  },
  "Persistence": {
    "MongoDb": {
      "DatabaseName": "groundcontrol-prod",
      "CollectionPrefix": "gc_"
    }
  }
}
```

## Authentication

| Setting | Default | Description |
|---|---|---|
| `Authentication:Mode` | `None` | Auth mode: `None`, `BuiltIn`, or `External`. See [Authentication](authentication.md). |

See [Authentication](authentication.md) for mode-specific settings (JWT, OIDC, password policy, etc.).

## Data Protection

Data Protection manages encryption keys used for sensitive configuration values and auth tokens. Choose a mode based on your deployment.

| Setting | Default | Description |
|---|---|---|
| `DataProtection:Mode` | `FileSystem` | Key storage mode: `FileSystem`, `Certificate`, `Redis`, or `Azure`. |
| `DataProtection:KeyStorePath` | `./keys` | Directory for key files. Used by all modes as the local key path. |
| `DataProtection:UseDpapi` | `false` | Protect keys with DPAPI (Windows only, FileSystem mode). |

### FileSystem mode (default)

Keys are stored as XML files in `KeyStorePath`. Suitable for single-instance deployments.

```json
{
  "DataProtection": {
    "Mode": "FileSystem",
    "KeyStorePath": "/keys"
  }
}
```

> **Warning:** Back up the key directory. If keys are lost, encrypted values (sensitive config entries, auth tokens) become permanently unreadable.

### Certificate mode

Keys are stored on the file system and the key XML is encrypted at rest with an X.509 certificate. The same configuration applies under Redis mode (which uses Redis storage but the same certificate-based key protection).

| Setting | Description |
|---|---|
| `DataProtection:CertificateProvider` | `FileSystem` or `AzureBlob`. |
| `DataProtection:FileSystemCertificate:Path` | Path to the current `.pfx` certificate file (`FileSystem` provider). |
| `DataProtection:FileSystemCertificate:Password` | Certificate password used for the current and previous file-system certificates. |
| `DataProtection:FileSystemCertificate:PreviousPaths` | Optional array of `.pfx` paths for certificates that previously protected the key ring (`FileSystem` provider). Required during certificate rotation so existing key XML remains decryptable until rotated out. |
| `DataProtection:AzureBlobCertificate:BlobUri` | Blob URI for the current certificate (`AzureBlob` provider). |
| `DataProtection:AzureBlobCertificate:Password` | Certificate password used for the current and previous Azure Blob certificates. |
| `DataProtection:AzureBlobCertificate:PreviousBlobUris` | Optional array of blob URIs for previously-used certificates (`AzureBlob` provider). Same semantics as `FileSystemCertificate:PreviousPaths`. |

```json
{
  "DataProtection": {
    "Mode": "Certificate",
    "KeyStorePath": "/keys",
    "CertificateProvider": "FileSystem",
    "FileSystemCertificate": {
      "Path": "/certs/dp-current.pfx",
      "Password": "<from-secret-store>",
      "PreviousPaths": [ "/certs/dp-previous.pfx" ]
    }
  }
}
```

> **Certificate rotation:** generate the new cert, deploy it as `FileSystemCertificate:Path`, move the old cert into `FileSystemCertificate:PreviousPaths`, and perform a rolling restart. New key ring entries are encrypted with the new cert; entries written under the previous cert remain decryptable as long as that cert stays in the previous list. Remove a cert from `PreviousPaths` only after every key encrypted with it has expired (90+ days by default) or been re-encrypted — otherwise the data those keys protect becomes permanently unreadable.

### Redis mode

Keys are stored in Redis and protected with an X.509 certificate. Suitable for multi-instance deployments.

| Setting | Default | Description |
|---|---|---|
| `DataProtection:Redis:ConnectionString` | _(required)_ | Redis connection string. |
| `DataProtection:Redis:KeyName` | `groundcontrol-data-protection` | Redis key name for the key ring. |
| `DataProtection:Redis:ConnectTimeoutMs` | `5000` | Connection timeout in milliseconds. |

Also requires the same certificate settings as Certificate mode (`CertificateProvider`, `FileSystemCertificate:Path` / `AzureBlobCertificate:BlobUri`, optional previous-certificate paths/URIs).

### Azure mode

Keys are stored in Azure Blob Storage and protected with Azure Key Vault.

| Setting | Description |
|---|---|
| `DataProtection:Azure:BlobUri` | Azure Blob Storage URI for key persistence. |
| `DataProtection:Azure:KeyVaultKeyId` | Azure Key Vault key identifier for key encryption. |

## Change Notifier

Controls how GroundControl instances detect snapshot activations.

| Setting | Default | Description |
|---|---|---|
| `ChangeNotifier:Mode` | `InProcess` | `InProcess` for single-instance, `MongoChangeStream` for multi-instance. |

- **InProcess:** Notifications stay within the process. Use for single-instance deployments.
- **MongoChangeStream:** Uses MongoDB change streams. All instances watching the same database receive notifications. Required for multi-instance deployments.

## Cache Warmup

| Setting | Default | Description |
|---|---|---|
| `Cache:PrewarmOnStartup` | `false` | Pre-load active snapshots for all projects at startup. Reduces first-request latency for client connections. |

## Health Endpoints

| Endpoint | Purpose |
|---|---|
| `GET /healthz/liveness` | Process running (always 200). |
| `GET /healthz/ready` | Dependencies healthy (MongoDB, change notifier). Returns 503 if unhealthy. |

## Logging

Standard ASP.NET Core logging configuration:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "System": "Warning",
      "Microsoft": "Warning"
    }
  }
}
```

## Admin seed

When using `BuiltIn` authentication, you can seed an initial admin account at startup:

| Setting | Default | Description |
|---|---|---|
| `Authentication:Seed:AdminUsername` | `admin` | Username for the seed admin account. |
| `Authentication:Seed:AdminEmail` | `admin@local` | Email for the seed admin account. |
| `Authentication:Seed:AdminPassword` | _(none)_ | Password for the seed admin. If not set, no admin is seeded. |

## Environment variable examples

```bash
# MongoDB
export ConnectionStrings__Storage="mongodb://mongo:27017/?replicaSet=rs0"
export Persistence__MongoDb__DatabaseName="groundcontrol"

# Authentication
export Authentication__Mode="BuiltIn"
export Authentication__BuiltIn__Jwt__Secret="YOUR_BASE64_SECRET_AT_LEAST_32_BYTES"
export Authentication__Seed__AdminPassword="YourSecurePassword123!"

# Data Protection
export DataProtection__Mode="Redis"
export DataProtection__Redis__ConnectionString="redis:6379"
export DataProtection__CertificateProvider="FileSystem"
export DataProtection__FileSystemCertificate__Path="/certs/dp.pfx"
export DataProtection__FileSystemCertificate__Password="certpass"

# Change notification
export ChangeNotifier__Mode="MongoChangeStream"

# Cache
export Cache__PrewarmOnStartup="true"
```

## What's next?

- [Authentication](authentication.md) — set up auth modes
- [Deployment](deployment.md) — deployment patterns
