# HireLens (Backend Phase)

HireLens is currently in a backend-first phase built on .NET 8 with REST APIs, Identity, and EF Core.

## Stack

- Backend: ASP.NET Core (`net8.0`)
- Data: EF Core + SQL Server/PostgreSQL
- Auth: ASP.NET Core Identity + roles (`Admin`, `Recruiter`)
- Architecture: layered (`Domain` -> `Application` -> `Infrastructure` -> `Web API`)

## Current Scope

- Identity-based authentication endpoints at `/api/auth/*`
- Role policies (`RecruiterOrAdmin`, `AdminOnly`)
- Auth and admin endpoint rate limiting
- Jobs REST API (`/api/jobs`)
- Candidates REST API (`/api/candidates`, `/api/candidates/upload`) with job-targeted applications
- Resume analysis API (`/api/analyses/*`)
- Matching API (`/api/matches/*`)
- Model management and training API (`/api/models/*`)
- Resume text extraction for `.pdf` and `.txt`
- Job application tracking (`Submitted` -> `Analyzed` -> `Scored` / `Failed`)
- Correlation ID middleware (`X-Correlation-ID`)
- Global exception middleware with `ProblemDetails` JSON for API errors
- Automatic migration apply on startup + role seeding
- Automated integration tests (`tests/HireLens.Web.IntegrationTests`)
- CI workflow (`.github/workflows/ci.yml`)

## Project Structure

- `src/HireLens.Domain` - core entities and enums
- `src/HireLens.Application` - DTOs and service contracts
- `src/HireLens.Infrastructure` - EF Core, Identity store, service implementations
- `src/HireLens.Web` - API host, controllers, middleware

## Configuration

Edit `src/HireLens.Web/appsettings.json`:

- `Database:Provider`: `SqlServer` (default) or `PostgreSql`
- `ConnectionStrings:DefaultConnection`: database connection string
- `SeedAdmin:Email` / `SeedAdmin:Password`: optional initial admin account
- `SeedData:Enabled`: seed demo jobs/candidates/analyses/matches on startup when DB is empty
- `ML:ModelDirectory`: local directory used for persisted ML.NET model files
- `ML:Training:MinLabeledResumes`: minimum labeled resumes required for training
- `ML:Training:MinDistinctCategories`: minimum category count required for training

## Migrations

Current migration is under:

- `src/HireLens.Infrastructure/Persistence/Migrations`

Apply/update database:

```powershell
dotnet tool run dotnet-ef database update `
  --project src/HireLens.Infrastructure/HireLens.Infrastructure.csproj `
  --startup-project src/HireLens.Web/HireLens.Web.csproj `
  --context HireLensDbContext
```

## Run

```powershell
dotnet build HireLens.sln
dotnet run --project src/HireLens.Web/HireLens.Web.csproj
```

## Run With Docker

Prerequisites:

- Docker Desktop
- .NET SDK 8 (`dotnet --list-sdks` should include an `8.0.x` entry)

Steps:

1. Create a local env file from the template:

```powershell
copy .env.example .env
```

2. Set a strong `MSSQL_SA_PASSWORD` in `.env` (SQL Server requires password complexity).

3. Build and run:

```powershell
docker compose up --build
```

4. Open:

- App: `http://localhost:8080`
- Swagger (Development): `http://localhost:8080/swagger`
- SQL Server from host: `localhost,14333`

## Tests

Run integration tests:

```powershell
dotnet test tests/HireLens.Web.IntegrationTests/HireLens.Web.IntegrationTests.csproj
```

Run ML API smoke tests against a running app:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/test-ml-api.ps1
```

Stop containers:

```powershell
docker compose down
```

Stop and remove DB volume:

```powershell
docker compose down -v
```

## CI

GitHub Actions CI runs restore, build, and integration tests on push/PR.
