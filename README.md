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
- Jobs REST API (`/api/jobs`)
- Candidates REST API (`/api/candidates`, `/api/candidates/upload`)
- Resume text extraction for `.pdf` and `.txt`
- Global exception middleware
- Automatic migration apply on startup + role seeding

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

Stop containers:

```powershell
docker compose down
```

Stop and remove DB volume:

```powershell
docker compose down -v
```

## Next Phases

- Add ML.NET analysis and matching engine
- Add UI layer after backend and ML APIs are finalized
