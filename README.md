# Fullstack App

A full-stack project management application — ASP.NET Core 10 API, Angular 22 frontend, PostgreSQL — built as a portfolio project and engineered to production standards. (The product name is provisional.)

## Stack

| Layer          | Technology                                                                                                     |
| -------------- | -------------------------------------------------------------------------------------------------------------- |
| Backend        | ASP.NET Core 10, EF Core + Npgsql, ASP.NET Identity, FluentValidation                                          |
| Frontend       | Angular 22 (standalone components, signals, OnPush), Angular Material, hybrid SSR via `@angular/ssr` + Express |
| Database       | PostgreSQL 17                                                                                                  |
| Testing        | xUnit (backend), Playwright (e2e)                                                                              |
| Infrastructure | Docker / Podman Compose                                                                                        |

## Architecture highlights

- **Authentication** — JWT bearer access tokens plus rotating refresh tokens with reuse detection: tokens are stored hashed, every refresh rotates the token, and reuse of a revoked token revokes the user's whole token family. Role-based authorization policies (Admin / User) on top of ASP.NET Identity.
- **Hybrid SSR** — public pages are server-rendered for first paint and SEO; authenticated routes are client-rendered. This keeps the API a client-agnostic, token-based JSON contract that a future mobile client can consume unchanged.
- **GDPR-aware deletion model** — soft delete with a dedicated trash area. Projects can be hard-purged after a retention window; user erasure is implemented as anonymization (PII scrubbed, row retained) because audit foreign keys (`created_by`/`updated_by`) reference users with `Restrict` semantics.
- **Workspaces** — every project belongs to a workspace, and every user gets a personal one on registration. Shared workspaces are the same entity with more members: owners invite by email (a known address joins immediately, an unknown one gets a redeemable token), members contribute, and owners alone can remove or move a project. Access is enforced inside the SQL query as a membership subquery, not after fetching, and a non-member gets 404 rather than 403 — existence and access are deliberately indistinguishable.
- **Cross-cutting hygiene** — automatic audit stamping in the DbContext, global exception handling with RFC 9457 ProblemDetails, request validation via FluentValidation, health checks (`/health`), OpenAPI with a Scalar reference UI in development.

## Getting started

Requires Docker (or Podman) with Compose. No local .NET or Node installation is needed to run the app.

1. Copy the template and fill in the blanks:

   ```bash
   cp .env.example .env
   ```

   `POSTGRES_PASSWORD` and the password inside `DB_CONNECTION_STRING` must match.
   Generate the signing key with `openssl rand -base64 48`.

2. Start the stack:

   ```bash
   docker compose up -d
   ```

   Migrations and seed data apply automatically on API startup.

3. Open the app:

   - Frontend: http://localhost:4200
   - API health: http://localhost:8080/health
   - API reference (dev only): http://localhost:8080/scalar

   Development seed users: `dev1@example.com` (Admin) and `dev2@example.com` (User), both with password `Password123!`.

## Testing

```bash
# Backend unit/integration tests (runs on the host)
dotnet test

# End-to-end tests (Playwright, runs in a container against the live stack)
docker compose --profile e2e run --rm e2e npx playwright test
```

## Project structure

```
backend/            ASP.NET Core API (controllers, services, EF Core data layer, migrations)
backend.Tests/      xUnit test suite
frontend-angular/   Angular app — core/ (guards, interceptors, services), features/, shared/
  e2e/              Playwright specs
compose.yaml        Dev stack: api, db, frontend (+ e2e profile)
```

## Status

Actively developed. Workspaces and workspace-scoped project access are in place. Current focus is production hardening: rate limiting and account lockout, a production deployment target with TLS, and the account lifecycle (email confirmation, password reset).
