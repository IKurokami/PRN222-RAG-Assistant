# Render deployment and CD

> Ownership: Member 1 deployment/infrastructure coordination.

## Deployment model

The repository uses a Render Blueprint (`render.yaml`) for a reproducible deployment stack:

```text
GitHub master
  -> GitHub Actions CI
  -> checks pass
  -> Render auto deploy
  -> Docker web service
  -> Render Postgres
```

Render is configured with `autoDeployTrigger: checksPass`, so a commit on `master` is deployed only after the repository checks pass.

## Resources

The Blueprint provisions:

- `prn222-rag-assistant`: Docker web service;
- `prn222-rag-db`: managed Render Postgres 17 in the same Singapore region.

The application receives the database connection through `fromDatabase.connectionString`. Startup normalizes Render's `postgresql://...` URL into an Npgsql connection string before registering EF Core.

When `Database__ApplyMigrationsOnStartup=true`, startup first runs:

```sql
CREATE EXTENSION IF NOT EXISTS vector;
```

and then applies EF Core migrations. Because Npgsql caches PostgreSQL type metadata, startup reloads pgvector type metadata immediately after enabling the extension. This is required on a brand-new Render database where the Npgsql datasource can exist before the `vector` type is created.

## OpenRouter-only AI runtime

Render uses OpenRouter for both chat and embeddings:

```text
Rag__Provider=OpenRouter
Rag__ChatProvider=OpenRouter
Rag__EmbeddingProvider=OpenRouter
Rag__EmbeddingDimensions=1024
```

Configured models:

```text
Chat:
  nvidia/nemotron-3.5-lightning:free

Embedding:
  nvidia/llama-nemotron-embed-vl-1b-v2:free
```

The embedding model intentionally remains the Llama Nemotron Embed VL 1B V2 free variant because the existing pgvector corpus is 1024-dimensional and this model supports 1024-dimensional output. Changing to an embedding model that only emits a different dimension requires a schema/corpus migration and a complete re-index.

## Environment variable entered manually

Only one AI secret is required for the default Render deployment:

```text
Rag__OpenRouter__ApiKey
```

`render.yaml` declares it with `sync: false`, so the real key is entered in the Render Dashboard during initial Blueprint setup and is never committed to GitHub.

### Optional seed users

The Blueprint leaves seeded users disabled by default:

```text
Auth__SeedUsers__Enabled=false
```

Seed accounts are independent. When seeding is enabled, each account is created only when its Email, Password, and DisplayName are all configured. An entirely missing account section is skipped; a partially configured account fails startup so a secret typo does not silently create an unusable deployment.

For a Render demo environment, seeding only the Admin account is enough:

```text
Auth__SeedUsers__Enabled=true
Auth__SeedUsers__Admin__Email=<enter manually>
Auth__SeedUsers__Admin__Password=<enter manually>
Auth__SeedUsers__Admin__DisplayName=Administrator
```

`SubjectLeader` and `Student` seed settings may remain completely unset. Student users can also use public registration, while Admin can manage/assign the remaining application roles through the product UI.

Do not commit seeded-user credentials.

## First deployment

1. Merge the Render CD PR into `master`.
2. Open Render Dashboard -> New -> Blueprint.
3. Connect this repository and let Render read `render.yaml`.
4. Enter `Rag__OpenRouter__ApiKey` when prompted.
5. Review the `prn222-rag-assistant` web service and `prn222-rag-db` database.
6. Apply the Blueprint.
7. Confirm `/healthz` returns HTTP 200.
8. Check logs for successful pgvector enablement, type reload, and EF migration startup.

After the first Blueprint setup, normal commits to `master` do not require manually triggering a deployment. Render follows the configured `checksPass` CD trigger.

## Storage warning

The application currently stores uploaded source files under:

```text
/app/storage/uploads
```

On a free Render web service, this local filesystem is ephemeral and no persistent disk can be attached. A redeploy/restart can therefore remove uploaded source files even though document metadata/chunks remain in Postgres.

For a longer-lived deployment, use one of these follow-ups:

- upgrade the web service and attach a persistent Render disk at `/app/storage/uploads`; or
- move source documents to object storage and keep only object keys/URLs in the application.

Do not treat the free filesystem as durable document storage.

## Free-tier database warning

The Blueprint uses the Render Postgres Free instance for development/demo convenience. Free Render Postgres should not be treated as production persistence. Upgrade the database before relying on it for long-lived data.

## Port and proxy behavior

Render injects `PORT`. The Docker entrypoint binds Kestrel to:

```text
0.0.0.0:${PORT}
```

and falls back to `8080` for local Docker Compose.

`ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` is set in Render so ASP.NET Core can honor the platform proxy headers before HTTPS redirection.

The final Docker image also installs the Linux GSSAPI/Kerberos runtime library expected by Npgsql, avoiding the non-fatal `libgssapi_krb5.so.2` loader error seen on the slim ASP.NET runtime image.

## Health check

Render checks:

```text
GET /healthz
```

The endpoint is intentionally lightweight and anonymous. It verifies the web process is serving HTTP; database migration failures still fail application startup before the service becomes healthy.

## Runtime warnings

ASP.NET Core Data Protection keys are currently stored on the web-service filesystem. On the free ephemeral filesystem, a redeploy can invalidate existing login cookies. This is acceptable for the current demo deployment but should be moved to durable shared storage or another supported persistent key store before treating the service as production-grade.

## Secrets policy

Never commit:

- OpenRouter/Gemini/OpenAI API keys;
- Render database credentials;
- seeded-user passwords;
- `.env` production files;
- deploy tokens or deploy-hook secrets.

The Blueprint's database wiring and `sync: false` variables avoid requiring these values in GitHub source control.
