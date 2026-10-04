# Render Environment Variables for Backend

This API now supports Render-style environment variables directly.

## Auto-mapped Render variables

- `PORT`
  - Used to bind Kestrel to `http://0.0.0.0:$PORT`.
- `DATABASE_URL`
  - Supports Render PostgreSQL URL format like:
  - `postgres://user:password@host:5432/database`
  - Automatically converted to `ConnectionStrings:DefaultConnection`.
- `REDIS_URL`
  - Supports `redis://` or `rediss://` and maps to `ConnectionStrings:Redis`.

## Recommended .NET-style variables (hierarchical)

You can set ASP.NET configuration keys in Render using double underscores (`__`).

Examples:

- `ConnectionStrings__DefaultConnection`
- `ConnectionStrings__Redis`
- `Jwt__Key`
- `Jwt__Issuer`
- `Jwt__Audience`
- `Stripe__SecretKey`
- `Stripe__WebhookSecret`
- `Recaptcha__SecretKey`
- `FrontendUrl`
- `ASPNETCORE_ENVIRONMENT=Production`

## Minimum required in Production

- Database:
  - Either `DATABASE_URL` (postgres:// from Neon/Render) or `ConnectionStrings__DefaultConnection` (Npgsql key=value)
  - `Database__AutoInitialize=false` (Neon already has the DB; full init tries CREATE DATABASE and can crash)
  - `Database__RunMigrationsOnStartup=true` (default in Production when AutoInitialize is false — applies column/slug patches only)
- JWT:
  - `Jwt__Key` (at least 32 chars)
  - `Jwt__Issuer`
  - `Jwt__Audience`
- Recaptcha:
  - `Recaptcha__SecretKey`
- Frontend CORS:
  - `FrontendUrl`

## Notes

- If Redis is not configured, the API uses in-memory cache (warning in logs — OK for small traffic).
- Prefer setting secrets in Render dashboard, not in appsettings files.
- `DATABASE_URL` is converted automatically to Npgsql format with SSL for Neon.
