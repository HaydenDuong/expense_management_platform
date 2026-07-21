# Milestone 1 - Backend Foundation

## Goal
Create a runnable ASP.NET Core backend foundation with:
    - A clean project structure.
    - Database connectivity.
    - Consistent API behavior.
    - Local development support.

## Scope
This milestone does not implement business features yet. It establishes the technical base that future modules will build on.

## Learning Objectives
- ASP.NET Core Web API Structure.
- Dependency Injection.
- EF Core with PostgreSQL.
- Configuration by environment.
- Global error handling.
- Structured logging.
- Docker-based local development

## Architecture Decisions
- Use a modular monolith as the initial architecture.
- Separate API, application, domain, and infrastructure concerns.
- Use PostgreSQL as the primary relational database.
- Use EF Core migrations for schema changes.
- Use Serilog for structured logging.
- Use Problem Details for standardized API errors.

## Tasks
Project Setup
- [x] Create ASP.NET Core Web API project
- [x] Define solution/project structure
- [x] Add environment-based configuration
- [x] Add dependency injection conventions      [Foundation skeleton for later stages as well]

Database
- [x] Add PostgreSQL connection
- [x] Configure EF Core DbContext class
- [ ] Create initial migration [Delay to Stage 2]
- [x] Add database health check

API Foundation
- [x] Add Swagger/OpenAPI
- [x] Add global exception middleware (app.UseExceptionHandler() is a global exception middleware)
- [x] Add Problem Details responses
- [ ] Add request validation foundation [Delay until first request DTO in Auth module]
- [ ] Add API versioning  [Delay until business endpoints exist]

Observability
- [x] Add Serilog
- [x] Add console logging
- [ ] Add correlation/request ID logging    [Delay to later stages]
- [x] Expose /health endpoint

Local Development
- [ ] Add Dockerfile [Delay until deployment / containerized API stage]
- [x] Add docker-compose for PostgreSQL
- [x] Add README setup instructions

## Definition of Done:
- API starts locally.
- PostgreSQL runs through Docker.
- EF Core can apply migrations.
- Swagger is available.
- Health check confirms database connectivity.
- Unhandled exceptions return consistent Problem Details responses.

## Notes:
A. Project Setup
- Create a new ASP.NET Core Web API Project with: 
    dotnet new webapi -n expense_management_app --framework net10.0

- Fixed "warning NU1903" by:
    In "expense_management_app.csproj":
        Navigate this line: <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="10.0.8" />
        And add this line below the it: <PackageReference Include="Microsoft.OpenApi" Version="2.7.5" />

- In "appsettings.Development.json": added "ConnectionStrings" to allow the local machine to connect to Docker compose PostgreSQL container

    "appsettings.json": this file should hold settings that are safe and common across environments.

    "appsettings.Development.json": contains local development settings, this is where local Docker PostgreSQL connection string belongs.

    In "Properties/launchSettings.json": "ASPNETCORE_ENVIRONMENT" is set to "Development" which means that ASP.NET Core automatically loads the following two files in the exact order:
        "appsettings.json"
        "appsettings.Development.json" ("Development" will overriding shared config)

- Created ".env", ".env.example", and ".gitignore"

- Created "expense_management_app/Infrastructure/DependencyInjection.cs" and move both "services.AddDbContext<>();" & "services.AddHealthChecks().AddDbContext<AppDbContext>();" to it
    Infrastructure DI.cs file should be used to group services that are related to: database, storage, messaging, external services, file systems, cloud providers
    
    Other services like: OpenAPI, Problem Details, endpoint behavior, HTTP concerns are belong to API foundation (HTTP presentation), thus, it should belong to a separate DI.cs file for API

B. Database
- Install Nuget packages for PostGreSQL:
    dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
    dotnet add package Microsoft.EntityFrameworkCore.Design

- In "appsettings.json" (currently this has been moved to "appsettings.Development.json):
    Under "AllowedHosts": "*" :
        added "ConnectionStrings": {
                "Postgres": "Host=localhost;Port=5435;Database=expenseManagementDB;Username=postgres;Password=postgres"
            }

- In "Infrastructure/Persistence": created an empty "AppDbContext.cs" and registered it in "Program.cs"

- Added a PostgreSQL health check - this proves this API can actually reach the DB
    Installed: dotnet add package Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore

    In "Program.cs":
        added: builder.Services.AddHealthCheck().AddDbContextCheck<AppDbContext>();

        added: app.MapHealthChecks("/health");

        verify this Health Endpoint: 
            dotnet run --launch-profile https
            ("dotnet run" alone = run this application with http launch profile only, https is not included)

            visit: https://localhost:7273/health or
                   http://localhost:5089/health
                   (The port numbers are defined in /Properties/launchSettings.json)

C. API Foundation
- Problem Details = a standard JSON shape for API errors. Instead of returning random error trings, the API returns predictable responses like:
    {
        "type": "https://tools.ietf.org/html/rfc9110#section-15.6.1",
        "title": "An error occured while processing your request.",
        "status": 500,
        "traceId": "00-..."
    }
    (This helps clients, frontend apps, logs, ...)

    In ASP.NET Core, there are 2 main pieces:

        builder.Services.AddProblemDetails(); - This registers the service that knows how to format error. Morelike, teaches ASP.NET Core how to format error responses

        app.UseExceptionHandler(); - This catches unhandled exceptions globally & turns them into Problem Details responses
    
    In "Program.cs", added those two pieces and a temporary endpoint for testing it
        Added:
            "builder.Services.AddProblemDetails();"
            "app.UseExceptionHandler();"
            "app.MapGet("/throw",...);"
                // Output from tested with: https://localhost:7273/throw
                    // {"type":"https://tools.ietf.org/html/rfc9110#section-15.6.1",
                    // "title":"An error occurred while processing your request.",
                    // "status":500,
                    // "traceId":"00-5918d876a53dc83c225bfcfcd78ae7a8-fea1dfb9e7a4c946-00"}

        Tested with either:
            https://localhost:7273/throw
            http://localhost:5089/throw


D. Observability
- Default ASP.NET Core logging is fine, however, Serilog provides a better structured logs

    Installed:
        dotnet add package Serilog.AspNetCore       - integrates Serilog with ASP.NET Core

        dotnet add package Serilog.Sinks.Console    - lets Serilog write logs to te console
    
    In "Program.cs": added:
        "builder.Host.UseSerilog(...);"
        "app.UseSerilogRequestLogging();"
        "Log.Information(...);" for testing Serilog (startup log)
            // Output from starting the application and other HTTP request
                // PS C:\Users\Hayden Duong\Desktop\learning_projects\expense_management_platform\expense_management_app> dotnet run --launch-profile https       
                // Using launch settings from C:\Users\Hayden Duong\Desktop\learning_projects\expense_management_platform\expense_management_app\Properties\launchSettings.json...
                // Building...
                // [13:11:59 INF] Starting Expense Management API in Development environment
                // [13:11:59 INF] Now listening on: https://localhost:7273
                // [13:11:59 INF] Now listening on: http://localhost:5089
                // [13:11:59 INF] Application started. Press Ctrl+C to shut down.
                // [13:11:59 INF] Hosting environment: Development
                // [13:11:59 INF] Content root path: C:\Users\Hayden Duong\Desktop\learning_projects\expense_management_platform\expense_management_app
                // [13:18:02 INF] Request starting HTTP/2 GET https://localhost:7273/favicon.ico - null null
                // [13:18:02 INF] HTTP GET /favicon.ico responded 404 in 2.1487 ms
                // [13:18:03 INF] Request finished HTTP/2 GET https://localhost:7273/favicon.ico - 404 0 null 100.1306ms
                // [13:18:03 INF] Request reached the end of the middleware pipeline without being handled by application code. Request path: GET https://localhost:7273/favicon.ico, Response status code: 404
                // [13:19:42 INF] Request starting HTTP/2 GET https://localhost:7273/health - null null
                // [13:19:42 INF] Executing endpoint 'Health checks'
                // [13:19:47 ERR] Health check AppDbContext with status Unhealthy completed after 4383.7164ms with message 'null'
                // [13:19:47 INF] Executed endpoint 'Health checks'
                // [13:19:47 ERR] HTTP GET /health responded 503 in 4565.8310 ms
                // [13:19:47 INF] Request finished HTTP/2 GET https://localhost:7273/health - 503 null text/plain 4640.4027ms
                // [13:20:01 INF] Request starting HTTP/2 GET https://localhost:7273/health - null null
                // [13:20:01 INF] Executing endpoint 'Health checks'
                // [13:20:02 INF] Executed DbCommand (36ms) [Parameters=[], CommandType='Text', CommandTimeout='30']
                // SELECT 1
                // [13:20:02 INF] Executed endpoint 'Health checks'
                // [13:20:02 INF] HTTP GET /health responded 200 in 265.7448 ms
                // [13:20:02 INF] Request finished HTTP/2 GET https://localhost:7273/health - 200 null text/plain 272.6994ms

E. Local Development
- In "learning_projects/expense_management_platform/": added docker-compose.yml
    Created PostgreSQL container with: docker compose up -d
    Check container's info: docker ps
    Ran container health check:
        cd expense_management_app
        dotnet run
        Visit this url link with browser: http://localhost:5089/health
                                    or    https://localhost:7273/health
