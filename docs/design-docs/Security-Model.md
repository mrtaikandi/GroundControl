# Security Model

This document defines the data protection and transport security mechanisms for GroundControl.

See [Domain Model](Domain-Model.md) for entity definitions and [Data Model](Data-Model.md) for storage schemas.

For full authentication and authorization design (three pluggable modes, role hierarchy, RBAC enforcement), see [Authentication & Authorization](Authentication-Authorization.md).

---

## Client Authentication

Client applications authenticate to the Client API using a `clientId` + `clientSecret` pair.

- Clients authenticate with a `clientId` + `clientSecret` pair, tied to a specific project + scope combination.
- Both values are returned at creation time. The secret is encrypted at rest via `IValueProtector`.
- Clients send credentials in the `Authorization: ApiKey <clientId>:<clientSecret>` header. The custom `ApiKey` scheme avoids `Basic` (which can trigger browser login dialogs) and `Bearer` (reserved for Management API JWT/PAT tokens). The scheme name is case-insensitive per HTTP spec.
- The `clientSecret` is a Base64Url-encoded cryptographically random value (no padding). This encoding guarantees the secret never contains `:` (the header delimiter), `+`, `/`, or `=`, making it safe for use in the `Authorization: ApiKey <clientId>:<clientSecret>` header without additional encoding. Parsing splits on the first `:` — the `clientId` (UUID) is on the left, the secret is on the right.
- The server looks up the `clients` document by `clientId` (document ID), decrypts the stored `Secret`, and compares with the provided `clientSecret` using constant-time comparison.
- Validation checks: document exists, secrets match, `isActive = true`, not expired (`expiresAt` is null or in the future).
- On successful authentication, the server knows the `projectId` and `scopes` for the client.

### Client Key Lifecycle

**Creation:**
1. Admin creates a client key for a project + scope combination.
2. Server generates a cryptographically random secret.
3. Server protects the secret via `IValueProtector` and stores it.
4. The `clientId` (document ID) and `clientSecret` are returned in the creation response. **This is the only time the raw secret is available.**

**Rotation:**
1. Create a new key for the same project + scope.
2. Update the client application to use the new key.
3. Deactivate the old key (`isActive = false`).
4. Optionally delete the old key after a grace period.

**Revocation:**
- Set `isActive = false` on the key. Takes effect immediately; connected SSE clients using this key are disconnected.

**Expiration:**
- Optional `expiresAt` field. Expired keys are rejected at authentication time.
- A background job can periodically clean up expired keys.

---

## Management API Authentication

Management API authentication is pluggable, with three modes. Only one mode is active per instance. See [Authentication & Authorization](Authentication-Authorization.md) for the full design.

| Mode | Mechanism | Credentials |
|------|-----------|-------------|
| **None** | All requests treated as admin (system-wide Admin grant) | No credentials needed |
| **Built-In** | ASP.NET Identity with MongoDB | Cookie sessions (SPA) + JWT access tokens + refresh tokens (programmatic) |
| **External** | OIDC integration (Entra ID, Keycloak, etc.) | Session cookie via BFF pattern |

All modes additionally support **Personal Access Tokens (PATs)** for CI/CD and automation (except None mode).

### Personal Access Token Security

- PATs use the format `gc_pat_<random>` with minimum 256 bits of entropy.
- Only the SHA-256 hash is stored in the `personal_access_tokens` collection. The raw token is returned once at creation.
- The `gc_pat_` prefix enables the bearer token handler to route PATs to database lookup without attempting JWT signature validation.
- PAT validation checks: hash match, not revoked, not expired, owning user is active.
- PATs support optional permission scoping. When the `permissions` field is set, effective permissions are the intersection of the user's grant-derived permissions and the token's allowed list. When `null`, the PAT inherits all user permissions.

### Refresh Token Security

- Refresh tokens use SHA-256 hashed storage (same as PATs).
- Single-use with rotation: each refresh token can only be used once and is replaced by a new token.
- Family revocation: if a revoked refresh token is reused (possible replay/theft), all tokens with the same `FamilyId` are revoked immediately (preserving other active sessions).
- Expired tokens are auto-cleaned via a TTL index.

### CSRF Protection

Cookie-based authentication uses a double-submit cookie pattern (`XSRF-TOKEN` cookie + `X-XSRF-TOKEN` header). See [Authentication & Authorization](Authentication-Authorization.md#csrf-protection) for the full design.

### Cookie Security

| Setting | Value | Reason |
|---------|-------|--------|
| `HttpOnly` | `true` | Prevents XSS from reading the cookie |
| `Secure` | `true` (production) | Cookie only sent over HTTPS |
| `SameSite` | `Lax` | Allows top-level navigation (needed for OIDC redirect) while blocking cross-origin POST |
| Encryption | ASP.NET Data Protection | Cookie payload encrypted at rest |

### JWT Security

- HMAC-SHA256 signing with a shared secret key across all instances (same pattern as Data Protection key ring).
- Short-lived access tokens (default 1 hour).
- Signing key supplied via environment variable, never in config files.

### Rate Limiting

- `/auth/login` and `/auth/token` endpoints are rate-limited to prevent brute-force credential attacks.
- Account lockout after configurable failed attempts (default: 5 attempts, 15-minute lockout).

---

## Sensitive Value Protection

### Encryption at Rest

Configuration entries and variables marked as `isSensitive = true` have their values encrypted before storage in MongoDB.

**Protection interface (pluggable):**

```
IValueProtector
├── Protect(plainText) → protectedText
└── Unprotect(protectedText) → plainText
```

**Default implementation: ASP.NET Data Protection**

The default `IValueProtector` implementation wraps ASP.NET Core's Data Protection API (`IDataProtector`).

- Uses authenticated encryption (AES-256-CBC + HMACSHA256 by default).
- Key metadata is embedded in the ciphertext — the framework automatically selects the correct key during `Unprotect()`.
- Protected values are stored as base64-encoded strings in MongoDB.
- By default, a single key is generated and persisted to the file system (`PersistKeysToFileSystem`). No automatic rotation is configured.
- All server instances in a deployment must share the same key storage location (see [Deployment Architecture](Deployment-Architecture.md) for configuration per deployment model).

**External vault implementation (optional):**
- Instead of encrypting locally, the value protector stores a reference to an external secrets manager (Azure Key Vault, HashiCorp Vault).
- The stored value becomes a URI (e.g., `vault://path/to/secret`).
- `Unprotect()` resolves the URI and fetches the value from the external store.

### Key Rotation (Opt-In)

Key rotation is disabled by default. When enabled, ASP.NET Data Protection manages the full key lifecycle automatically.

**Enabling rotation:**
- Set `DataProtection:KeyRotation:Enabled = true` in application configuration.
- Data Protection generates a new key on a configurable schedule (default: every 90 days).
- The previous key remains in the key ring for decrypting existing values — no immediate re-encryption is needed.
- `Unprotect()` reads the key ID from the ciphertext header and selects the matching key from the ring.

**Re-encryption (optional):**
- An admin command can re-encrypt all existing values under the current active key.
- This allows retiring old keys from the ring after all values have been migrated.
- Re-encryption scope: `config_entries`, `variables`, `snapshots`, and `clients` collections.

### Key Ring Configuration (`IKeyRingConfigurator`)

The Data Protection key ring requires two things: a **storage backend** (where key XML files are persisted) and a **protection mechanism** (how those files are encrypted at rest). These are deployment-specific concerns.

**Key ring configurator interface (pluggable):**

```
IKeyRingConfigurator
└── Configure(IDataProtectionBuilder, IConfiguration) → void
```

Each implementation configures both key storage and key protection for a specific deployment model. The configurator is selected via the `DataProtection:KeyRing` configuration setting.

**Implementations:**

| Implementation | Config Value | Storage | Protection |
|---|---|---|---|
| `FileSystemKeyRingConfigurator` | `FileSystem` | File system | DPAPI (Windows) or none (dev) |
| `CertificateKeyRingConfigurator` | `Certificate` | File system | X.509 certificate via `IDataProtectionCertificateProvider` |
| `RedisKeyRingConfigurator` | `Redis` | Redis | X.509 certificate via `IDataProtectionCertificateProvider` |
| `AzureKeyRingConfigurator` | `Azure` | Azure Blob Storage | Azure Key Vault (native) |
| `AwsKeyRingConfigurator` *(future)* | `Aws` | S3 | KMS (native) |

**Relationship to `IValueProtector`:**

These operate at different lifecycle stages — `IKeyRingConfigurator` runs once at startup to initialize the Data Protection infrastructure, while `IValueProtector` uses it at runtime:

```
Startup:  IKeyRingConfigurator.Configure(builder, config)
            → ASP.NET Data Protection key ring initialized
            → IDataProtectionProvider registered in DI

Runtime:  IValueProtector (default implementation)
            → uses IDataProtector (from IDataProtectionProvider)
            → Protect/Unprotect use the key ring
```

See [Deployment Architecture](Deployment-Architecture.md) for per-deployment configuration examples.

### Certificate Sourcing (`IDataProtectionCertificateProvider`)

Certificate-based key ring configurators (`Certificate`, `Redis`) need X.509 certificates but should not be coupled to a specific certificate source. The `IDataProtectionCertificateProvider` interface abstracts where certificates are loaded from.

**Certificate provider interface (pluggable):**

```
IDataProtectionCertificateProvider
├── GetCurrentCertificateAsync() → X509Certificate2
└── GetPreviousCertificatesAsync() → IReadOnlyList<X509Certificate2>
```

`GetCurrentCertificateAsync()` returns the active certificate used by `ProtectKeysWithCertificate()` to encrypt new key ring entries. `GetPreviousCertificatesAsync()` returns any retired certificates passed to `UnprotectKeysWithAnyCertificate()` for decrypting old key ring entries during rotation. Selected via `DataProtection:Certificate:Provider`.

**Implementations:**

| Implementation | Config Value | Source | Use Case |
|---|---|---|---|
| `FileSystemCertificateProvider` | `FileSystem` | `.pfx` files on disk | Homelab, K8s volume mounts |
| `AzureBlobCertificateProvider` | `AzureBlob` | Azure Blob Storage container | Shared cloud infrastructure |
| `AzureKeyVaultCertificateProvider` *(future)* | `AzureKeyVault` | Azure Key Vault certificate store | Managed certificate lifecycle |

> **Note:** The `AzureKeyRingConfigurator` and `AwsKeyRingConfigurator` do **not** use `IDataProtectionCertificateProvider`. They use cloud-native key protection (Azure Key Vault keys / AWS KMS) which handle key wrapping directly — no X.509 certificates involved.

**FileSystem provider configuration:**

```json
{
  "DataProtection": {
    "Certificate": {
      "Provider": "FileSystem",
      "FileSystem": {
        "CurrentPath": "/certs/dp-2026.pfx",
        "Password": "...",
        "PreviousPaths": ["/certs/dp-2024.pfx"]
      }
    }
  }
}
```

**AzureBlob provider configuration:**

```json
{
  "DataProtection": {
    "Certificate": {
      "Provider": "AzureBlob",
      "AzureBlob": {
        "ContainerUri": "https://account.blob.core.windows.net/certificates",
        "CurrentBlobName": "dp-2026.pfx",
        "PreviousBlobNames": ["dp-2024.pfx"],
        "Password": "..."
      }
    }
  }
}
```

The `AzureBlobCertificateProvider` uses `DefaultAzureCredential` for authentication to the storage account.

### Certificate Lifecycle (Key Ring Protection)

When using certificate-based key ring protection (`Certificate`, `Redis`), there are two independent rotation lifecycles to manage:

| Concern | What rotates | Typical cycle | Managed by |
|---------|-------------|---------------|------------|
| Data protection keys | AES encryption keys in the key ring | 90 days (automatic) | ASP.NET Data Protection |
| Key ring protection certificate | The X.509 cert that encrypts key ring XML | Years (manual) | Operator / cert-manager |

**What the certificate protects:**

The certificate encrypts the **key ring XML files at rest** — not the application data directly. The public key encrypts new key files when they are generated; the private key is required to decrypt them at runtime. All server instances must have access to the private key.

**Certificate expiry behavior:**

ASP.NET Data Protection does **not** check certificate expiry dates when decrypting key ring files. An expired certificate still works cryptographically — expiry is a trust/validation concept, not a cryptographic limitation.

However, there is a practical caveat: if configured via **thumbprint** (`ProtectKeysWithCertificate("{thumbprint}")`), the certificate store lookup may skip expired certificates. The `IDataProtectionCertificateProvider` abstraction avoids this by loading certificates directly (from files, blob storage, etc.) rather than relying on store lookups.

**Certificate rotation workflow:**

1. Generate a new X.509 certificate.
2. Make the new certificate available to the `IDataProtectionCertificateProvider` (e.g., deploy the file, upload to blob storage).
3. Update configuration: register the new certificate as current and move the old certificate to the previous certificates list (provider-specific config).
4. Perform a rolling restart. The key ring configurator calls `IDataProtectionCertificateProvider` to obtain certificates, then:
   - `ProtectKeysWithCertificate(currentCert)` — new data protection keys are encrypted with the new certificate.
   - `UnprotectKeysWithAnyCertificate(previousCerts)` — existing keys encrypted with old certificates remain decryptable.
5. After all old data protection keys have expired (90+ days) or been re-encrypted, remove the old certificate from the provider's previous certificates configuration.

**Configuration example (certificate rotation in progress, FileSystem provider):**

```json
{
  "DataProtection": {
    "KeyRing": "Certificate",
    "KeyStorePath": "./keys",
    "Certificate": {
      "Provider": "FileSystem",
      "FileSystem": {
        "CurrentPath": "/certs/dp-2026.pfx",
        "Password": "...",
        "PreviousPaths": ["/certs/dp-2024.pfx"]
      }
    },
    "KeyRotation": {
      "Enabled": true,
      "KeyLifetime": 90
    }
  }
}
```

**Operational risks:**

| Risk | Impact | Mitigation |
|------|--------|------------|
| Premature old certificate removal | Keys encrypted with that cert become **permanently undecryptable** — all data they protect is lost | Keep old certs for at least 90 days after rotation; verify no keys reference the old cert before removal |
| Key ring memory cache (24h) | Instances cache the key ring in memory and only refresh approximately every 24 hours | Perform a rolling restart after certificate rotation; do not remove old certs within 24 hours |
| Certificate provider unavailable at startup | Application cannot decrypt the key ring and fails to start | Ensure the certificate source (file system, blob storage) is available before the application starts; use health checks |
| Lost private key | Key ring files encrypted with that key become permanently undecryptable | Back up certificate `.pfx` files securely; use managed certificate services in cloud deployments |

### Sensitive Value Handling by Context

| Context | Behavior |
|---------|----------|
| MongoDB storage | Encrypted at rest |
| Admin API responses | Masked (e.g., `"***"`) by default. Decrypted values returned only with `?decrypt=true` query parameter, requiring `sensitive_values:decrypt` permission. Every decrypted read is audit-logged. |
| Audit records | Always masked |
| Snapshot storage | Encrypted |
| Client API delivery (SSE/REST) | Decrypted for authorized clients (connection is TLS-encrypted in transit) |
| Local file cache (client SDK) | Sensitive entries are encrypted when the consumer supplies an `IConfigurationProtector`; non-sensitive entries are stored plaintext. With no protector configured, all entries are plaintext (explicit opt-out). |
| Logs | Never logged in plaintext |

---

## Transport Security

| Path | Requirement |
|------|-------------|
| Admin API | TLS required in production. |
| Client API (SSE/REST) | TLS required. Client credentials must only be sent over HTTPS. |
| MongoDB connection | TLS recommended, especially for multi-region deployments. |
| Inter-instance communication | Handled by MongoDB replication (TLS configurable at MongoDB level). |

---

## Security Checklist

- [ ] Client secrets are cryptographically random (minimum 256 bits of entropy)
- [ ] Client secrets encrypted at rest via `IValueProtector`
- [ ] Sensitive config values encrypted at rest via ASP.NET Data Protection
- [ ] Data Protection key ring persisted to a shared location accessible by all server instances
- [ ] TLS enforced for all API endpoints in production
- [ ] Audit logging for all modification events
- [ ] No sensitive values in log output
- [ ] Input validation on all API endpoints to prevent injection attacks
- [ ] PATs stored as SHA-256 hashes (irreversible)
- [ ] Refresh tokens stored as SHA-256 hashes with single-use rotation
- [ ] CSRF protection enabled for cookie-based authentication
- [ ] Session cookies use `HttpOnly`, `Secure`, `SameSite=Lax`
- [ ] JWT signing key shared securely across instances (environment variable or K8s Secret)
- [ ] Rate limiting on authentication endpoints
- [ ] Account lockout configured for built-in authentication
- [ ] Admin seed password supplied via environment variable, not config files
