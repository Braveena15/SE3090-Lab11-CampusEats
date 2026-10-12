# CampusEats · SE3090 Lab 11

A working campus menu dashboard built with React/Vite, ASP.NET Core 8, EF Core and PostgreSQL. The deployment architecture is **Netlify → Render Docker API → Neon PostgreSQL**, with GitHub Actions for CI. No repository was created, code pushed, or cloud resources deployed by the assistant.

The UI fetches real database records through `GET /api/menu`, supports category filters and text search, and shows loading, empty, error and retry states. Eight sample dishes include LKR prices, category, indicative preparation times and vegetarian labels. Authentication is API-only; this is a menu discovery app, with no ordering or payment feature. Food photographs are illustrative Unsplash images, not evidence of the named dishes. Cards fall back to a bundled SVG when an image fails. Google Fonts and Unsplash require internet access; system fonts are the fallback.

## Project structure

```text
SE3090-Lab11/
├── frontend/
│   ├── src/                 # Dashboard, API client, responsive CSS, UI tests
│   ├── public/              # Local image fallback
│   ├── .env.example         # Public API origin placeholder
│   ├── package.json
│   ├── package-lock.json
│   └── vite.config.js
├── backend/
│   ├── CampusEats.Api/
│   │   ├── Data/            # PostgreSQL EF model + design-time factory
│   │   ├── Migrations/      # Initial schema and eight seed records
│   │   ├── Program.cs
│   │   └── appsettings.json
│   ├── CampusEats.Api.Tests/ # Validation and PostgreSQL integration tests
│   ├── .env.example
│   ├── .dockerignore
│   └── Dockerfile
├── .config/dotnet-tools.json
├── .github/workflows/ci.yml
├── CampusEats.sln
├── netlify.toml
├── render.yaml
├── .gitignore
└── README.md
```

## Local prerequisites

- .NET SDK 8; Node.js 22.12+ (Node 24 also works); npm; Git.
- PostgreSQL 16 running locally, or a separate development Neon database.
- Docker Desktop with its Linux engine running to verify the container.
- Commands below use PowerShell and start in the repository root.

### 1. Database setup

Using your PostgreSQL administrator account, create a dedicated development user and database. `-W` prompts for the administrator password; `\password` prompts for a new password without placing it in a command or SQL file.

```powershell
psql -h localhost -U postgres -W
```

Inside psql:

```sql
CREATE ROLE campuseats_dev LOGIN;
\password campuseats_dev
CREATE DATABASE campuseats OWNER campuseats_dev;
\q
```

Set your connection privately in the current PowerShell session. Enter an Npgsql connection string when prompted, for example the following **placeholder template**, replacing its fields: `Host=localhost;Port=5432;Database=campuseats;Username=campuseats_dev;Password=Braveena`. Quote password values according to Npgsql syntax if they contain semicolons.

```powershell
Set-Location C:\Users\brave\Documents\SE3090-Lab11
$connectionInput = Read-Host 'Npgsql connection string' -AsSecureString
$env:ConnectionStrings__Default = [System.Net.NetworkCredential]::new('', $connectionInput).Password
$env:Jwt__Key = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$env:Jwt__Issuer = 'CampusEats'
$env:Jwt__Audience = 'CampusEats'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:PORT = '8080'
dotnet restore CampusEats.sln
dotnet tool restore
dotnet ef database update --project backend/CampusEats.Api
dotnet run --project backend/CampusEats.Api
```

The API listens on `http://localhost:8080`. Local CORS already permits `http://localhost:5173` and `http://localhost:4173`. Environment variables are session-specific: set them again in any new API/test terminal. Changing the generated JWT key invalidates previously issued tokens. The design-time EF factory can generate migrations without credentials, but applying them needs a valid connection. Do not use `EnsureCreated` with this migration-managed database.

### 2. Frontend (second terminal)

```powershell
Set-Location C:\Users\brave\Documents\SE3090-Lab11\frontend
npm ci
npm run dev
```

Open `http://localhost:5173`. Local development uses `http://localhost:8080` when `VITE_API_URL` is absent. For a different API origin, copy `.env.example` to `.env`, replace the placeholder, and restart Vite. Do not use the example placeholder unchanged. The value is the API origin, without `/api` (the client appends `/api/menu`).

```powershell
Copy-Item .env.example .env
```

`.env` is ignored. ASP.NET Core does **not** load `backend/.env` automatically: use process environment variables, user secrets, or hosting settings. Both `.env.example` files contain placeholders only. Never put a database connection, JWT key or other secret in a `VITE_` variable: Vite embeds them in public JavaScript at build time.

## API and authentication

| Endpoint | Behaviour |
|---|---|
| `GET /api/menu` | Public menu ordered by ID |
| `GET /api/menu?category=Drinks` | Case-insensitive category match |
| `GET /health/live` | Process liveness |
| `GET /health/ready` | Checks the menu table is queryable; 503 if database/schema unavailable |
| `POST /api/auth/register` | JSON `{ "email": "<EMAIL>", "password": "<PASSWORD>" }`; 201, 400 or 409 |
| `POST /api/auth/login` | Same JSON; 200 with `token`/`expiresAt`, or 401 |
| `GET /api/auth/me` | Requires `Authorization: Bearer <TOKEN>`; returns ID/email |

JWT uses HMAC SHA-256, issuer/audience/signature/lifetime validation and one-hour expiry. Passwords use ASP.NET Core's salted password hasher. Email addresses are normalized and unique in PostgreSQL. Passwords must be 10–128 characters. Auth endpoints have a per-process request limit (10/minute per observed remote IP); behind a proxy the observed IP may be shared. This lab implementation does not provide email verification, reset, refresh, distributed throttling, or a sign-in UI. Use HTTPS on hosted services. JWT is optional: leaving `Jwt__Key` empty disables register/login with HTTP 503 while the menu remains available. A configured key must be at least 32 UTF-8 bytes and have nonempty issuer/audience.

Quick local checks:

```powershell
Invoke-RestMethod http://localhost:8080/health/ready
Invoke-RestMethod http://localhost:8080/api/menu
Invoke-RestMethod 'http://localhost:8080/api/menu?category=Drinks'
```

## Tests and builds

Frontend:

```powershell
Set-Location frontend
npm ci
npm test
npm run build
npm audit
Set-Location ..
```

Backend integration tests require a **dedicated test database** with the migration applied and `Jwt__Key` configured. Create `campuseats_test` using the database setup above, change the session connection to it, then run:

```powershell
dotnet restore CampusEats.sln
dotnet tool restore
dotnet build CampusEats.sln -c Release --no-restore
dotnet ef database update --project backend/CampusEats.Api --configuration Release --no-build
dotnet ef migrations has-pending-model-changes --project backend/CampusEats.Api --configuration Release --no-build
dotnet test CampusEats.sln -c Release --no-build
```

The integration test verifies eight seeds, category filtering, readiness, allowed/denied CORS origins, registration/duplicate registration, incorrect passwords, valid JWTs and invalid tokens. Test registrations create randomly named users in the test database. Tests fail clearly if configuration is missing; they do not substitute an in-memory database. Do not point tests at production. UI tests cover fetching, filters, search, errors and retry. Tests use one fork worker for reliable execution on this Windows environment.

For schema changes:

```powershell
dotnet ef migrations add YourChangeName --project backend/CampusEats.Api
dotnet ef database update --project backend/CampusEats.Api
```

Review the migration before applying it and commit the migration plus snapshot. `Database__ApplyMigrations` defaults to false. Setting it to true applies migrations on startup and only logs success after completion. That is suitable for a single-instance lab deployment; for larger deployments run migrations once as a controlled release step, with separate migration permissions and a backup.

## Docker verification

Start Docker Desktop's Linux engine first. Set `ConnectionStrings__Default` and optionally the JWT environment variables in the current shell. A host PostgreSQL database is reachable from Docker Desktop using `Host=host.docker.internal`, not container `localhost`. Neon uses its actual remote hostname.

```powershell
docker build -t campuseats-api:local backend
docker run --rm --name campuseats-api -p 8080:8080 -e ConnectionStrings__Default -e Jwt__Key -e Jwt__Issuer -e Jwt__Audience -e Database__ApplyMigrations=true campuseats-api:local
```

Stop a previously running local API before binding port 8080. The multistage build uses .NET 8 SDK and ASP.NET 8 runtime images, publishes `CampusEats.Api.dll`, and runs as the non-root `app` user. The API binds `0.0.0.0` using `PORT` (8080 fallback). The SDK and credentials are not copied into the runtime image. Never pass secrets as Docker build arguments.

## GitHub (manual, after your approval)

Review ignored files and source before staging. Never stage `.env`, `.local`, credentials, passwords, private keys, database dumps, or generated outputs. If credentials are ever exposed in history, rotate them; deleting a file does not remove its history.

Create your own empty GitHub repository in the GitHub UI, then run these commands after replacing the remote placeholder:

```powershell
git init
git branch -M main
git status --short --ignored
git add .gitignore README.md CampusEats.sln frontend backend .config .github netlify.toml render.yaml
git diff --cached --stat
git diff --cached
git commit -m "Build CampusEats full-stack Lab 11 application"
git remote add origin https://github.com/<YOUR_USERNAME>/<YOUR_REPOSITORY>.git
git push -u origin main
```

Use GitHub's normal authentication flow; never embed a token in the remote URL. GitHub Actions builds/tests both projects, migrates a temporary PostgreSQL 16 service, checks migration drift, and builds Docker. CI generates its JWT key for that run and masks it. The PostgreSQL CI service uses ephemeral trust authentication only within the disposable runner; use password/TLS authentication for normal databases. No cloud secrets are required. A local test pass is not a GitHub Actions run: capture CI evidence only after the workflow actually completes on GitHub.

## Neon production database (manual)

1. Create a Neon project, choose a suitable region, and create/select the production database and role. Keep development/test databases separate.
2. In Neon's connection dialog obtain the real hostname, database name, role and password. Use the direct connection hostname for migrations/startup in this small lab deployment. Do not paste the full connection into screenshots or GitHub.
3. Convert the connection details into **Npgsql keyword syntax** for `ConnectionStrings__Default`:

```text
Host=<ACTUAL_NEON_HOST>;Port=5432;Database=<DATABASE>;Username=<ROLE>;Password=<PASSWORD>;SSL Mode=VerifyFull;Timeout=30
```

Do not paste a `postgresql://...` URI as an Npgsql keyword string. TLS certificate/hostname verification is enabled with `VerifyFull`; do not turn it off to work around configuration mistakes. See [Neon connection guidance](https://neon.com/docs/connect/connect-from-any-app) and [Npgsql parameters](https://www.npgsql.org/doc/connection-string-parameters).

4. Store the connection only in Render's environment configuration (or a private local session if manually applying migrations).
5. Apply the migrations using either the startup option described below or `dotnet ef database update` from a trusted shell with the production connection. Confirm the actual migration log and `__EFMigrationsHistory` before claiming success.
6. In Neon SQL Editor, verify:

```sql
SELECT "MigrationId" FROM "__EFMigrationsHistory";
SELECT "Id", "Name", "Category", "Price" FROM "MenuItems" ORDER BY "Id";
```

## Render backend deployment (manual)

Create a **Web Service** from your GitHub repository. Choose Docker runtime, branch `main`, and these settings:

| Setting | Value |
|---|---|
| Root directory | `backend` |
| Dockerfile path | `./Dockerfile` relative to that root |
| Docker build context | `.` relative to that root |
| Docker command override | Leave empty; image entrypoint runs the API DLL |
| Health check path | `/health/ready` |

`render.yaml` provides an optional equivalent Blueprint; use the dashboard or Blueprint deliberately, not both to create duplicate services. Choose a service plan yourself and review the provider's current pricing. See [Render Docker](https://render.com/docs/docker) and [port binding](https://render.com/docs/web-services).

Configure runtime variables:

| Name | Value |
|---|---|
| `ConnectionStrings__Default` | Private Neon Npgsql string |
| `Jwt__Key` | Private randomly generated key of at least 32 bytes; optional if auth disabled |
| `Jwt__Issuer` | `CampusEats` |
| `Jwt__Audience` | `CampusEats` |
| `Cors__AllowedOrigins__0` | Your exact actual Netlify HTTPS origin, no trailing slash or path |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `PORT` | `8080` (the app also respects a platform-provided value) |
| `Database__ApplyMigrations` | `true` for the initial single-instance lab deployment |

Generate a key privately with `[Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))`; paste its output into the private Render setting, never into source or screenshots. Production frontend origin overrides the first local CORS entry; set `Cors__AllowedOrigins__1` to the same production origin to replace the second local default as well. To permit additional real origins use indexed entries; avoid `*`. Netlify deploy-preview origins require explicit configuration if you want them to access this API.

Deploy, inspect real build/startup logs, confirm migration success, and visit the actual Render `/health/ready` and `/api/menu` URLs. Do not claim deployment success just because the build finished. If readiness fails, check database details, TLS, migration history and runtime variables. If the service is asleep, allow it time to start and use the frontend retry control. For future deployments, leave startup migrations enabled for this single instance or set false and apply reviewed migrations separately.

## Netlify frontend deployment (manual)

Import your GitHub repository into Netlify; select branch `main`.

| Setting | Value |
|---|---|
| Repository root | Monorepo root |
| Base/root directory | `frontend` |
| Build command | `npm run build` |
| Publish directory | `dist` relative to `frontend` (repository path `frontend/dist`) |
| Node version | `22` |

The repository-root `netlify.toml` supplies these settings and the SPA rewrite. See [Netlify monorepos](https://docs.netlify.com/build/configure-builds/monorepos/).

Set `VITE_API_URL` to **your real Render HTTPS origin**, without `/api` or a trailing slash. It is public, not a secret. Build/deploy again when it changes, because [Vite replaces variables during the build](https://vite.dev/guide/env-and-mode). Missing this variable makes the deployed frontend attempt the visitor's localhost.

Once Netlify assigns your actual site origin, copy it into Render's CORS settings and redeploy/restart the API. This resolves the initial dependency between the frontend's URL and backend CORS. Verify the deployed page's Network panel shows a 200 response from the Render `/api/menu` endpoint. Test a category and search on mobile and desktop. Never invent a hosted URL in a report.

## Genuine Lab 11 evidence

The lab handout was not supplied, so confirm your exact Tasks 01–06 wording with it. This project supports the following evidence areas; these are not asserted to be the official task titles:

1. Local implementation: source structure, dashboard, menu request and local terminal builds.
2. Version control: your actual GitHub repository, commit and branch.
3. Frontend hosting: real Netlify build settings/logs and deployed dashboard URL.
4. Backend hosting: real Render Docker settings, build/startup logs and readiness/menu responses.
5. Managed database: Neon tables, eight menu records and migration history.
6. Integration/DevOps: successful GitHub Actions run, browser Network request, category filtering and responsive UI.

Capture screenshots yourself after each step succeeds. Include the browser URL where useful, the relevant terminal command/results, and captions explaining what each image proves. Capture the real dashboard at desktop and mobile widths; show an active category and resulting dishes. Capture the actual GitHub Actions green run, not this README. Hide connection strings, passwords, JWT keys/tokens, account details and credentials before capturing provider settings. A screenshot of a settings form alone is not proof of a working deployment.

Suggested evidence files (create only after capture): `docs/evidence/local-dashboard.png`, `frontend-build.png`, `backend-tests.png`, `github-ci.png`, `netlify-dashboard.png`, `render-readiness.png`, `neon-menu.png`, `production-network.png`. No screenshots or deployment URLs have been fabricated or supplied.

## Verification performed in this workspace

- Dependencies restored; frontend lockfile included.
- `npm run build` passed; `npm test` passed (2 tests); `npm audit` reported zero vulnerabilities after updating Vitest.
- `dotnet build CampusEats.sln -c Release --no-restore` passed with zero warnings/errors.
- `dotnet publish` passed, and the published `CampusEats.Api.dll` exists with the exact filename used by the Docker entrypoint.
- Initial migration applied to a temporary local PostgreSQL 16 database on loopback port 55439. Its migration history was queried and contained `20261012172355_InitialCreate`. No Neon migration has been run.
- EF reported no pending model changes; backend tests passed (4 tests, no skips) against PostgreSQL.
- Real HTTP checks returned eight menu items, ready status, and frontend HTTP 200.
- Docker engine was stopped, so the Docker image has **not** been built/run locally. `dotnet publish` is checked separately; CI contains a Docker build step for your future GitHub run.
- No GitHub workflow run, cloud deployment, hosted-browser check or screenshot capture has been claimed.

This machine's Windows sandbox blocked package network access, esbuild ancestor directory reads, and integration-test database access; those operations passed when rerun with approved host access. Ordinary local PowerShell does not require that sandbox workaround. The temporary test cluster and generated artifacts are ignored under `.local`, `bin`, `obj`, `node_modules` and `dist`.

## Troubleshooting

- **Startup says connection missing:** set `ConnectionStrings__Default` in the API shell/Render; a backend `.env` file is not auto-loaded.
- **Menu HTTP 500 or readiness 503:** verify the database is reachable and the migration is applied. Check server logs without publishing sensitive contents.
- **CORS failure:** origin must exactly match scheme, hostname and port. Changing CORS requires API restart; CORS does not prevent non-browser callers from accessing public menu data.
- **Frontend attempts localhost in production:** set Netlify `VITE_API_URL` and trigger a fresh build.
- **JWT startup error:** use a long random key plus issuer/audience, or leave key empty for menu-only operation.
- **Docker unavailable:** start Docker Desktop/Linux engine, then rerun the documented build/run commands.
- **Port occupied:** stop the process you started or change `PORT` and the frontend API origin together.
- **New migration not in container:** commit the new migration and redeploy the image; do not edit the live database ad hoc.
