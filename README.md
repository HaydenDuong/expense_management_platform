# Expense Management Platform

## Overview

Expenses Management Platform is a backend-focused personal finance application designed to automate expense tracking through OCR and AI technologies.

Instead of manually recording transactions, users can upload receipts, PDFs, or digital invoices. The system extracts structured information, categorizes expenses using AI, generates financial insights, and helps users monitor their spending habits.

The primary objective of this project is not only to build an expense tracker, but also to explore modern backend engineering practices including asynchronous processing, modular monolith architecture, cloud storage, AI integration, distributed messaging, and production-ready system design.

## Vision

Build a production-ready backend platform capable of processing financial documents automatically while demonstrating enterprise backend architecture and engineering best practices.

## Project Goals

### Business Goals

- Automate expense tracking.
- Reduce manual data entry.
- Generate spending analytics.
- Provide budgeting tools.
- Support AI-assisted financial insights.

### Engineering Goals

- Learn scalable backend architecture and maintain system design.
- Build maintainable modules.
- Practice distributed systems.
- Design asynchronous workflows.
- Integrate cloud services.
- Improve API design.
- Implement observability.
- Increase test coverage.

## Tech Stack

Candidate:
    - Language: C#
    - Framework: ASP.NET Core
    - Database: PostgreSQL
    - ORM: EF Core
    - Authentication: JWT
    - Storage: MinIO (local) => Amazon S3
    - Queue: RabbitMQ
    - Cache: Redis
    - OCR: Azure Document Intelligence / Google Vision
    - AI: Ollama => OpenAI (optional)
    - Logging: Serilog
    - Monitoring: OpenTelemetry
    - Metrics: Prometheus
    - Dashboard: Grafana
    - Testing: xUnit
    - API Docs: Swagger
    - CI / CD: GitHub Actions
    - Container: Docker

## Architecture

    - Current: Modular Monolith

## Engineering Principles

    - Build the simplest solution that can evolve.
    - Prefer asynchronous processing for long-running tasks.
    - Keep AI isolated behind interfaces.
    - Design for replaceable infrastructure components.
    - Validate AI output before persisting data.
    - Follow Clean Architecture and SOLID principles.
    - Write tests for business logic.
    - Keep modules loosely coupled and highly cohesive.
    - Prefer observability over debugging.
    - Optimize for maintainability before optimization.

## Functional Requirements (Business Features)

    - User Registration.
    - Account Authentication.
    - Login / Logout.
    - Refresh Token.
    - Profile Management.
    - Expense Management.
    - CRUD Expense.
    - Expense Categories.
    - Expense Tags.
    - Receipt Upload.
    - OCR Processing.
    - AI Categorization.
    - Budget.
    - Analytics.
    - Search.
    - Notifications.

## Non-Functional Requirements

    - JWT Authentication.
    - Role-based Authorization.
    - Background Processing.
    - Retry Mechanism.
    - Structured Logging.
    - Monitoring.
    - Rate Limiting.
    - Caching.
    - Pagination.
    - Validation.
    - API Versioning.
    - Docker.
    - CI / CD.
    - Unit Testing.
    - Integration Testing.

## System Modules

The platform is designed as a modular monolith. Each module owns its domain logic and data access boundaries, while sharing common infrastructure such as authentication, logging, persistence, and messaging.

### Identity Module

Responsible for user accounts, authentication, authorization, refresh tokens, and user ownership rules.

Core responsibilities:
    - User registration and login
    - Password hashing
    - JWT generation and validation
    - Refresh token lifecycle
    - Authenticated user context

### Expense Module

Responsible for manually created and AI-generated expense records.

Core responsibilities:
    - Expense CRUD
    - Categories and tags
    - Expense validation
    - User-owned expense queries
    - Filtering, sorting, and pagination

### Receipt Module

Responsible for uploaded financial documents and their processing lifecycle.

Core responsibilities:
    - Receipt upload
    - Receipt metadata
    - Receipt status tracking
    - File validation
    - Receipt deletion rules

### Storage Module

Responsible for storing and retrieving uploaded files through replaceable storage providers.

Core responsibilities:
    - Object storage abstraction
    - Local/MinIO/S3 implementations
    - Signed download URLs
    - File checksums and metadata
    - Storage cleanup

### Processing Module

Responsible for asynchronous workflows that happen outside the HTTP request lifecycle.

Core responsibilities:
    - Publishing processing events
    - Consuming background jobs
    - Retry and failure handling
    - Idempotency
    - Processing status updates

### OCR Module

Responsible for extracting raw text from uploaded receipts and invoices.

Core responsibilities:
    - OCR provider abstraction
    - OCR result storage
    - OCR confidence tracking
    - OCR error handling
    - Multi-provider support

### AI Parsing Module

Responsible for converting OCR text into structured expense data.

Core responsibilities:
    - LLM provider abstraction
    - Prompt versioning
    - Structured JSON output
    - Schema validation
    - AI confidence tracking

### Review Module

Responsible for human review when OCR or AI output is incomplete, uncertain, or invalid.

Core responsibilities:
    - Pending review state
    - User correction flow
    - Approval before expense creation
    - Audit trail from OCR to final expense

### Budget Module

Responsible for user budgeting rules and spending limits.

Core responsibilities:
    - Monthly budgets
    - Category budgets
    - Budget usage calculation
    - Budget alerts

### Analytics Module

Responsible for spending summaries, trends, and financial insights.

Core responsibilities:
    - Daily, weekly, and monthly summaries
    - Category breakdowns
    - Merchant analysis
    - Spending trends
    - AI-assisted insights

### Notification Module

Responsible for notifying users about important events.

Core responsibilities:
    - Processing completed notifications
    - Receipt processing failure alerts
    - Budget threshold alerts
    - Email or in-app notification delivery

### Search Module

Responsible for finding expenses and receipts efficiently.

Core responsibilities:
    - Keyword search
    - Merchant search
    - Date range search
    - Category/tag filters
    - Future full-text or AI-assisted search

### Observability Module

Responsible for making the system understandable in development and production.

Core responsibilities:
    - Structured logging
    - Metrics
    - Tracing
    - Health checks
    - Dashboards

### Shared Kernel

Contains shared building blocks used across modules.

Core responsibilities:
    - Base entities
    - Domain errors
    - Result types
    - Common validation
    - Date/time abstractions
    - Current user context

## Development Roadmap

### Milestone 1 - Backend Foundation

#### Goal

Create a runnable ASP.NET Core backend foundation with:
    - A clean project structure.
    - Database connectivity.
    - Consistent API behavior.
    - Local development support.

#### Scope

This milestone does not implement business features yet. It establishes the technical base that future modules will build on.

#### Learning Objectives

- ASP.NET Core Web API Structure.
- Dependency Injection.
- EF Core with PostgreSQL.
- Configuration by environment.
- Global error handling.
- Structured logging.
- Docker-based local development

#### Architecture Decisions

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
    - [x] Create initial migration [Delay to Stage 2]
    - [x] Add database health check

API Foundation
    - [x] Add Swagger/OpenAPI
    - [x] Add global exception middleware (app.UseExceptionHandler() is a global exception middleware)
    - [x] Add Problem Details responses
    - [x] Add request validation foundation [Delay until first request DTO in Auth module]
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

### Definition of Done

    - API starts locally.
    - PostgreSQL runs through Docker.
    - EF Core can apply migrations.
    - Swagger is available.
    - Health check confirms database connectivity.
    - Unhandled exceptions return consistent Problem Details responses.

#### Notes

### Milestone 2 - Identity and Authentication

#### Goal

    Allow users to create an account, authenticate securely, refresh sessions, and access protected API endpoints.

#### Scope

    This milestone introduces user identity but does not include profile management, roles beyond a basic authenticated user, or email delivery unless added as optional work.

#### Learning Objectives

    - Password hashing
    - JWT access tokens
    - Refresh token lifecycle
    - Claims-based authentication
    - Authorization policies
    - Secure authentication API design

#### Business Requirements

    - A user can register with email and password.
    - A user can log in and receive an access token and refresh token.
    - A user can refresh an expired access token.
    - A user can log out by revoking their refresh token.
    - Protected endpoints require authentication.
    - Passwords are never stored in plain text.

#### API Endpoints

- [x] `POST /auth/register`
- [x] `POST /auth/login`
- [x] `POST /auth/refresh`
- [x] `POST /auth/logout`
- [x] `GET /auth/me`

#### Data Model

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

#### Tasks

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

#### Definition of Done

    - A new user can register.
    - A registered user can log in.
    - JWT-protected endpoints reject unauthenticated requests.
    - Refresh tokens can issue new access tokens.
    - Logout prevents reuse of the refresh token.

### Milestone 3 - Expense Management Module

#### Goal

Allow authenticated users to manually create, update, search, and organize expenses before introducing receipt upload and AI automation.

#### Scope

This milestone focuses on user-owned expense records. Receipt upload, OCR, AI parsing, and budgeting are handled in later milestones.

#### Learning Objectives

    - Domain modeling
    - User-owned data access
    - CRUD API design
    - Pagination, filtering, and sorting
    - EF Core relationships
    - Database indexes
    - Transaction boundaries

#### Business Requirements

    - A user can create an expense manually.
    - A user can view only their own expenses.
    - A user can update or delete their own expenses.
    - A user can categorize expenses.
    - A user can tag expenses.
    - A user can filter expenses by date, category, merchant, and amount.
    - A user can sort expenses by date, amount, or merchant.
    - Expense amounts must be positive.
    - Expense dates must be valid.

#### API Endpoints

    - [x] `POST /expenses`
    - [x] `GET /expenses`
    - [x] `GET /expenses/{id}`
    - [x] `PUT /expenses/{id}`
    - [x] `DELETE /expenses/{id}`
    - [x] `GET /categories`
    - [x] `POST /categories`
    - [x] `GET /tags`
    - [x] `POST /tags`

#### Data Model

    Expense
        - Id
        - UserId
        - Merchant
        - Amount
        - Currency
        - ExpenseDate
        - CategoryId
        - Notes
        - CreatedAt
        - UpdatedAt

    Category
        - Id
        - UserId
        - Name
        - CreatedAt

    Tag
        - Id
        - UserId
        - Name

    ExpenseTag
        - ExpenseId
        - TagId

#### Tasks

    - [x] Create Expense entity
    - [x] Create Category entity
    - [x] Create Tag entity
    - [x] Create EF Core relationships
    - [x] Add expense CRUD endpoints
    - [x] Add pagination
    - [x] Add filtering
    - [x] Add sorting
    - [x] Add ownership checks
    - [x] Add indexes for common queries
    - [ ] Add tests for expense rules and user isolation

#### Definition of Done

    - Authenticated users can manage their own expenses.
    - Users cannot access another user's expenses.
    - Expense list supports pagination, filtering, and sorting.
    - Categories and tags can be assigned to expenses.
    - Core expense validation is covered by tests.

### Milestone 4 - Receipt Upload Module

#### Goal

    Allow users to securely upload receipts (images / PDFs) and register them for processing.

#### Learning Objectives

    - Multipart Form Upload.
    - File Streaming.
    - File Validation.
    - Object Metadata.
    - Secure File Handling.
    - File Size Limitation.
    - MIME Type Validation.

#### Business Requirements

    - User uploads receipt.
    - Receipt belongs to one user.
    - Receipt can be image or PDF.
    - Receipt status starts as "Pending".
    - Receipt has upload timestamp.
    - Receipt stores original filename.
    - Receipt stores storage path.
    - Receipt can be deleted before processing.

#### Task

    API:
    - [x] POST /receipts
    - [x] GET /receipts
    - [x] GET /receipts/{id}
    - [x] DELETE /receipts/{id}

    Validation:
    - [x] Validate file exists.
    - [x] Validate file size.
    - [x] Validate file extension.
    - [x] Validate MIME type.
    - [x] Reject corrupted uploads

    Database:
        Receipt
            Id
            UserId
            Status
            OriginalFileName
            StorageKey
            ContentType
            FileSize
            CreateAt
            UpdatedAt

    Business Logic:
    - [x] One receipt belongs to one user.
    - [x] Prevent duplicate uploads (optional hash).
    - [x] Receipt starts with Pending status.

    Security:
    - [x] Authorization.
    - [x] Max upload size.
    - [x] Sanitize filename.
    - [ ] Virus scan placeholder (future)

#### Definition of Done

    - Upload feature works.
    - Receipt metadate saved.
    - File stored successfully.
    - Validation working.

### Milestone 5 - Object Storage

#### Goal
Separate file storage from application server.

#### Learning Objectives
- Amazon S3.
- MinIO.
- File Streaming.
- Object Storage Concepts.
- Signed URLs.

#### Business Requirements
- Application should never store receipt inside PostgreSQL.
- Only store metadata.

#### Tasks
Storage Abstraction
    - Create interface: "IObjectStorage"
    - Implement:
        "LocalStorage"
        "MinIOStorage"
        "S3Storage"
    - Later:
        Azure Blob Storage - can be added without changing business logic.

Upload
- [ ] Upload stream.
- [ ] Generate unique filename.
- [ ] Folder per user.
- [ ] Folder per year / month. (e.g: receipts/user-123/2026/07/uuid.jpg)

Download
- [ ] Download stream.
- [ ] Signed URL.
- [ ] Authorization.

Delete
- [ ] Delete object.
- [ ] Delete metadata.
- [ ] Soft delete.

Metadata
    - Store:
        "Storage Provider"
        "Storage Key"
        "Checksum"
        "Content Length"
        "Created Time"

Future
- Compression.
- Encryption.
- Versioning.

#### Notes

### Milestone 6 - Background Processing

#### Goal
Move heavy processing outside request-reponse lifecycle.

#### Learning Objectives
- RabbitMQ.
- Producer.
- Consumer.
- Background Worker.
- Retry.
- Idempotency.

#### Architecture
Client => API => RabbitMQ => Worker => OCR

#### Tasks
Queue
- [ ] RabbitMQ.
- [ ] Queue declaration.
- [ ] Exchange.
- [ ] Routing key.

Producer
    Upload Receipt => Publish Event: "ReceiptUploaded"

Consumer
    Worker => Receive Event => Download Receipt => OCR => Save Result

Retry
- [ ] Retry 3 times.
- [ ] Exponential Backoff.

Failure
    Receipt status: "Failed"
    Reason: "OCR timeout"

Idempotency
    Prevent: Receipt uploaded twice => Processed twice

Logging
    Log:
        - "ReceiptId"
        - "Elapsed Time"
        - "Retry Count"

#### Definition of Done
Upload endpoint returns "202 Accepted" while OCR continues in background

#### Notes

### Milestone 7 - OCR Module

#### Goal
Extract text from uploaded receipts.

#### Learning Objectives
- OCR APIs.
- External APIs.
- Retry.
- Confidence Score.

#### Tasks
OCR Service: "IOcProvider"
    Implement:
        - "Google Vision"
        - "Azure Document Intelligence"
        - "Tesseract"

OCR Result: 
    Store:
        - "Raw Text"
        - "Confidence"
        - "Language"
        - "Provider"

Error Hanling:
    - Timeout.
    - Invalid image.
    - Unsupported language.
    - OCR failed.

Receipt Status:
    Pending => Processing => OCRCompleted => AIProcessing => Completed

Future:
    Support:
        - PDF.
        - Multi-page PDF.
        - HEIC.

#### Definition of Done
OCR extracts readable raw text.

#### Notes:

### Milestone 8 - AI Parsing

#### Goal
Convert messy OCR text into structured expense data.

#### Learing Objectives
- Prompt Engineering.
- Structured Output.
- JSON Schema.
- LLM Integration.

#### Tasks
Create: "IAIParser"

Input: "OCR Text"

Output: JSON format as follow (suggestion)
    {
        "merchant": "",
        "date": "",
        "currency": "",
        "subtotal": 0,
        "tax": 0,
        "total": 0,
        "items": []
    }

Validation:
- JSON schema validation
- Missing fields.
- Invalid date.
- Invalid total.

Confidence
    Store:
        - "Confidence Score"
        - "AI Model"
        - "Prompt Version"

Retry:
    if JSON invalid => retry => fallback prompt

Future:
- Function Calling.
- Structured Output.

#### Definition of Done
AI consistently produces valid JSON for common receipt formats.

### Milestone 9 - Business Validation

#### Goal
Validate AI output before persisting to the database

#### Learning Objectives
- Domain Validation.
- Business Rules.
- Defensive Programming.

#### Tasks
Validate
- [ ] Total > 0.
- [ ] Valid Date.
- [ ] Non-empty Merchant.
- [ ] Valid Currency.
- [ ] No duplicate receipt (hash / fingerprint).
- [ ] Not create duplicate expense due to worker retry.

#### Manual Review Flow
If confidence score is low or missing data: "Pending Review" & allow users to edit => Approve => Persist

#### Audit
Store all: "OCR Raw Text" => "AI Parsed JSON" => "Final User-Corrected Expense"
    - Purpose: aid in debugging and prompt engineering improvement / change of AI model in the future.

#### Notes


### Milestone 10 - Caching Strategy
Introduce Redis caching for frequently accessed read models, including cache-aside, expiration policies, and cache invalidation.

### Milestone 11 - Search
Add flexible expense and receipt search by keyword, merchant, category, tag, and date range. Explore PostgreSQL full-text search before considering AI-assisted search.

### Milestone 12 - Budgeting
Allow users to define monthly and category-level budgets, calculate budget usage, and prepare alert rules.

### Milestone 13 - Analytics
Build spending summaries across daily, weekly, monthly, and yearly views, including category trends and top merchants.

### Milestone 14 - Notifications
Notify users when receipt processing completes, processing fails, or budget thresholds are reached.

### Milestone 15 - Observability
Add production-style logging, tracing, metrics, dashboards, and health checks using Serilog, OpenTelemetry, Prometheus, and Grafana.

### Milestone 16 - Security Hardening
Review authentication, authorization, rate limiting, file upload security, secret management, audit logging, and OWASP risks.

### Milestone 17 - Testing Strategy
Expand test coverage with unit tests, integration tests, Testcontainers, contract tests, and selected load tests.

### Milestone 18 - Deployment
Containerize and deploy the platform with Docker, GitHub Actions, environment configuration, and cloud secret management.

### Milestone 19 - Performance Optimization
Improve database and API performance through indexes, query optimization, pagination, caching, and benchmarking.

### Milestone 20 - AI Financial Insights
Generate weekly and monthly spending summaries, trend explanations, budget predictions, and natural language spending queries.

### Milestone 21 - Production Readiness
Add backup and restore strategy, disaster recovery notes, feature flags, API documentation, operational runbooks, and release checklist.

### Software Design Document (SDD)
README.md
│
├── docs/
│   ├── 01-product-vision.md
│   ├── 02-system-architecture.md
│   ├── 03-database-design.md
│   ├── 04-api-specification.md
│   ├── 05-development-roadmap.md
│   ├── 06-ai-pipeline.md
│   ├── 07-deployment.md
│   ├── 08-testing-strategy.md
│   ├── 09-monitoring.md
│   └── ADR/
│       ├── 0001-use-modular-monolith.md
│       ├── 0002-use-rabbitmq.md
│       ├── 0003-use-minio.md
│       └── 0004-abstract-ai-provider.md

## Future Improvements
