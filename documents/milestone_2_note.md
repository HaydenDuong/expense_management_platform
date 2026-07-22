# Milestone 2 - Identity and Authentication

## Goal
Allow users to create an account, authenticate securely, refresh sessions, and access protected API endpoints.

## Scope
This milestone introduces user identity but does not include profile management, roles beyond a basic authenticated user, or email delivery unless added as optional work.

## Learning Objectives
- Password hashing
- JWT access tokens
- Refresh token lifecycle
- Claims-based authentication
- Authorization policies
- Secure authentication API design

## Business Requirements
- A user can register with email and password.
- A user can log in and receive an access token and refresh token.
- A user can refresh an expired access token.
- A user can log out by revoking their refresh token.
- Protected endpoints require authentication.
- Passwords are never stored in plain text.

## API Endpoints
- [ ] `POST /auth/register`
- [ ] `POST /auth/login`
- [ ] `POST /auth/refresh`
- [ ] `POST /auth/logout`
- [ ] `GET /auth/me`

## Data Model
User
- Id
- Email
- PasswordHash
- CreatedAt
- UpdatedAt

RefreshToken
- Id
- UserId
- TokenHash
- ExpiresAt
- RevokedAt
- CreatedAt

## Tasks
- [x] Create User entity
- [x] Create RefreshToken entity
- [x] Add registration flow
- [ ] Add login flow
- [ ] Add JWT generation
- [ ] Add refresh token rotation
- [ ] Add logout/revoke token flow
- [ ] Add authenticated test endpoint
- [ ] Add validation for auth requests
- [ ] Add unit/integration tests for core auth flows

## Definition of Done
- A new user can register.
- A registered user can log in.
- JWT-protected endpoints reject unauthenticated requests.
- Refresh tokens can issue new access tokens.
- Logout prevents reuse of the refresh token.

## Notes

### Check Docker compose PostgreSQL DB container
    docker exec -it expense-management-db psql -U postgres -d ExpenseManagementDB
    SELECT * FROM "AppUsers";
    SELECT * FROM "RefreshTokens";

### Create "AppUser" and "RefreshToken" entities:
Mental model for:

When a user registers:
    1. Accept email and password.
    2. Validate email format and password requirements.
    3. Normalize email, usually lowercase / trim.
    4. Check whether this email already exists (User.email will be used for unique index in PostgreSQL).
    5. Hash the password using password hasher.
    6. Store "User" object in "User" Table in PostgreSQL.
    7. Return success code without exposing PasswordHash.

When a user logs in:
    1. Accept email and password.
    2. Normalize email.
    3. Find user by normalized email.
    4. If no user exists => return 401 Unauthorized.
    5. Verify password using the password hasher.
    6. If password wrong => return 401 Unauthorized.
    7. If valid:
        a. Server creates access token.
        b. Server creates refresh token.
        c. Server stores hash of refresh token in "RefreshToken" table. [This make sure that even the table is leaked, attackers should not immediately get valid refresh token]
        d. Server returns raw access token + raw refresh token to client

Current schema for "AppUser" object to be stored:
AppUser
- Id
- Email
- NormalizedEmail
- PasswordHash
- EmailConfirmedAt [placeholder at the moment]
- CreatedAt
- UpdatedAt

Refresh tokens will be stored in another table that dedicated to it, because one user can have multiple active sessions (laptop, phone, browser)
If refresh token lived directly on "User" object, then that could only represent one session => Logging on different device might overwrite the current device session.
RefreshToken
- Id
- AppUserId                 [ The ID stored in the DB]
- Class AppUser AppUser     [ The full "AppUser" object loaded in C#]
    public AppUser AppUser { get; set; } = null!;
    "null!" was used, because: 
        This is a navigation property - it is a relationship metadata
        Do not send "nullable warning" as "!" = null-forgiving operator.
        This does not make the value non-null at runtime
        We used it here because this property is for EF Navigation:
            The DB relationship is enforced by "AppUserId"
            EF can load "AppUser" later
            Create a token may only need "AppUserId"
            => C# nullable warnings stay quiet
    // "required" was not, because this property is not expect application code to provide every time


- TokenHash
- CreatedAt
- ExpiresAt
- RevokedAt



AppUsers table

Id | Email
---|--------------------
7  | hayden@example.com

RefreshTokens table

Id | AppUserId | TokenHash | ExpiresAt
---|-----------|-----------|----------------
1  | 7         | abc123... | 2026-08-22
2  | 7         | def456... | 2026-08-22

"AppUserId" is the actual database link. It says:
    Refresh token 1 belongs to user 7.
    Refresh token 2 belongs to user 7.

Example use case for "AppUserId":
    var refreshToken = new RefreshToken
    {
        AppUserId = user.Id,
        TokenHash = hashedToken,
        CreatedAt = DateTime.UtcNow,
        ExpiresAt = DateTime.UtcNow.AddDays(30)
    };

    Here, you already know the user's ID, so you don't need to attach the whole user object. (This is common when creating rows)

Example use case for "AppUser":
    var refreshToken = await db.RefreshTokens
        .Include(rt => rt.AppUser)
        .FirstOrDefaultAsync(rt => rt.TokenHash == hashedToken);

    var email = refreshToken.AppUser.Email

    This is useful when refreshing a token, because after finding the refresh token, you often need the "AppUser" object so you can create new access token for that user

Access tokens usually are not stored in the database: because they are short-lived JWTs. The server creates one, gives it to the client, and later verifies its signature when the clients sends it backs.

In "/Models":
    Created both "AppUser.cs" & "RefreshToken.cs" object schemas.

### Add registration flow

Current mental flow for registration:
    Request => Validate => Check Duplicates => Hash Password => Save User

    1. Receive email and password.
    2. Trim and normalize email.
    3. Validate:
        - email is required
        - email format is valid
        - password is required
        - password has minimum strength
    4. Check if NormalizedEmail already exists.
    5. If exists, return 409 Conflict.
    6. Hash password.
    7. Create AppUser.
    8. Save to PostgreSQL.
    9. Return a safe response.
        This response means do not return:
            PasswordHash
            RefreshTokens

The endpoint contract can be:
    POST /auth/register
    Content-Type: application/json

    {
        "email": "abc@example.com",
        "password": "SomeStrongPassword123!"
    }

Possible HTTP Responses:
    201 Created - User Registerd Successfully
    400 Bad Request - Invalid email / password shape
    409 Conflict - Email already exists

Created:
    Contracts/Auth/
        RegisterRequest.cs
        AuthUserResponse.cs
    
    Controllers/
        AuthController.cs

The Different between "required" and "[Required]"
required: (For Domain / Data Objects where missing values are a programming mistake)
    Compile-time object initialization rule.
    Helps when the application C# code creates the object.
    Does not replace API validation.

[Require]: (Validation attributes for request DTOs where missing values are client input problem)
    Runtime validation rule.
    Helps when JSON comes from the client.
    Produces validation errors for bad requests.

Added to "AppDbContext.cs":
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
        Modern Approach:
            No setter
            No nullable warning
            Clean and modern
            Clearly says DbContext owns this set

    can be written as: public DbSet<AppUser> AppUsers { get; set; }
        But this is old / common style
            Property EF can intialize
            May need = null!
            Allows setting, though you normally never set it yourself

Create and apply EF Core Migration to the Docker compose PostgreSQL DB:
    [In case "dotnet ef" is not recognized] dotnet tool install --global dotnet-ef
    dotnet ef migration add AddAppUsersRefreshTokenTables
    dotnet ef database update