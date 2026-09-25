# Idfy.Api

A .NET 8 minimal API that wraps the [IDfy EVE](https://eve.idfy.com) v3 tasks
(document validation, OCR extraction, verify-with-source, masking and face
compare). Every call to IDfy is logged to SQL Server with document images and
extracted PII redacted.

## Endpoints

The API is unauthenticated — it is intended for internal use behind the network
perimeter. Browser access is governed by [CORS](#cors). `GET /openapi/v1.json`
is served in Development only.

### Document validation

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/documents/validate` | Validate a document image (URL or Base64). |
| POST | `/api/documents/validate/upload` | Validate an uploaded image file. |

### OCR extraction

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/pan/extract` | Extract PAN card details (URL/Base64). |
| POST | `/api/pan/extract/upload` | Extract PAN card details from an uploaded file. |
| POST | `/api/aadhaar/extract` | Extract Aadhaar details. **Requires consent.** |
| POST | `/api/aadhaar/extract/upload` | Extract Aadhaar details from an uploaded file. **Requires consent.** |
| POST | `/api/driving-license/extract` | Extract driving-licence details. |
| POST | `/api/driving-license/extract/upload` | Extract driving-licence details from an uploaded file. |
| POST | `/api/passport/extract` | Extract passport details (`document2` back page optional). |
| POST | `/api/passport/extract/upload` | Extract passport details from uploaded file(s). |
| POST | `/api/voter-id/extract` | Extract voter-ID details (`document2` back side optional). |
| POST | `/api/voter-id/extract/upload` | Extract voter-ID details from uploaded file(s). |

### Aadhaar masking

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/aadhaar/mask` | Mask the Aadhaar number in an image; returns URLs to the masked/original images. **Requires consent.** |
| POST | `/api/aadhaar/mask/upload` | Mask the Aadhaar number in an uploaded file. **Requires consent.** |

### Verify with source

Each type offers a synchronous call (result returned directly) and an
asynchronous pair (submit returns a `requestId`; poll returns `202` while
processing, `200` once complete).

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/driving-license/verify/sync` | Verify a driving licence against the source (sync). |
| POST | `/api/driving-license/verify` | Submit async driving-licence verification. |
| GET | `/api/driving-license/verify/{requestId}` | Poll async driving-licence verification. |
| POST | `/api/voter-id/verify/sync` | Verify a voter ID against the source (sync). |
| POST | `/api/voter-id/verify` | Submit async voter-ID verification. |
| GET | `/api/voter-id/verify/{requestId}` | Poll async voter-ID verification. |
| POST | `/api/passport/verify/sync` | Verify a passport against the source (sync). |
| POST | `/api/passport/verify` | Submit async passport verification. |
| GET | `/api/passport/verify/{requestId}` | Poll async passport verification. |

### PAN-Aadhaar link

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/pan-aadhaar-link/verify/sync` | Check PAN-Aadhaar linkage (sync). |
| POST | `/api/pan-aadhaar-link/verify` | Submit async PAN-Aadhaar link check. |
| GET | `/api/pan-aadhaar-link/verify/{requestId}` | Poll async PAN-Aadhaar link check. |

### Face compare

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/face/compare` | Compare two face images and return a match result. |
| POST | `/api/face/compare/upload` | Compare two uploaded face image files. |

### Operational

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/health` | Liveness/readiness (checks log-DB connectivity). Anonymous. |
| GET | `/openapi/v1.json` | OpenAPI document. Development only, anonymous. |

## Request shapes

- **JSON endpoints** take `document` (a public URL or Base64 string), plus any
  type-specific fields. `taskId`/`groupId` are optional GUIDs (generated when
  omitted).
- **`/upload` endpoints** take a multipart form with `file` (and `file2` where a
  second image applies), converted to Base64 before forwarding.
- **Common validation:** image resolution (150–10000px; **150–4096px** for face
  compare), a 3 MB Base64 payload cap, image-only uploads, duplicate/unknown
  JSON properties rejected.
- **Aadhaar endpoints** require `consent: true`. **Face compare** requires both
  images. **PAN-Aadhaar link** takes `panNumber` + `aadhaarNumber`
  (format-validated).

See [`src/Idfy.Api/Idfy.Api.http`](src/Idfy.Api/Idfy.Api.http) for runnable
examples of every endpoint, and [`docs/INTEGRATION.md`](docs/INTEGRATION.md)
for the full integration guide (request/response fields, async polling, error
handling, retry guidance).

## CORS

Cross-origin browser requests are allowed only from the origins listed in
`Cors:AllowedOrigins`. List exact origins (e.g. `https://app.internal`), or a
single `"*"` entry to allow any origin. When the list is empty, cross-origin
browser calls are blocked. Any header and method are allowed. Per-caller (by IP)
rate limiting returns `429` on breach.

## Error handling

- Caller-fixable IDfy errors (bad input, unusable image, rate limit) are passed
  through with IDfy's status and error code.
- Our own problems (credentials, credits) and IDfy outages become `502` without
  leaking internal detail; timeouts become `504`.
- Every error response carries a `traceId` for log correlation.

## Logging & PII

Both legs of every call are written to SQL Server across four tables:

- **`IdfyRequestLogs`** — the inbound call from the internal caller: method,
  path, redacted request body, the status/response we returned, client IP and
  timing. Captures calls that never reach IDfy too (e.g. validation `400`s).
- **`IdfyApiCallLogs`** — the outbound IDfy call: full request/response (masked)
  and timing.
- **`IdfyTasks`** — structured, PII-free record per task (type, status, timing,
  error code) for querying.
- **`IdfyErrorLogs`** — unhandled server exceptions.

Rows from all four correlate on **`TraceId`**, so one inbound request links to
the IDfy call(s) it triggered.

Document images, extracted personal details and signed document URLs are
redacted/masked before logging (inbound `document`/`document2` images and
camelCase PII fields too; multipart uploads are logged only as a size note).
Writes happen off the request path in a batched background writer, so logging
never blocks or fails a request. Logs are retained indefinitely for audit;
the application never deletes them.

Schema: [`db/log-tables.sql`](db/log-tables.sql) (idempotent; run once per
environment).

## Configuration

Configured via `appsettings.json`, environment variables or user-secrets:

| Key | Purpose |
|-----|---------|
| `Idfy:BaseUrl`, `Idfy:AccountId`, `Idfy:ApiKey` | IDfy endpoint and credentials. |
| `Idfy:*ImageLimits` | Per-task resolution limits. |
| `Idfy:AdvancedFeatureKeys` | Validate feature key names (from the IDfy SPOC). |
| `ConnectionStrings:LogDb` | SQL Server connection string for the log tables. |
| `Cors:AllowedOrigins` | Browser origins allowed to call the API (`"*"` for any). |
| `RateLimit` | Rate-limit settings. |

### Secrets

The two secrets — `Idfy:ApiKey` and `ConnectionStrings:LogDb` — must **never**
be committed. `appsettings.json` ships with empty placeholders.

Provide them per environment as **environment variables** (config keys map with
`__` for nesting):

```
Idfy__AccountId=<account-id>
Idfy__ApiKey=<idfy-api-key>
ConnectionStrings__LogDb=<sql-connection-string>
```

**On IIS**, set these on the app pool (*Advanced Settings → Environment
Variables*) or in the server's `web.config` (which stays on the server, not in
the repo):

```xml
<aspNetCore ...>
  <environmentVariables>
    <environmentVariable name="Idfy__ApiKey" value="<idfy-api-key>" />
    <environmentVariable name="ConnectionStrings__LogDb" value="<sql-connection-string>" />
  </environmentVariables>
</aspNetCore>
```

For local development, `dotnet user-secrets` is the convenient equivalent.

## Running locally

Requires the .NET 8 SDK (any later SDK also builds it). Deploy targets need the
.NET 8 runtime; on IIS, the ASP.NET Core 8 Hosting Bundle.

```sh
# 1. Start SQL Server (see docker-compose.yml) and create the schema
export MSSQL_SA_PASSWORD='<choose-a-password>'
docker compose up -d
docker exec -i idfy-sql /opt/mssql-tools18/bin/sqlcmd -C -U sa -P "$MSSQL_SA_PASSWORD" -Q "IF DB_ID('IdfyLogs') IS NULL CREATE DATABASE IdfyLogs"
docker exec -i idfy-sql /opt/mssql-tools18/bin/sqlcmd -C -U sa -P "$MSSQL_SA_PASSWORD" -d IdfyLogs < db/log-tables.sql

# 2. Configure secrets
cd src/Idfy.Api
dotnet user-secrets set "ConnectionStrings:LogDb" "Server=localhost,14333;Database=IdfyLogs;User Id=sa;Password=$MSSQL_SA_PASSWORD;TrustServerCertificate=True"
dotnet user-secrets set "Idfy:AccountId" "<account-id>"
dotnet user-secrets set "Idfy:ApiKey" "<api-key>"

# 3. Run and test
dotnet run
dotnet test           # from the repo root
```

## Project layout

```
src/Idfy.Api/         The API (Endpoints, Services, Models, Logging, Data, Options)
tests/Idfy.Api.Tests/ xUnit unit + integration tests
db/log-tables.sql     Log table schema
docker-compose.yml    Local SQL Server Express
```
