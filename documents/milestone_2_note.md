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

- [x] `POST /auth/register`
- [x] `POST /auth/login`
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
- [x] Add login flow
- [x] Add JWT generation
- [x] Add refresh token rotation
- [x] Add logout/revoke token flow
- [x] Add authenticated test endpoint
- [x] Add validation for auth requests
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

In the same file:
Added configuration for entity constrains for both AppUser & RefreshToken object in "OnModelCreating" method

    AppUser Object:
        - NormalizedEmail unique
        - Email required/max length
        - NormalizedEmail required/max length
        - PasswordHash required
        - CreatedAt/UpdatedAt required
    
    RefreshToken Object:
        - TokenHash required
        - CreatedAt required
        - UpdatedAt required
        - Characteristics of this object:
            A RefreshToken belongs to one AppUser.
            An AppUser can have many RefreshTokens.
            RefreshToken.AppUserId is the foreign key.
            If the user is deleted, their refresh tokens are deleted too.

Create and apply EF Core Migration to the Docker compose PostgreSQL DB:
    [In case "dotnet ef" is not recognized] dotnet tool install --global dotnet-ef
    dotnet ef migrations add AddAppUsersRefreshTokenTables
    dotnet ef database update

For preference, a more production-shaped version will have the followings:
    Database:
    - Unique index on NormalizedEmail
    - Required columns configured
    - Max lengths configured
    - UTC timestamps
    - Migration checked into source control

    Security:
    - Strong password hasher, not SHA256
    - No password/hash/token in logs
    - Generic errors where useful
    - Rate limiting on registration
    - Email confirmation token
    - Optional CAPTCHA / abuse prevention
    - HTTPS only
    - CORS configured intentionally

    API behavior:
    - 201 Created on success
    - 400 for validation errors
    - 409 for duplicate email, or generic success if avoiding account enumeration
    - ProblemDetails response format
    - Consistent response DTOs

    Observability:
    - Log registration attempted
    - Log duplicate attempt without password/email if possible
    - Log success with UserId
    - Add request correlation id
    - Track metrics: registration success/failure count

    Testing:
    - valid registration succeeds
    - duplicate email rejected
    - invalid email rejected
    - weak password rejected
    - password is hashed
    - raw password is never stored
    - unique index exists or duplicate insert fails

### Add Login flow

Added a request DTO in: "/Contracts/Auth/LoginRequest.cs"

Noted, during authentication tests:
    Successful login: 399ms
    Failed login with existing user/wrong password: 123ms
    Failed login with likely unknown email: 16ms

    => Timing different in terminal output during account authentication can technically leak whether an email exists, because:
            Password hash verification is slower than "user not found".
            In serious production auth, teams sometimes add mitigations so unknown-email and wrong password paths take similar times
    
    This timing issue is called a "user enumeration side channel":
        In the current "AuthController.cs":
            Unknown email:
                - query database
                - user is null
                - return 401 quickly
            
            Known email + wrong password:
                - query database
                - verify password hash      (This is intentionally expensive, attackers could measure response times and guess which emails are registered)
                - return 401 more slowly
        
        A more production-shaped approach is: "Always do password verification work, even when the user does not exist."
            - By keep a fake password hash and verify against it when no user is found.

            - A reference skeleton for this approach:

                // This can be generated one once using PasswordHasher<AppUser> with a dummy password, then store it as a constant / config value
                private const string FakePasswordHash = "..."; (This must be generated by the password hasher, not a random string)

                var passwordHash = user?.PasswordHash ?? FakePasswordHash;

                var result = _passwordHasher.VerifyHashedPassword(
                    user ?? new AppUser
                    {
                        Email = "fake@example.com",
                        NormalizedEmail = "FAKE@EXAMPLE.COM",
                        PasswordHash = FakePasswordHash,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    },
                    passwordHash,
                    request.Password);

                if (user is null || result == PasswordVerificationResult.Failed)
                {
                    _logger.LogWarning("Login failed due to invalid credentials.");
                    return Unauthorized();
                }
        
        Moreover, production-level auth usually combines several protections:
            1. Generic error messages
            2. Similar timing for failure paths
            3. Rate limiting
            4. Account lockout or throttling
            5. Monitoring failed login bursts
            6. Optional CAPTCHA after suspicious behavior
            7. MFA for sensitive accounts
        
        In the later stage: timing mitigation can be implemented as a small service like so that the controller.cs file is not messy: 
            AuthService:
            - VerifyPasswordEvenIfUserMissing(...)

### Add JWT generation

Goal: After successful login, return a short-lived access token that proves who the user is

A JWT is not "the login". Login verifies credentials. JWT is the receipt the client uses after login

A JWT access token usually contains:
    sub: user id
    email: user email, optional
    jti: unique token id
    iat/exp: issued/expiry time
    iss: issuer
    aud: audience

Access tokens are:
    Created by server
    Returned to client
    Sent back in Authorization header
    Verified by server
    Not stored in database
    Short-lived

For the current stage of this project:
    sub = AppUser.Id
    email = AppUser.Email
    expires = may be 15 minutes

Implementation Pieces:
    Configuration:
        - Jwt:Issuer
        - Jwt:Audience
        - Jwt:Secret
        - Jwt:AccessTokenMinutes

    Options class:
        - JwtOptions.cs

    Service:
        - IJwtTokenService.cs
        - JwtTokenService.cs

    Response DTO:
        - AuthResponse.cs

Packing JWT code into a service instead of "AuthController.cs" because:
    Controller should orchestrate HTTP behavior only.

    Token generation is auth logic.

    Put the signing code directly into the controller is possible, but, make it coupling => harder to test and resuse for "/auth/refresh"

Step 1: Add Config

    In "appsettings.Development.json", the following was added:

        "Jwt": {
        "Issuer": "ExpenseManagementApi",
        "Audience": "ExpenseManagementClient",
        "Secret": "dev-only-secret-key-that-is-long-enough-for-hmac-signing",
        "AccessTokenMinutes": 15
        }
    
    Noted: When run "dotnet run" => ASP.NET Core reads the following files:
        appsettings.json
        appsettings.Development.json
        environment variables
        user secrets
        command-line args
    
    Remember: ASP.NET Core can read "appsettings.Development.json" but it does not automatically know that the "Jwt" section should become "JwtOptions"
        => Must be register that binding in DI

        => The flow is:
            appsettings.Development.json => builder.Configuration => services.Configure<JwtOptions>(configuration.GetSection("Jwt")) => IOptions<JwtOptions> => JwtTokenService constructor.
            
        => This flow will mapping the config values of "Jwt" in "appsettings.Development.json" into "/Options/JwtOptions.cs" object, because the property names match:
                Jwt:Issuer => JwtOptions.Issuer
                Jwt:Audience => JwtOptions.Audience
                Jwt:Secret => JwtOptions.Secret
                Jwt:AccessTokenMinutes => JwtOptions.AccessTokenMinutes
        
        Thus, we need to manually tell ASP.NET the mapping above as: services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
            Then when the constructor in "JwtOptions.cs" runs:
                public JwtTokenService(IOptions<JwtOptions> jwtOptions)
                {
                    _jwtOptions = jwtOptions.Value;
                }
            
            DI says: JwtTokenService needs IOptions<JwtOptions>.
                     I know how to create that because JwtOptions was configured from the Jwt config section.
        
        Analogy:
            appsetting.Development.json = raw config file
            JwtOptions = typed C# container
            Configure<JwtOptions> = mapping instruction
            IOptions<JwtOptions> = delivery mechanism
        
        "IOption<T>" is part of the "options pattern" => It prevents app's services from doing string-key lookups like: configuration["Jwt:Secret"] 
            It works fine, but spreads config knowledge everywhere
            => Typed options keep it cleaner and easier to validate

Step 2: Create Option Class

    Created "Options/JwtOptions.cs" - This gives typed access to config instead of scattering string keys everywhere

Step 3: Create Token Response DTO

    Created: "Contracts/Auth/AuthResponse.cs" (Different from AuthUserResponse.cs)

Step 4: Token Service

    Installed JWT package: "dotnet add package System.IdentityModel.Tokens.Jwt"

    Created "/Services/IJwtTokenService.cs":
        This is an Interface which should express what the app needs, not how JWT works internally.

        Controller does not need to know signing keys.

        Controller does not need to know claims construction.

        Controller only says: given this user, make me an access token.

        A Good rule:
            Use an interface when the dependency crosses a boundary, may be replaced, or helps testing.

            Skip it for simple internal helpers with no alternate implementation.
    
    This service will use these concepts:
        Claim = facts about the user
        SigningCredentials = proves server created token
        SecurityTokenDescriptor/JwtSecurityToken = token shape
        JwtSecurityTokenHandler = writes token string
    
    For this project, claims will be:
        sub = user.Id
        email = user.Email
        jti = unique token id
    
    Created: "/Services/JwtTokenService.cs"
        AppUser => claims => signed JWT => string
    
    Registered this service in "/Infrastructure/DependencyInjection.cs"

Step 5: Register DI

    Injected "IJwtTokenService" into "AuthController.cs"
    
Step 6: Update Login

### Add refresh token rotation

Concept: this process has 2 phases
    Login:
        - Create access token.
        - Creat refresh token.
        - Store hashed refresh token in DB.
        - Return raw refresh token to client once.
    Refresh:
        - client sends old refresh token.
        - Server hashes it and finds DB row
        - If valid => Revoke old token.
            - Create new access token.
            - Create new refresh token.
            - Store hash of new refresh token.
            - Return both new tokens.

Mental Model:
    Access Token:
        - Short-lived
        - JWT
        - Not stored in DB
        - Used for API requests.
    Refresh Token:
        - Longer-lived.
        - Random opaque string.
        - Stored as hash in DB.
        - Used only to get new token.
        - Can be revoked.
        - Rotated on every refresh.
Refresh token hashing can be used with SHA-256 because this token are random high-entropy values.
    It should be generated from secure random bytes.

Passwords need to be hashed with bcrypt / Argon2 / PBKDF2.

Added:
    In "appsettings.Development.json":
        "RefreshTokenDays": 30
    In "/Options/JwtOptions.cs":
        "public int RefreshTokenDays { get; set; }"
    In "/Contracts/Auth/AuthResponse.cs":
        "public string RefreshToken { get; set; }" = string.Empty;

Created:
    "/Services/IJwtTokenService.cs"
    "/Services/JwtTokenService.cs"
        - GenerateRefreshToken():
            - Create random bytes using crytographic randomness.
            - Convert to a string safe to send in JSON format.
        - HashRefreshToken(refreshToken):
            - Hash the raw token.
            - Store only the hashed in DB

Updated "/Controllers/AuthController.cs"

### Add revoke token flow / Logout

#### Revoke Token Flow (Refresh Token Rotation)

Created "/Contracts/Auth/RefreshRequest.cs"
Added HttpPost /auth/refresh to "/Controllers/AuthController.cs":
    Login:
        Refresh token A created
        A.RevokedAt = null
        A.ExpiresAt = now

    Refresh:
        Client sends token A
        Server validates A
        Server sets A.RevokedAt = now
        Server creates token B
        Client receives token B

    Meaning: a refreshToken will have a null value for its revokedAt property and will turn into a DateTime value after HttpPost /Auth/Refresh to rendering it into not active => allow new refreshToken to be created and save to the corresponding user.

#### Logout = Refresh Token Revoked

Added HttpPOST /auth/logout to "/Controller/AuthController.cs"
    The flow:
        1. Client sends refresh token.
        2. Server hashes it.
        3. Find matching RefreshToken row.
        4. If missing, expired, or already revoked, return 401 or 204 depending on your chosen behavior.
        5. Set RevokedAt = now.
        6. Save changes.
        7. Return 204 No Content.

### Add authenticated test endpoint

This is the step where the access token becomes meaningful

To do this, it is needed to wire JWT bearer authentication into ASP.NET Core:
    1. Register JWT bear authentication in DI
    2. Add authentication / authorization middleware in "Program.cs"
    3. Add [Authorize] to protected endpoints.
    4. Implement GET /auth/me using claims from the access token.

Mental Model:
When a client calls: GET /auth/me
                     Authorization: Bearer ....

ASP.NET Core should => Anything fails = 401 Unauthorized
    1. Read the Authorization header.
    2. Extract the Bear token.
    3. Validate JWT signature.
    4. Validate issuer.
    5. Validate audience.
    6. Validate expiry.
    7. Convert JWT claims into HttpContext.User.
    8. Allow [Authorize] endpoint to run.

Steps:

1. Added ASP.NET Core JWT bearer authentication support:
    dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer --version 10.0.10

2. Register JWT Authentication in "/Infrastructure/DependencyInjection.cs"

3. Added HTTP GET /auth/me to "/Controllers/AuthController.cs"
    GET /auth/me does not receive a request DTO like LoginRequest or RefreshRequest.

        For register/login/refresh/logout:
        - The client sends JSON in the request body.
        - ASP.NET Core model binding turns that JSON into a method parameter.
        - Example: Login(LoginRequest request)

        For /auth/me:
        - The client sends the access token in the HTTP Authorization header.
        - Example: Authorization: Bearer <accessToken>
        - There is no JSON body.
        - There is no request DTO.

        Flow:
        1. User logs in with email/password.
        2. Server returns an access token.
        3. Client calls GET /auth/me and puts the access token in the Authorization header.
        4. ASP.NET Core authentication middleware reads the Authorization header.
        5. The JWT bearer handler validates the token signature, issuer, audience, and expiry.
        6. If valid, ASP.NET Core creates HttpContext.User from the JWT claims.
        7. AuthController inherits from ControllerBase, so inside the controller we can access HttpContext.User as simply User.
        8. The /auth/me endpoint reads the user id from the "sub" claim.
        9. The server queries AppUsers by that id and returns AuthUserResponse.

        Important:
        - User in the controller is not the AppUser database entity.
        - User is HttpContext.User, a ClaimsPrincipal created from the validated JWT.
        - AppUser is the EF Core entity stored in PostgreSQL.

        Why this is safer:
        - /auth/me should not accept userId from the request body.
        - A client could lie and send another user's id.
        - Instead, the user id comes from the signed JWT claim.
        - If a client changes the JWT claim, the signature validation fails.

### Add validation for auth requests

Added [parameters] to Login / Logout / Refresh / Register Request.cs files

### Add unit/integration tests for core auth flows