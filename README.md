# Expense Management Platform

Backend-focused expense management API built with ASP.NET Core, PostgreSQL and Entity Framework Core.

The current implementation focuses on authentication, user-owned expense data, relational modelling, querying, and receipt upload validation.

## Current Implementation

- User registration and login
- JWT authentication
- Refresh token rotation and revocation
- User-scoped expense CRUD
- Categories and tags
- Many-to-many expense/tag relationships
- Filtering, sorting and pagination
- Receipt upload
- File size, extension, MIME type and corruption checks
- PostgreSQL with EF Core
- Structured logging with Serilog
- Database health checks
- Docker Compose for local PostgreSQL

## Planned Work

- Object storage with MinIO/S3
- Background receipt processing
- RabbitMQ
- OCR integration
- AI-assisted parsing
- Redis caching
- Metrics and tracing
- Automated tests

## Tech Stack

### Current

- C#
- ASP.NET Core
- PostgreSQL
- Entity Framework Core
- JWT Authentication
- Serilog
- Swagger / OpenAPI
- Docker Compose

### Planned / Exploring

- MinIO / Amazon S3
- RabbitMQ
- Redis
- OCR providers
- OpenAI / local LLM integration
- OpenTelemetry
- Prometheus / Grafana
- xUnit
- GitHub Actions

## Architecture

    - Current: ASP.NET Core monolith with separated controllers, contracts, models, services, and infrastructure.
    - Direction: Progressively modularise business capabilities as the application grows

## Roadmap

The project is developed incrementally, beginning with backend fundamentals and moving toward asynchronous receipt processing, OCR, caching and observability.

See the [development roadmap](documents/roadmap.md) for detailed milestones.