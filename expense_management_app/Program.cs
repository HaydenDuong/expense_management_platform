using Microsoft.EntityFrameworkCore;
using Serilog;
using expense_management_app.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Replaces the default ASP.NET Core logger with Serilog
// Serilog writes structured logs that are easier to search and analyze.
builder.Host.UseSerilog((context, LoggerConfiguration) =>
{
    LoggerConfiguration

        // Allow Serilog settings from "appsettings.json" later
        .ReadFrom.Configuration(context.Configuration)

        // Include contextual properties attached during a request
        .Enrich.FromLogContext()

        // Output logs to terminal / Docker logs
        .WriteTo.Console();
});

// Add services to the container.
// Register Controllers to handle HTTP Requests
builder.Services.AddControllers();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddInfrastructure(builder.Configuration);

// Registers standardized API error responses using the Problem Details format
builder.Services.AddProblemDetails();

var app = builder.Build();

// Converts unhandled exceptions into consistent Problem Details responses
app.UseExceptionHandler();

// Logs each HTTP request with method, path, status code, and elapse time.
app.UseSerilogRequestLogging();

// When this application is running inside Docker container, then skip HTTPS redirection
// Else, keep HTTPS redirection when running locally on local machine
if (!builder.Configuration.GetValue<bool>("DOTNET_RUNNING_IN_CONTAINER"))
{
    app.UseHttpsRedirection();
}

// Read token and create HttpContext.User
app.UseAuthentication();

// enforce [Authorize]
app.UseAuthorization();

// Route requests to controller actions
app.MapControllers();

// Testing for Serilog with this startup log
Log.Information("Starting Expense Management API in {Environment} environment", app.Environment.EnvironmentName);

// Temporary endpoint for testing Problem Details
app.MapGet("/throw", () =>
{
    throw new InvalidOperationException("This is a test exception.");
});

// Maps GET /health so 
// Docker, monitoring tools, or developers can verify that the API is running
// and its critical dependencies are healthy
// This is marked to complete to task "Expose /health endpoint"
app.MapHealthChecks("/health");

// Configure the HTTP request pipeline.
// Test with: https://localhost:7273/openapi/v1.json
//        or  http://localhost:5089/openapi/v1.json
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.Run();
