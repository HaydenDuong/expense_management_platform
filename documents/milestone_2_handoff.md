# Milestone 2 Engineering Handoff - Identity and Authentication

## Claim Classification Key

- VERIFIED: Established from repository files, build output, migrations, or committed history.
- PARTIAL: Supported by repository evidence and/or manual run reports, but not fully covered by automated tests or independently reproduced in this handoff.
- PLANNED: Identified as intended or remaining work.
- UNVERIFIED: Discussed or claimed, but not established from repository evidence available in this audit.

## Goal of the Milestone

- VERIFIED: Milestone 2 goal was to allow users to create accounts, authenticate, refresh sessions, and access protected API endpoints. Evidence: `documents/milestone_2_note.md`.
- VERIFIED: Milestone scope introduced identity/authentication only, excluding profile management, non-basic roles, and email delivery unless optional. Evidence: `documents/milestone_2_note.md`.

## Features Actually Completed

- VERIFIED: Added identity data model classes:
  - `expense_management_app/Models/Identity/AppUser.cs`
  - `expense_management_app/Models/Identity/RefreshToken.cs`
- VERIFIED: Added auth request/response contracts:
  - `RegisterRequest`
  - `LoginRequest`
  - `RefreshRequest`
  - `LogoutRequest`
  - `AuthResponse`
  - `AuthUserResponse`
- VERIFIED: Added `AuthController` endpoints:
  - `POST /auth/register`
  - `POST /auth/login`
  - `POST /auth/refresh`
  - `POST /auth/logout`
  - `GET /auth/me`
  Evidence: `expense_management_app/Controllers/AuthController.cs`.
- VERIFIED: Login returns an access token, refresh token, and user response DTO. Evidence: `AuthController.Login`, `AuthResponse`.
- VERIFIED: Refresh token rotation is implemented by revoking the existing stored refresh token and creating a new refresh token row. Evidence: `AuthController.Refresh`.
- VERIFIED: Logout revokes the submitted active refresh token without issuing a new token. Evidence: `AuthController.Logout`.
- VERIFIED: `GET /auth/me` is protected with `[Authorize]` and reads the authenticated user's `sub` claim before loading the current user from PostgreSQL. Evidence: `AuthController.Me`.
- VERIFIED: JWT bearer authentication is registered and configured with issuer, audience, lifetime, signing-key validation, and zero clock skew. Evidence: `Infrastructure/DependencyInjection.cs`.
- VERIFIED: Authentication and authorization middleware are wired into the ASP.NET Core pipeline. Evidence: `Program.cs`.
- VERIFIED: The project builds successfully after package restore. Evidence: `dotnet build .\expense_management_app\expense_management_app.csproj` completed with 0 warnings and 0 errors during this handoff.

## Features Partially Completed

- PARTIAL: Manual endpoint validation was reported for registration, duplicate registration, login, refresh-token rotation, logout revocation, and `/auth/me`. These were reported during the milestone discussion, but no automated test project or automated test run is present in the repository.
- PARTIAL: Auth request validation is present via DataAnnotations, but validation behavior is not covered by automated tests. Evidence: contracts under `expense_management_app/Contracts/Auth`.
- PARTIAL: JWT access-token expiry behavior was discussed and manually testable by reducing `AccessTokenMinutes`, but no automated expiry test exists.

## Architecture and Design Decisions

- VERIFIED: Identity entities were placed under `Models/Identity`, separated from later expense and receipt model folders.
- VERIFIED: API request/response models were separated from EF entities under `Contracts/Auth`.
- VERIFIED: JWT generation was placed behind `IJwtTokenService` / `JwtTokenService` instead of embedding signing logic directly in `AuthController`.
- VERIFIED: Refresh token generation and hashing were placed behind `IRefreshTokenService` / `RefreshTokenService`.
- VERIFIED: JWT config uses typed options via `JwtOptions` and `services.Configure<JwtOptions>(configuration.GetSection("Jwt"))`.
- VERIFIED: Refresh tokens are represented in a separate table from `AppUser`, supporting multiple sessions per user.
- VERIFIED: Refresh tokens are stored as hashes, while raw refresh tokens are returned to the client only in login/refresh responses.
- VERIFIED: Access tokens are JWTs and are not stored in the database.
- VERIFIED: `AppUser.NormalizedEmail` has a unique database index. Evidence: `AppDbContext.OnModelCreating`, auth migrations.
- VERIFIED: Entity constraints were configured for required fields, max email length, and refresh-token relationships. Evidence: `AppDbContext.OnModelCreating`.
- VERIFIED: JWT bearer validation disables inbound claim remapping with `MapInboundClaims = false`, keeping claim names such as `sub` stable.

## Alternatives Considered and Rejected

- PARTIAL: Username login was discussed and deferred. Rationale: email-only login keeps Milestone 2 focused; username/display name can be added later as profile/display identity.
- PARTIAL: Email confirmation was discussed and deferred. Rationale: email delivery adds infrastructure beyond this milestone; `EmailConfirmedAt` remains as a future-support field.
- PARTIAL: Storing refresh tokens directly on `AppUser` was rejected. Rationale: one user can have multiple sessions/devices; separate `RefreshToken` rows model that correctly.
- PARTIAL: Plain SHA-256 password hashing was rejected. Rationale: SHA-256 is too fast for human passwords; ASP.NET Core `PasswordHasher<AppUser>` is used instead.
- PARTIAL: Returning/logging sensitive auth data was rejected. Rationale: password hashes, raw passwords, JWTs, raw refresh tokens, and refresh-token hashes should not be logged.
- PARTIAL: Reusing `RefreshRequest` for logout was discussed; a separate `LogoutRequest` was chosen because refresh and logout are different API concepts even though they currently have the same shape.
- PARTIAL: Idempotent logout behavior was discussed but not selected for current learning-mode behavior. Current implementation returns `401` for missing, expired, or already revoked refresh tokens.

## Problems and Bugs Encountered

- PARTIAL: `POST /auth/register` initially returned `404` because controllers were not mapped/registered. Resolved by adding controller registration and `app.MapControllers()`.
- PARTIAL: EF query `SELECT * FROM "AppUsers"` initially failed because migrations had not created the table. Resolved by creating/applying EF Core migrations.
- PARTIAL: EF migration warned about possible data loss when changing `Email`/`NormalizedEmail` from `text` to `varchar(320)`. Resolved by inspecting the migration and confirming existing data length was under 320.
- PARTIAL: `Created(response)` produced a compile/API usage error because `ControllerBase.Created` does not have a one-argument overload. Resolved by using a valid response form such as `StatusCode(StatusCodes.Status201Created, response)`.
- PARTIAL: A malformed `.http` request produced "Header name must be a valid HTTP token" because a blank line was missing between headers and JSON body. Resolved by separating headers from body.
- PARTIAL: `/auth/me` initially received an invalid token error because a truncated token containing `...` was used. Resolved by using the complete access token.
- PARTIAL: Confusion between `ControllerBase.User` / `HttpContext.User` and the EF `AppUser` entity was identified and clarified.
- PARTIAL: Confusion between access tokens, JWT format, and refresh tokens was identified and clarified.

## Debugging and Resolution Approach

- PARTIAL: Used incremental endpoint testing through `.http` requests and inspected HTTP status codes.
- PARTIAL: Used PostgreSQL queries against `AppUsers` and `RefreshTokens` to verify table creation, stored hashes, expiry timestamps, and revocation timestamps.
- PARTIAL: Used application logs from Serilog and ASP.NET Core middleware to trace route matching, authentication failure, and request outcomes.
- PARTIAL: Inspected EF Core migration output before applying schema-changing migrations.
- VERIFIED: Performed build verification during this handoff with `dotnet build .\expense_management_app\expense_management_app.csproj`; build succeeded after package restore.

## Technical Concepts Learned or Demonstrated

- PARTIAL: Difference between EF Core foreign key (`AppUserId`) and navigation property (`AppUser`).
- PARTIAL: Difference between C# `required`, DataAnnotations `[Required]`, and `= string.Empty`.
- PARTIAL: Password hashing vs general hashing; `PasswordHasher<AppUser>` for passwords and SHA-256 only for random high-entropy refresh tokens.
- PARTIAL: JWT as a token format; access token as a short-lived JWT used for protected API calls.
- PARTIAL: Refresh token as a longer-lived opaque secret used only to obtain new tokens.
- PARTIAL: Refresh token rotation: revoke old token and issue/store a new one.
- PARTIAL: Claims-based authentication, including the `sub` claim as user id.
- PARTIAL: ASP.NET Core authentication vs authorization:
  - authentication creates `HttpContext.User`
  - authorization enforces `[Authorize]`
- PARTIAL: Typed options pattern with `IOptions<JwtOptions>`.
- PARTIAL: Structured logging placeholders such as `{UserId}` instead of string interpolation.
- PARTIAL: Avoiding sensitive values in logs.

## Code/Components Implemented by User During Milestone

- PARTIAL: User created and iteratively modified the auth entities, DTOs, services, controller endpoints, configuration, and migrations while receiving guidance. Repository evidence confirms the components exist, but line-by-line authorship is not independently verifiable from the repository alone.
- VERIFIED: Components present in repository:
  - `expense_management_app/Models/Identity/AppUser.cs`
  - `expense_management_app/Models/Identity/RefreshToken.cs`
  - `expense_management_app/Contracts/Auth/*.cs`
  - `expense_management_app/Controllers/AuthController.cs`
  - `expense_management_app/Services/IJwtTokenService.cs`
  - `expense_management_app/Services/JwtTokenService.cs`
  - `expense_management_app/Services/IRefreshTokenService.cs`
  - `expense_management_app/Services/RefreshTokenService.cs`
  - `expense_management_app/Options/JwtOptions.cs`
  - auth-related EF Core migrations

## Areas Where Codex Provided Substantial Guidance

- VERIFIED: Codex provided mentoring/guidance throughout the milestone in this thread.
- PARTIAL: Substantial guidance covered:
  - auth flow sequencing
  - entity placement and EF relationship modeling
  - DTO design and validation attributes
  - registration and login endpoint behavior
  - password hashing selection
  - JWT configuration, generation, and validation concepts
  - refresh-token generation, hashing, rotation, and logout revocation
  - protected endpoint behavior with `[Authorize]`
  - logging/security practices
  - manual test flows

## Things Generated or Designed by Codex That Should Not Be Overclaimed

- PARTIAL: Codex designed or provided close skeletons/patterns for:
  - `JwtOptions`
  - `AuthResponse`
  - `IJwtTokenService` / `JwtTokenService`
  - `IRefreshTokenService` / `RefreshTokenService`
  - auth endpoint flow pseudocode for register/login/refresh/logout/me
  - JWT bearer validation setup in `DependencyInjection.cs`
  - manual endpoint test sequences
- PARTIAL: Codex provided conceptual explanations and recommended implementation shapes. User appears to have typed and adapted code, but this handoff should not claim all auth architecture was independently designed without guidance.
- VERIFIED: During this handoff creation, Codex created this documentation file. Codex did not modify milestone auth source files in this turn.

## Tests, Validation, Security, and Error Handling Added

- VERIFIED: DataAnnotations validation exists on auth request DTOs:
  - `RegisterRequest`: required email, email format, max length 320, required password, min length 8, max length 128
  - `LoginRequest`: required email, email format, max length 320, required password
  - `RefreshRequest`: required refresh token, min length 20
  - `LogoutRequest`: required refresh token, min length 20
- VERIFIED: Duplicate email check returns conflict before user creation. Evidence: `AuthController.Register`.
- VERIFIED: Login uses generic `Unauthorized()` for invalid credentials. Evidence: `AuthController.Login`.
- VERIFIED: JWT generation includes `sub`, `email`, and `jti` claims. Evidence: `JwtTokenService.GenerateAccessToken`.
- VERIFIED: JWT validation checks issuer, audience, lifetime, and signing key. Evidence: `DependencyInjection.cs`.
- VERIFIED: Refresh/logout reject missing, expired, or revoked refresh tokens. Evidence: `AuthController.Refresh`, `AuthController.Logout`.
- VERIFIED: Refresh token rotation stores only hashed refresh tokens in the database. Evidence: `AuthController`, `RefreshTokenService`.
- VERIFIED: `/auth/me` is protected by `[Authorize]` and loads current user from DB by authenticated `sub` claim. Evidence: `AuthController.Me`.
- PARTIAL: Manual validation was reported for successful register/login/refresh/logout and rejection cases, but no automated test suite exists.
- PLANNED: Unit/integration tests for core auth flows remain unfinished.

## Refactors and Why They Happened

- PARTIAL: `User` entity name was changed/settled as `AppUser` to reduce confusion with framework/user identity concepts.
- PARTIAL: Identity models were placed under `Models/Identity`; later repository work also added expense/receipt model folders. Only the identity model placement belongs to Milestone 2.
- PARTIAL: Auth token logic was extracted to services to keep `AuthController` focused on HTTP orchestration and to make token logic reusable for login and refresh.
- PARTIAL: Request/response DTOs were separated from EF entities to avoid exposing persistence details such as `PasswordHash` or navigation collections.

## Remaining Technical Debt and Unfinished Work

- VERIFIED: No automated test project exists in the repository for Milestone 2 auth flows.
- VERIFIED: `documents/milestone_2_note.md` still marks `Add unit/integration tests for core auth flows` as incomplete.
- PARTIAL: Auth controller contains extensive learning comments. Useful during learning, but may be too verbose for production code.
- PARTIAL: Auth logic is concentrated in `AuthController`; an application/auth service could later reduce controller size and improve testability.
- PARTIAL: Login timing side channel mitigation was discussed but not implemented.
- PARTIAL: Rate limiting, lockout/throttling, CAPTCHA, MFA, email confirmation delivery, and account disablement are not implemented.
- PARTIAL: Logout currently returns `401` for invalid/already revoked tokens; idempotent `204` logout behavior was discussed but not implemented.
- PARTIAL: JWT secret is present in `appsettings.Development.json` as a development secret. Production secret storage is not implemented.
- PARTIAL: No automated tests verify token expiry, refresh reuse rejection, logout revocation, validation failures, or `/auth/me` authorization behavior.
- PARTIAL: `AppUser` now contains navigation properties for later modules (`Expenses`, `Categories`, `Tags`, `Receipts`), which are outside Milestone 2 scope and should not be claimed as part of this milestone.

## Relevant Evidence

- VERIFIED: Milestone note: `documents/milestone_2_note.md`
- VERIFIED: Main controller: `expense_management_app/Controllers/AuthController.cs`
- VERIFIED: Identity entities:
  - `expense_management_app/Models/Identity/AppUser.cs`
  - `expense_management_app/Models/Identity/RefreshToken.cs`
- VERIFIED: Auth DTOs: `expense_management_app/Contracts/Auth/*.cs`
- VERIFIED: Token services:
  - `expense_management_app/Services/JwtTokenService.cs`
  - `expense_management_app/Services/RefreshTokenService.cs`
  - `expense_management_app/Services/IJwtTokenService.cs`
  - `expense_management_app/Services/IRefreshTokenService.cs`
- VERIFIED: JWT options: `expense_management_app/Options/JwtOptions.cs`
- VERIFIED: DI/auth registration: `expense_management_app/Infrastructure/DependencyInjection.cs`
- VERIFIED: Middleware pipeline: `expense_management_app/Program.cs`
- VERIFIED: EF Core context and constraints: `expense_management_app/Infrastructure/Persistence/AppDbContext.cs`
- VERIFIED: Auth-related migrations:
  - `expense_management_app/Migrations/20260722060109_AddAppUsersRefreshTokensTables.cs`
  - `expense_management_app/Migrations/20260725081239_AddDatabaseIndex.cs`
  - `expense_management_app/Migrations/20260725083252_ConfigureAuthEntityConstraints.cs`
- VERIFIED: Package references in `expense_management_app/expense_management_app.csproj` include:
  - `Microsoft.AspNetCore.Authentication.JwtBearer`
  - `System.IdentityModel.Tokens.Jwt`
  - `Microsoft.EntityFrameworkCore.Design`
  - `Npgsql.EntityFrameworkCore.PostgreSQL`
  - `Serilog.AspNetCore`
- VERIFIED: Relevant commits from `git log --oneline --max-count=8`:
  - `26e5677 [9/10] Milestone 2: Identity and Authentication`
  - `1a3d712 (7/10) Milestone 2: Identity and Authentication`
  - `c364ef0 [3/10] Milestone 2: Identity and Authentication`
  - `3e70df4 [3/10] Milestone 2: Identity and Authentication`
- VERIFIED: Build verification during handoff:
  - Command: `dotnet build .\expense_management_app\expense_management_app.csproj`
  - Result: succeeded with 0 warnings and 0 errors after package restore.

