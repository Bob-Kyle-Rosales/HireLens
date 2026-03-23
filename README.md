# HireLens

HireLens is a hiring workflow application that helps recruiters and hiring teams create job postings, upload resumes, analyze candidate profiles, and score candidate-to-job fit.

It combines:

- ASP.NET Core and server-side Blazor for the app and UI
- ASP.NET Core Identity for authentication and role-based access
- EF Core for persistence
- ML.NET for lightweight resume category classification
- Explainable scoring logic for candidate-job matching

The goal is to turn raw resumes into practical hiring signals that are faster to review and easier to explain.

## Project Description

HireLens is designed to support the early stages of recruitment where teams need to:

- collect job requirements
- receive candidate resumes
- extract readable resume text
- analyze candidate profiles
- rank candidates against specific roles

Instead of relying only on manual screening, HireLens adds a structured workflow with resume analysis, candidate categorization, and job-fit scoring.

## What The Project Does

HireLens currently supports:

- user authentication with `Admin` and `Recruiter` roles
- job posting management
- candidate resume upload for `.pdf` and `.txt` files
- automatic resume text extraction
- resume analysis with predicted category and extracted skills
- candidate-to-job scoring and ranking
- model version management, activation, and retraining
- recruiter/admin dashboards and protected pages
- middleware for correlation IDs, security headers, and global exception handling
- rate limiting on auth and admin-heavy endpoints
- seed data for demos
- automated integration tests

## Use Case

This project is useful when a team wants a more consistent and explainable way to shortlist candidates.

Typical use cases:

- a recruiter wants to quickly see who best fits a specific role
- a hiring team wants visibility into matched skills and missing skills
- an admin wants a simple internal ML workflow without a heavy external model stack
- a portfolio/demo project needs a complete recruiting workflow with auth, APIs, UI, ML, and scoring

## End-to-End Flow

1. A recruiter or admin signs in.
2. A job posting is created with a title, description, required skills, optional skills, and seniority level.
3. A candidate resume is uploaded for a specific job.
4. The system extracts text from the uploaded resume.
5. The system analyzes the resume:
   - extracts skills
   - predicts a candidate category such as `IT`, `Frontend`, `Data/ML`, or `HR`
6. The system scores the candidate against the selected job:
   - checks required skill coverage
   - checks optional skill coverage
   - measures textual similarity between the job and the resume
   - stores matched skills, missing skills, and overlapping keywords
7. The recruiter reviews the results in the dashboard and related pages.
8. An admin can retrain models, activate a model version, and reanalyze candidates.

## Machine Learning And Scoring

HireLens uses two different approaches:

- ML for `resume category prediction`
- explainable scoring for `candidate-job matching`

### 1. Resume Category Prediction

The project uses `ML.NET` for classifying resume text into categories.

Current pipeline:

- `FeaturizeText` converts raw resume text into a numeric feature vector
- `SdcaMaximumEntropy` trains a multiclass classifier on those feature vectors
- the classifier predicts categories such as `IT`, `Frontend`, `Data/ML`, and `HR`

Why this approach fits the project:

- lightweight and easy to run inside a .NET app
- simple to retrain
- practical for small internal datasets
- more explainable and easier to maintain than a heavier ML stack

Current training data source:

- stored candidate `ResumeText`
- stored category labels from resume analyses in the application database

Fallback behavior:

- if no trained model is available, the app falls back to keyword-based category heuristics

### 2. Candidate-To-Job Matching

The job-matching side is not a large black-box ML model. It is an explainable ranking system based on:

- `TF-IDF`
- `cosine similarity`
- required skill coverage
- optional skill coverage
- penalty for missing required skills

Current scoring formula:

```text
score = clamp(
  (cosineSimilarity * 60)
  + (requiredCoverage * 30)
  + (optionalCoverage * 10)
  - (missingRatio * 15),
  0,
  100
)
```

### 3. Resume Noise Reduction Improvement

The matcher was improved to avoid blindly comparing the entire resume body when that would introduce noise.

The current section-focused scoring:

- prioritizes `Skills`
- prioritizes `Experience`
- prioritizes `Projects`
- includes `Summary` as a lower-signal support section
- ignores noisier sections such as `Interests`, `References`, `Education`, and similar headings when structured sections are available
- falls back to full resume text only when the resume does not contain usable structure

This makes the ranking more stable and reduces false positives from irrelevant resume text.

## Why The Project Has Value

HireLens adds value by making hiring workflows:

- faster, because recruiters do not need to read every resume from scratch
- more consistent, because candidates are scored against the same requirements
- more explainable, because the system shows matched skills, missing skills, and keyword overlap
- more actionable, because jobs, candidates, analyses, applications, and scores are tied together in one workflow
- easier to demo, extend, and discuss as a practical software and ML project

## Stack

- Backend: ASP.NET Core (`net8.0`)
- UI: server-side Blazor + MudBlazor
- Data: EF Core + SQL Server/PostgreSQL
- Auth: ASP.NET Core Identity
- ML: ML.NET
- File handling: `.pdf` and `.txt` resume extraction
- Architecture: `Domain -> Application -> Infrastructure -> Web`

## Project Structure

- `src/HireLens.Domain` - core entities and enums
- `src/HireLens.Application` - DTOs and service contracts
- `src/HireLens.Infrastructure` - EF Core, Identity, helpers, and service implementations
- `src/HireLens.Web` - web host, controllers, middleware, Blazor pages
- `tests/HireLens.Web.IntegrationTests` - integration tests

## Configuration

Main settings are in `src/HireLens.Web/appsettings.json`.

Key settings:

- `Database:Provider`: `SqlServer` or `PostgreSql`
- `ConnectionStrings:DefaultConnection`: database connection string
- `SeedAdmin:Email` / `SeedAdmin:Password`: optional initial admin account
- `SeedData:Enabled`: seed demo jobs, candidates, analyses, matches, and applications
- `ML:ModelDirectory`: local directory for saved ML.NET model files
- `ML:Training:MinLabeledResumes`: minimum labeled resumes required for training
- `ML:Training:MinDistinctCategories`: minimum category count required for training

## Migrations

Current migrations are under:

- `src/HireLens.Infrastructure/Persistence/Migrations`

Apply or update the database with:

```powershell
dotnet tool run dotnet-ef database update `
  --project src/HireLens.Infrastructure/HireLens.Infrastructure.csproj `
  --startup-project src/HireLens.Web/HireLens.Web.csproj `
  --context HireLensDbContext
```

## Run Locally

Build and run:

```powershell
dotnet build HireLens.sln
dotnet run --project src/HireLens.Web/HireLens.Web.csproj
```

## Run With Docker

Prerequisites:

- Docker Desktop
- .NET SDK 8

Steps:

1. Create a local env file:

```powershell
copy .env.example .env
```

2. Set a strong `MSSQL_SA_PASSWORD` in `.env`.

3. Build and run:

```powershell
docker compose up --build
```

4. Open:

- App: `http://localhost:8080`
- Swagger (Development): `http://localhost:8080/swagger`
- SQL Server from host: `localhost,14333`

### Docker Dev Hot Reload

Use the dev override file to run `dotnet watch` with a bind-mounted source tree:

```powershell
docker compose -f docker-compose.yml -f docker-compose.dev.yml up --build
```

In this mode:

- source changes are detected automatically
- rebuilds are usually not needed for normal code changes
- rebuild/restart is still needed when Dockerfile, compose, or env settings change significantly

## Tests

Run integration tests:

```powershell
dotnet test tests/HireLens.Web.IntegrationTests/HireLens.Web.IntegrationTests.csproj
```

Run the ML API smoke test against a running app:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/test-ml-api.ps1
```

## CI

GitHub Actions CI runs restore, build, and integration tests on push and pull request.

## Current Status

HireLens is in a practical MVP state with:

- authenticated recruiter/admin workflows
- dashboard and management pages
- resume upload and analysis
- explainable candidate-job scoring
- lightweight internal ML classification
- seeded demo data and automated test coverage

It is a strong foundation for expanding into richer resume parsing, stronger labeling pipelines, better model training data, and more advanced ranking logic.
