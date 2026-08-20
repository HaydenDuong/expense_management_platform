# Milestone 1 Engineering Handoff - Backend Foundation

## Claim Classification Key

- VERIFIED: Supported by repository files, commit history, or milestone notes.
- PARTIAL: Implemented or discussed in limited form, but incomplete or intentionally scoped down.
- PLANNED: Explicitly deferred or intended for a later milestone.
- UNVERIFIED: Mentioned, but not confirmed from repository evidence available during this handoff.

## Goal of the Milestone

- VERIFIED: Establish a runnable ASP.NET Core backend foundation with clean initial structure, PostgreSQL connectivity, consistent API error behavior, structured logging, and Docker-based local database support.
- VERIFIED: The milestone scope explicitly excludes business features and focuses on technical foundation for later modules.

## Features Actually Completed

- VERIFIED: Created an ASP.NET Core Web API project targeting `net10.0`.
- VERIFIED: Added EF Core PostgreSQL support using `Npgsql.EntityFrameworkCore.PostgreSQL`.
- VERIFIED: Added an `AppDbContext` under `Infrastructure/Persistence`.
- VERIFIED: Registered infrastructure services through an extension method: `builder.Services.AddInfrastructure(builder.Configuration)`.
- VERIFIED: Configured PostgreSQL connection string in `appsettings.Development.json`.
- VERIFIED: Kept shared/default settings in `appsettings.json`.
- VERIFIED: Added Docker Compose support for PostgreSQL using `docker-compose.yml`.
- VERIFIED: Added `.env.example` for Docker Compose variables and `.gitignore` entry to exclude local `.env`.
- VERIFIED: Added EF Core database health check registration through `AddDbContextCheck<AppDbContext>()`.
- VERIFIED: Exposed `/health` endpoint through `app.MapHealthChecks("/health")`.
- VERIFIED: Added OpenAPI JSON support through `builder.Services.AddOpenApi()` and `app.MapOpenApi()` in Development.
- VERIFIED: Added Problem Details service registration through `builder.Services.AddProblemDetails()`.
- VERIFIED: Added global exception handling through `app.UseExceptionHandler()`.
- VERIFIED: Added Serilog and console logging packages.
- VERIFIED: Configured Serilog as the host logger with configuration reading, log-context enrichment, and console output.
- VERIFIED: Added Serilog request logging through `app.UseSerilogRequestLogging()`.

## Features Partially Completed

- PARTIAL: OpenAPI is available as JSON (`/openapi/v1.json`), but no Swagger UI was added during this milestone.
- PARTIAL: Dependency injection conventions were introduced for infrastructure services only. Separate API/application/module registration conventions were discussed but not implemented in Milestone 1.
- PARTIAL: Problem Details was validated using a temporary `/throw` endpoint, but broader exception mapping for domain/application errors was not implemented.
- PARTIAL: Docker-based local development was implemented for PostgreSQL only. The API itself was not containerized in Milestone 1.
- PARTIAL: EF Core was configured, but no meaningful initial migration was created during Milestone 1 because no domain model existed yet.

## Architecture and Design Decisions Made

- VERIFIED: The project uses a modular monolith direction rather than starting with distributed services.
- VERIFIED: Infrastructure concerns were separated into `Infrastructure/DependencyInjection.cs` and `Infrastructure/Persistence/AppDbContext.cs`.
- VERIFIED: Database access was placed under infrastructure because EF Core/PostgreSQL are concrete persistence concerns.
- VERIFIED: API host concerns such as OpenAPI and Problem Details remained in `Program.cs` instead of being placed in infrastructure.
- VERIFIED: PostgreSQL was selected as the primary relational database.
- VERIFIED: EF Core was selected for ORM/database access.
- VERIFIED: Serilog was selected for structured logging.
- VERIFIED: Problem Details was selected for standardized API error responses.
- VERIFIED: Docker Compose was used for local PostgreSQL instead of requiring a manually installed local database.
- VERIFIED: Local connection string configuration was moved to `appsettings.Development.json` to separate development-specific settings from shared settings.
- VERIFIED: `.env` was used for local Docker Compose variables, with `.env.example` committed as documentation.

## Alternatives Considered and Rejected

- VERIFIED: Creating business data models during Milestone 1 was discussed and rejected. The milestone kept `AppDbContext` initially empty because business entities belonged to later milestones.
- VERIFIED: Creating an initial EF migration during Milestone 1 was discussed and deferred until Milestone 2, when real identity entities would exist.
- VERIFIED: Putting `AddOpenApi()` and `AddProblemDetails()` inside `Infrastructure/DependencyInjection.cs` was discussed and rejected because they are API/HTTP concerns, not infrastructure concerns.
- VERIFIED: `Microsoft.Extensions.Diagnostics.HealthChecks.ResourceUtilization` was considered by the user as a possible health-check package but rejected for this milestone because the required check was database connectivity, not CPU/memory/resource utilization.
- VERIFIED: The API Dockerfile was deferred because local database containerization was enough for the current foundation milestone.

## Problems or Bugs Encountered

- VERIFIED: `AddDbContext` was initially registered after `builder.Build()`, which was identified as incorrect because service registrations must happen before the DI container is built.
- VERIFIED: The health-check EF package was initially mistyped as `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameCore`; the correct package is `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore`.
- VERIFIED: Visiting `https://localhost:5089/health` produced a browser SSL/protocol error because port `5089` was configured for HTTP, while HTTPS was configured on port `7273`.
- VERIFIED: `.env`, `.env.example`, and `.gitignore` were initially placed under the API project folder while `docker-compose.yml` was at the repository root. Docker Compose expects the default `.env` beside the compose file.
- VERIFIED: `.env.example` initially had an incorrect PostgreSQL container port value (`5342` instead of `5432`).
- VERIFIED: Health check returned unhealthy while PostgreSQL was not reachable, then healthy after Docker PostgreSQL was running.
- VERIFIED: NuGet restore/build initially failed in the sandbox due to blocked network access. Build succeeded after escalated network access was approved.
- VERIFIED: The NU1903 warning was addressed by adding an explicit `Microsoft.OpenApi` package reference according to milestone notes.

## Debugging and Resolution Approach

- VERIFIED: Inspected `Program.cs`, project files, appsettings, launch settings, Docker Compose, and milestone notes to align implementation with milestone scope.
- VERIFIED: Fixed DI registration ordering by moving service registration before `builder.Build()`.
- VERIFIED: Corrected the EF Core health-check package name.
- VERIFIED: Mapped HTTP/HTTPS URLs to the ports defined in `Properties/launchSettings.json`.
- VERIFIED: Moved Docker environment files to the repository root so Docker Compose could consume them by default.
- VERIFIED: Corrected PostgreSQL container port to `5432` and kept host port `5435`.
- VERIFIED: Used `/health` to validate API-to-PostgreSQL connectivity.
- VERIFIED: Used `/openapi/v1.json` to validate OpenAPI output.
- VERIFIED: Used a temporary `/throw` endpoint to validate Problem Details behavior.
- VERIFIED: Used Serilog console output to confirm startup and request logging.

## Technical Concepts Learned or Demonstrated

- VERIFIED: ASP.NET Core application startup flow: register services before `builder.Build()`, then configure middleware/endpoints.
- VERIFIED: Dependency injection registration using `IServiceCollection`.
- VERIFIED: Extension-method based DI convention for grouping infrastructure registrations.
- VERIFIED: Environment-based configuration with `appsettings.json`, `appsettings.Development.json`, and `ASPNETCORE_ENVIRONMENT`.
- VERIFIED: Docker Compose environment variable usage with `.env` and `.env.example`.
- VERIFIED: PostgreSQL container port versus host port mapping.
- VERIFIED: EF Core `DbContext` registration and PostgreSQL provider configuration.
- VERIFIED: Health checks for infrastructure dependency validation.
- VERIFIED: OpenAPI JSON generation in ASP.NET Core.
- VERIFIED: Problem Details as standardized API error response format.
- VERIFIED: Built-in global exception handling through `UseExceptionHandler`.
- VERIFIED: Structured logging concepts with Serilog message templates and request logging.
- VERIFIED: Local HTTP versus HTTPS launch profiles and port/protocol matching.

## Code and Components Implemented by the User

- VERIFIED: Created the ASP.NET Core Web API project.
- VERIFIED: Added NuGet package references for OpenAPI, EF Core/PostgreSQL, health checks, and Serilog.
- VERIFIED: Created `Infrastructure/Persistence/AppDbContext.cs`.
- VERIFIED: Created `Infrastructure/DependencyInjection.cs`.
- VERIFIED: Updated `Program.cs` to register OpenAPI, infrastructure services, Problem Details, exception handling, Serilog, request logging, `/health`, and development OpenAPI mapping.
- VERIFIED: Created `docker-compose.yml` for PostgreSQL.
- VERIFIED: Created `.env.example` and `.gitignore`.
- VERIFIED: Moved local connection string configuration into `appsettings.Development.json`.
- VERIFIED: Maintained `documents/milestone_1_note.md` with learning notes and verification examples.

## Areas Where Codex Provided Substantial Guidance

- VERIFIED: Suggested keeping `AppDbContext` empty during Milestone 1 instead of designing business entities prematurely.
- VERIFIED: Recommended placing `AppDbContext` under `Infrastructure/Persistence`.
- VERIFIED: Identified that `AddDbContext` must be registered before `builder.Build()`.
- VERIFIED: Recommended using Docker Compose for PostgreSQL before validating `/health`.
- VERIFIED: Explained HTTP versus HTTPS launch ports and the `ERR_SSL_PROTOCOL_ERROR`.
- VERIFIED: Explained how to separate shared and development configuration.
- VERIFIED: Explained `.env`, `.env.example`, `.gitignore`, and Docker Compose file placement.
- VERIFIED: Explained Problem Details and the relationship between `AddProblemDetails()` and `UseExceptionHandler()`.
- VERIFIED: Explained Serilog setup and request logging.
- VERIFIED: Proposed the infrastructure DI extension method pattern.
- VERIFIED: Helped classify unfinished Milestone 1 tasks as current scope versus intentionally delayed.

## Generated or Designed Material the User Should Not Overclaim

- VERIFIED: Codex generated or substantially drafted wording for Milestone 1 structure and rewrite guidance in earlier discussion.
- VERIFIED: Codex generated example comments for health checks, Problem Details, and Serilog.
- VERIFIED: Codex generated the design suggestion for `Infrastructure/DependencyInjection.cs` and `AddInfrastructure(...)`.
- VERIFIED: Codex generated the Docker Compose shape and `.env.example` variable structure guidance.
- VERIFIED: Codex generated guidance for classifying roadmap items as complete, partial, or delayed.
- VERIFIED: Codex generated this handoff document.

## Tests, Validation, Security, and Error Handling Added

- VERIFIED: Manual validation of `/health` confirmed database connectivity after PostgreSQL was running.
- VERIFIED: Manual validation of `/openapi/v1.json` confirmed OpenAPI endpoint availability.
- VERIFIED: Manual validation of `/throw` confirmed unhandled exceptions were converted to Problem Details responses.
- VERIFIED: Serilog output confirmed startup logging and request logging.
- VERIFIED: `.gitignore` excludes `.env`, reducing risk of committing local secrets.
- PARTIAL: No automated unit or integration tests were added during Milestone 1.
- PARTIAL: No formal security hardening was implemented beyond excluding `.env` and separating local config.

## Refactors and Why They Happened

- VERIFIED: Moved EF Core and health-check registration out of `Program.cs` into `Infrastructure/DependencyInjection.cs` to establish a DI convention and reduce startup-file growth.
- VERIFIED: Moved PostgreSQL connection string from `appsettings.json` to `appsettings.Development.json` to separate local development configuration from shared defaults.
- VERIFIED: Moved Docker environment files to the repository root to match Docker Compose default `.env` behavior.

## Remaining Technical Debt or Unfinished Work

- VERIFIED: Temporary `/throw` endpoint still exists in current `Program.cs`; it should be removed after Problem Details validation.
- PLANNED: Initial EF Core migration was intentionally delayed until real entities were introduced in Milestone 2.
- PLANNED: Request validation foundation was intentionally delayed until the first request DTOs in the Auth module.
- PLANNED: API versioning was intentionally delayed until business endpoints existed.
- PLANNED: Dockerfile/API containerization was intentionally delayed until deployment or full containerized local development.
- PLANNED: Correlation/request ID logging was delayed to a later observability stage.
- PARTIAL: Swagger/OpenAPI JSON exists, but Swagger UI was not added.
- PARTIAL: Serilog is configured for console logging only; no file sink, centralized log storage, tracing integration, or production logging policy was added.
- PARTIAL: Current repository has later milestone changes in the same files, so Milestone 1 evidence should be read from commits `ee58dc0` and `14c3e57` where possible.

## Evidence

- VERIFIED: Milestone 1 primary commit: `ee58dc0` - `(Completed) Milestone 1 - Backend Foundation`.
- VERIFIED: Milestone 1 README update commit: `14c3e57` - `(Completed) Milestone 1: Updated README.md`.
- VERIFIED: Milestone note: `documents/milestone_1_note.md`.
- VERIFIED: API startup/config file: `expense_management_app/Program.cs`.
- VERIFIED: Infrastructure DI file: `expense_management_app/Infrastructure/DependencyInjection.cs`.
- VERIFIED: EF Core context: `expense_management_app/Infrastructure/Persistence/AppDbContext.cs`.
- VERIFIED: Project/package file: `expense_management_app/expense_management_app.csproj`.
- VERIFIED: Shared config: `expense_management_app/appsettings.json`.
- VERIFIED: Development config: `expense_management_app/appsettings.Development.json`.
- VERIFIED: Launch profiles/ports: `expense_management_app/Properties/launchSettings.json`.
- VERIFIED: Docker Compose config: `docker-compose.yml`.
- VERIFIED: Docker variable example: `.env.example`.
- VERIFIED: Ignore rules: `.gitignore`.
- UNVERIFIED: No automated test output file or CI result for Milestone 1 was found in the repository during this handoff.
