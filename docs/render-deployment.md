# Render deployment and CD

> Synchronized with `render.yaml` and runtime code on 2026-08-21.

## Deployment model

```text
GitHub master
 -> GitHub Actions CI
 -> checks pass
 -> Render checksPass auto deploy
 -> Docker web service
 -> Render PostgreSQL 17 + pgvector
```

The Blueprint is `render.yaml`.

## Resources

The Blueprint provisions:

- `prn222-rag-assistant`: free Docker web service in Singapore;
- `prn222-rag-db`: free managed PostgreSQL 17 in Singapore.

The database connection is supplied through `fromDatabase.connectionString`. Startup normalizes Render `postgresql://...` URLs to the Npgsql form used by the app.

When startup migrations are enabled, the application enables pgvector before EF migrations and reloads Npgsql type metadata so a fresh database can use the newly created `vector` type reliably.

## Current AI runtime: Gemini chat + OpenRouter embeddings

Render uses Gemini for Chat while preserving the existing OpenRouter embedding corpus.

Current Blueprint values:

```text
Rag__Provider=OpenRouter
Rag__ChatProvider=Gemini
Rag__EmbeddingProvider=OpenRouter
Rag__EmbeddingDimensions=1024

Rag__Gemini__ChatModels=gemini-3.5-flash-lite,gemini-3.1-flash-lite,gemini-2.5-flash,gemini-2.5-flash-lite
Rag__OpenRouter__EmbeddingModel=nvidia/llama-nemotron-embed-vl-1b-v2:free
```

`Rag__Provider=OpenRouter` remains the backward-compatible base value, but the purpose-specific overrides determine the actual Render chat and embedding providers.

Gemini Chat models are tried in the configured order. The service advances to the next model for quota/rate-limit, timeout, model-unavailable/not-found, or transient `5xx` failures before response text has been emitted. It does not fallback for invalid requests or authentication/authorization failures, and it never mixes two models after streaming output has begun.

## Required manual AI secrets

The current Render deployment needs **two** AI secrets:

```text
Rag__Gemini__ApiKey
Rag__OpenRouter__ApiKey
```

Both are declared with `sync: false` and must be entered in the Render Dashboard. Never commit their real values.

The Gemini key is used by Chat; the OpenRouter key is used by embeddings.

## Embedding continuity

Render keeps the 1024-dimensional OpenRouter embedding model so the existing corpus does not need a provider/model migration merely because Chat changed to Gemini.

Changing only Chat provider/model/fallback order does not require document re-indexing.

Changing the embedding provider/model/dimension still requires a full corpus re-index. PR #37 makes different-dimension transitions safe from pgvector dimension errors, but does not make different embedding semantic spaces interchangeable.

## Data Protection durability

PR #38 fixed the former antiforgery/auth key-ring problem on ephemeral web containers.

Runtime configuration now uses:

```text
DataProtectionKeyDbContext
AddDataProtection().PersistKeysToDbContext<DataProtectionKeyDbContext>()
SetApplicationName("PRN222-RAG-Assistant")
```

The key ring is stored in PostgreSQL (`DataProtectionKeys`) rather than only on the web-service filesystem. A normal web restart/redeploy therefore does not lose the key ring as long as the database remains available/persistent.

This does not turn the free database into production-grade infrastructure; it only moves the Data Protection state to the durable system of record used by the demo.

## Optional seed users

The Blueprint keeps:

```text
Auth__SeedUsers__Enabled=false
```

If seeding is enabled, each optional account is created only when its required values are configured. An entirely missing account section is skipped; a partially configured account fails fast.

For a demo, an Admin seed alone is sufficient. Student users can self-register, and Admin can manage roles through the product UI.

Never commit seed passwords.

## First deployment

1. Create/apply a Render Blueprint from this repository.
2. Enter `Rag__Gemini__ApiKey`.
3. Enter `Rag__OpenRouter__ApiKey`.
4. Review the web service and PostgreSQL resources.
5. Apply the Blueprint.
6. Confirm `/healthz` returns HTTP 200.
7. Review startup logs for pgvector enablement/type reload and EF migrations.
8. Sign in and verify Flow 2 Chat can generate answers and index/retrieval uses the expected embedding configuration.

After Blueprint setup, normal commits to `master` deploy automatically after repository checks pass.

## Health check

Render calls:

```text
GET /healthz
```

This endpoint confirms the process is serving HTTP. Fatal startup/migration failures prevent the app from reaching a healthy state.

## Port and proxy behavior

Render injects `PORT`; the container binds Kestrel to the injected port and preserves local port 8080 behavior for Compose.

`ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` is configured for platform proxy headers.

The final runtime image includes the Linux GSSAPI/Kerberos library expected by Npgsql on the slim ASP.NET image.

## Source-file storage warning

Uploaded source documents are stored under:

```text
/app/storage/uploads
```

On a free Render web service this filesystem is ephemeral. A restart/redeploy can therefore remove uploaded source files even while database metadata/chunks remain.

For durable hosting:

- use a persistent disk on an eligible Render plan; or
- move source documents to object storage and persist object keys/URLs.

Do not treat the free web-service filesystem as durable storage.

## Database warning

The Blueprint's free PostgreSQL instance is suitable for demo/development convenience, not production durability/SLA expectations. Upgrade before relying on it for long-lived production data.

## CI relation

GitHub Actions validates the application and PostgreSQL schema before the `checksPass` deployment trigger. CI explicitly checks the `DataProtectionKeys` table and a pgvector mixed-dimension retrieval scenario introduced after PR #37/#38.

## Secrets policy

Never commit:

- Gemini/OpenRouter/OpenAI API keys;
- database credentials;
- seed-user passwords;
- production `.env` files;
- deploy tokens/hooks.
