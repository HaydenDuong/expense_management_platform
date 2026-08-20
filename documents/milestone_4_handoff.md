# Milestone 4 Engineering Handoff - Receipt Upload Module

## Scope And Claim Legend

- VERIFIED: Supported by repository files, migrations, or explicit conversation evidence.
- PARTIAL: Implemented or discussed in part, but incomplete, not fully validated, or not covered by tests.
- PLANNED: Documented or discussed as future work, but not implemented.
- UNVERIFIED: Claimed or implied but not confirmed from repository evidence.

This handoff is factual project-audit material. It is not resume copy.

## Goal

- VERIFIED: Add a receipt upload module that allows authenticated users to upload receipt files and register receipt metadata for later processing.
- VERIFIED: Model receipt ownership, upload metadata, storage location, file validation requirements, and receipt processing status.
- PARTIAL: The milestone is currently in progress. Receipt model, EF Core mapping, migration, and API contracts exist, but the receipt controller and actual upload/file-storage behavior are not implemented yet.

Evidence:
- `README.md`
- `documents/milestone_4_note.md`
- `expense_management_app/Models/Receipts/Receipt.cs`
- `expense_management_app/Models/Receipts/ReceiptStatus.cs`
- `expense_management_app/Infrastructure/Persistence/AppDbContext.cs`
- `expense_management_app/Migrations/20260814002125_AddReceiptUploadModule.cs`
- `expense_management_app/Contracts/Receipts/`

## Features Actually Completed

- VERIFIED: Added `Receipt` entity under `expense_management_app/Models/Receipts/Receipt.cs`.
- VERIFIED: Added `ReceiptStatus` enum under `expense_management_app/Models/Receipts/ReceiptStatus.cs`.
- VERIFIED: Added receipt properties for:
  - `Id`
  - `AppUserId`
  - `AppUser`
  - `Status`
  - `OriginalFileName`
  - `StorageKey`
  - `ContentType`
  - `ContentHash`
  - `FileSize`
  - `CreatedAt`
  - `UpdatedAt`
- VERIFIED: Added `DbSet<Receipt>` to `AppDbContext`.
- VERIFIED: Added `AppUser.Receipts` navigation collection.
- VERIFIED: Configured one-to-many relationship from `AppUser` to `Receipt`.
- VERIFIED: Configured cascade delete from `AppUser` to owned receipts.
- VERIFIED: Configured required receipt metadata fields in EF Core.
- VERIFIED: Configured max lengths:
  - `OriginalFileName`: 320
  - `StorageKey`: 1024
  - `ContentType`: 100
  - `ContentHash`: 64
- VERIFIED: Configured normal query indexes on `(AppUserId, CreatedAt)` and `(AppUserId, Status)`.
- VERIFIED: Configured unique composite index on `(AppUserId, ContentHash)`.
- VERIFIED: Generated migration `AddReceiptUploadModule`.
- VERIFIED: Migration creates `Receipts` table with expected foreign key and indexes.
- PARTIAL: User reported applying the database migration. Repository evidence confirms migration generation, but database state is not captured in the repository.
- VERIFIED: Added receipt API contract files:
  - `CreateReceiptRequest`
  - `ReceiptResponse`
  - `ReceiptListResponse`
  - `ReceiptQueryParameters`

## Features Partially Completed

- PARTIAL: `CreateReceiptRequest` contains `IFormFile File`, but `POST /receipts` is not implemented yet.
- PARTIAL: `ReceiptResponse` exists and intentionally hides internal fields, but no controller currently returns it.
- PARTIAL: `ReceiptListResponse` exists for paginated receipt listing, but `GET /receipts` is not implemented yet.
- PARTIAL: `ReceiptQueryParameters` exists with pagination, created-date filtering, nullable status filtering, and sorting fields, but controller behavior is not implemented yet.
- PARTIAL: Duplicate-upload prevention is designed at the database level through `(AppUserId, ContentHash)`, but content hash calculation during upload is not implemented yet.
- PARTIAL: Local/object file storage behavior is not implemented yet.
- PARTIAL: File validation requirements are documented but not implemented yet.

## Architecture And Design Decisions

- VERIFIED: Receipt model was placed under `Models/Receipts`, matching the module grouping style used by `Models/Identity` and `Models/Expenses`.
- VERIFIED: `ReceiptStatus` was implemented as a separate enum file rather than a string property.
  - Reason: Restrict status to known domain values and avoid typo-driven invalid states.
- VERIFIED: `ReceiptStatus` enum values were explicitly numbered.
  - Reason: EF Core stores enums as integers by default; explicit values reduce risk if enum order changes later.
- VERIFIED: `OriginalFileName`, `StorageKey`, `ContentType`, and `ContentHash` use `required string`.
  - Reason: These are required receipt metadata values known by the application when creating a receipt record.
- VERIFIED: `AppUser` navigation uses `= null!`.
  - Reason: EF Core may populate required navigation properties later through relationship loading.
- VERIFIED: `StorageKey` is modeled as a provider-neutral string rather than a local filesystem path.
  - Reason: Later object storage providers such as MinIO, S3, or Azure Blob Storage can use the same concept.
- VERIFIED: `ContentHash` was added to support duplicate file-content detection.
- VERIFIED: Duplicate prevention was scoped by user using `(AppUserId, ContentHash)`.
  - Reason: The same file content may be valid across different users, but the same user should not upload the exact same file bytes twice.
- VERIFIED: `OriginalFileName` was not used as a unique identity field.
  - Reason: Different files can share a filename, and the same file can be uploaded under different filenames.
- VERIFIED: Query indexes were based on expected access patterns instead of indexing every field.
  - `(AppUserId, CreatedAt)` supports listing current user's receipts by upload time.
  - `(AppUserId, Status)` supports filtering current user's receipts by processing state.
- VERIFIED: Request DTO design separates client-provided input from server-derived metadata.
  - Client provides file only.
  - Server will derive status, filename, storage key, content type, file size, content hash, and timestamps.
- VERIFIED: Response DTO design avoids exposing `AppUserId`, `StorageKey`, `ContentHash`, and navigation properties.

## Alternatives Considered

- VERIFIED: String status values were considered implicitly and rejected in favor of an enum.
  - Reason: Strings allow invalid values such as typos, inconsistent casing, or arbitrary text.
  - Chosen approach: `ReceiptStatus` enum with `Pending`, `Processing`, `Completed`, `Failed`, and `Deleted`.
- VERIFIED: Placing `ReceiptStatus` in the same file as `Receipt` was discussed.
  - Reason to reject for this project: Status is likely to be used by controllers, background workers, OCR flow, filtering, and responses.
  - Chosen approach: Separate `ReceiptStatus.cs`.
- VERIFIED: Using `OriginalFileName` as a unique index was considered and rejected.
  - Reason: Filenames are user-provided display metadata, not reliable content identity.
  - Chosen approach: Use `ContentHash` for content identity.
- VERIFIED: Global `ContentHash` uniqueness was not chosen.
  - Reason: Different users may validly upload identical files.
  - Chosen approach: Unique index on `(AppUserId, ContentHash)`.
- VERIFIED: `HasLength(64)` was attempted for exact hash length enforcement and rejected because EF Core `PropertyBuilder<string>` does not provide `HasLength`.
  - Chosen approach: Use `.HasMaxLength(64)` in EF Core and rely on server-side SHA-256 generation/application validation for exact 64-character hex output.
- VERIFIED: Having clients submit all receipt metadata in `CreateReceiptRequest` was rejected.
  - Reason: Status, storage key, content hash, content type, file size, and timestamps should be server-derived or server-validated.
  - Chosen approach: `CreateReceiptRequest` accepts only `IFormFile File`.

## Problems And Bugs Encountered

- VERIFIED: Receipt files were initially looked for under `Models/Receipts`, but the first implementation was under singular `Models/Receipt`.
  - Resolution: Folder was renamed to `Models/Receipts` to match namespace and existing module naming style.
- VERIFIED: Initial `Receipt` model omitted `StorageKey`.
  - Resolution: Added `StorageKey` as required metadata.
- VERIFIED: Initial EF index idea used `OriginalFileName` as a unique index.
  - Risk: Would incorrectly block unrelated uploads with the same filename, including across users.
  - Resolution: Replaced with query-focused indexes and unique `(AppUserId, ContentHash)` index.
- VERIFIED: Attempting `.HasLength(64)` caused compile-time error because EF Core does not define that API.
  - Resolution: Kept `.HasMaxLength(64)` and documented exact hash length as an application-level concern.
- VERIFIED: `ReceiptQueryParameters.Status` initially used non-nullable enum.
  - Risk: Missing query value would default to `0`, which is not a defined receipt status.
  - Resolution: Changed to `ReceiptStatus?` so `null` means no status filter.
- VERIFIED: Initial `CreateReceiptRequest` included server-derived metadata.
  - Resolution: Changed request contract to accept only `IFormFile File`.

## Debugging And Resolution Approach

- VERIFIED: Used file search to locate receipt files when expected path did not exist.
- VERIFIED: Reviewed EF Core migration before applying it.
- VERIFIED: Checked generated migration for table shape, foreign key, enum storage, string max lengths, and indexes.
- VERIFIED: Compared DTO design against existing expense DTO/controller patterns.
- VERIFIED: Used compiler feedback to identify invalid EF Core API usage (`HasLength`).
- VERIFIED: Discussed query/index behavior before finalizing indexes.
- VERIFIED: Separated metadata fields into display identity (`OriginalFileName`), storage identity (`StorageKey`), and content identity (`ContentHash`).

## Technical Concepts Learned Or Demonstrated

- VERIFIED: Enum vs string for finite business states.
- VERIFIED: EF Core enum storage as integer by default.
- VERIFIED: Explicit enum numeric values to protect persisted meaning.
- VERIFIED: `required` vs `= null!` for domain properties vs EF navigation properties.
- VERIFIED: Framework-populated values and EF navigation-property materialization.
- VERIFIED: File metadata vs file bytes.
- VERIFIED: Storage key vs local file path.
- VERIFIED: Content hash as file-byte fingerprint.
- VERIFIED: SHA-256 hash concept and 64-character hex representation.
- VERIFIED: Why duplicate detection should use file content rather than filename.
- VERIFIED: Why duplicate detection should usually be scoped by user.
- VERIFIED: Normal database indexes vs unique indexes.
- VERIFIED: Composite index order and query-pattern-based indexing.
- VERIFIED: Why indexes speed reads but add write overhead.
- VERIFIED: Nullable query parameters for optional filters.
- VERIFIED: DTO boundaries for upload endpoints.
- VERIFIED: Server-derived metadata in file upload workflows.
- VERIFIED: Migration review as an engineering habit.

## Code And Components Implemented By Hayden

- PARTIAL: Hayden reported creating and revising receipt model files; repository contains the resulting files.
- VERIFIED: Repository contains `expense_management_app/Models/Receipts/Receipt.cs`.
- VERIFIED: Repository contains `expense_management_app/Models/Receipts/ReceiptStatus.cs`.
- PARTIAL: Hayden reported wiring receipt model into `AppDbContext`; repository contains receipt DbSet, configuration, relationship, and indexes.
- VERIFIED: Repository contains `AppUser.Receipts` navigation collection.
- PARTIAL: Hayden reported generating and applying the migration; repository contains generated migration files.
- VERIFIED: Repository contains receipt contract files under `expense_management_app/Contracts/Receipts/`.
- PARTIAL: Authorship of exact code lines is based on conversation and repository state, not commit attribution in this audit pass.

## Areas Where Codex Provided Substantial Guidance

- VERIFIED: Explained why `ReceiptStatus` should be an enum instead of a string.
- VERIFIED: Recommended placing `ReceiptStatus` in a separate file.
- VERIFIED: Explained `UpdatedAt` as status/metadata-change timestamp rather than only PUT-update timestamp.
- VERIFIED: Explained `StorageKey` as a provider-neutral storage identifier.
- VERIFIED: Explained `required` vs `= null!`.
- VERIFIED: Explained EF navigation properties and framework-populated values.
- VERIFIED: Reviewed `Receipt.cs` and identified missing `StorageKey`.
- VERIFIED: Reviewed `AppDbContext` receipt configuration and rejected unique index on `OriginalFileName`.
- VERIFIED: Explained content hashing and duplicate detection using same-user plus same-file-bytes.
- VERIFIED: Explained database indexes using receipt query examples.
- VERIFIED: Recommended `(AppUserId, CreatedAt)`, `(AppUserId, Status)`, and unique `(AppUserId, ContentHash)` indexes.
- VERIFIED: Reviewed generated migration for expected table and indexes.
- VERIFIED: Guided DTO design for `CreateReceiptRequest`, `ReceiptResponse`, `ReceiptListResponse`, and `ReceiptQueryParameters`.
- VERIFIED: Recommended next controller implementation order: skeleton, current user helper, `GET /receipts`, `GET /receipts/{id}`, `POST /receipts`, then `DELETE /receipts/{id}`.

## Work Not Fully Claimable As Independently Designed

- VERIFIED: Codex provided substantial design guidance for receipt status modeling, enum placement, and enum persistence considerations.
- VERIFIED: Codex provided substantial design guidance for `StorageKey`, `ContentHash`, and duplicate-upload strategy.
- VERIFIED: Codex recommended specific EF Core indexes and explained why `OriginalFileName` should not be unique.
- VERIFIED: Codex guided the shape of receipt API DTOs, especially why upload request should only accept `IFormFile`.
- VERIFIED: Codex reviewed the generated migration and confirmed expected schema.
- VERIFIED: Codex generated this handoff document.
- PARTIAL: Hayden implemented the repository changes interactively, but some design decisions were strongly mentored and should not be represented as entirely independent architecture work.

## Tests, Validation, Security, And Error Handling

- VERIFIED: EF Core migration was generated for the receipt schema.
- PARTIAL: User reported applying the migration to the database.
- VERIFIED: Database-level duplicate prevention exists in schema through unique `(AppUserId, ContentHash)` index.
- VERIFIED: Database-level ownership relationship exists through `AppUserId` foreign key.
- VERIFIED: Database-level required fields and max lengths exist for receipt metadata.
- PARTIAL: Exact 64-character hash validation is not enforced by a database check constraint.
- PARTIAL: Upload file validation is not implemented yet.
- PLANNED: Validate file exists.
- PLANNED: Validate file size.
- PLANNED: Validate file extension.
- PLANNED: Validate MIME type.
- PLANNED: Reject corrupted uploads.
- PLANNED: Sanitize filename before storing metadata/display value.
- PLANNED: Generate server-owned storage key.
- PLANNED: Calculate SHA-256 hash from uploaded file stream.
- PLANNED: Restrict deletion based on processing status, likely pending-only at first.
- PLANNED: Add virus-scan placeholder later.
- UNVERIFIED: No build or automated test command was run after receipt contract changes in this audit pass.
- VERIFIED: No automated tests for Milestone 4 receipt behavior exist yet.

## Refactors And Why They Happened

- VERIFIED: Receipt folder was corrected from singular `Models/Receipt` to plural `Models/Receipts`.
  - Reason: Match namespace and existing module folder convention.
- VERIFIED: Receipt creation request was refactored from metadata-heavy request to file-only request.
  - Reason: Metadata should be derived and validated by the server.
- VERIFIED: Receipt query status was refactored from non-nullable enum to nullable enum.
  - Reason: Missing query status should mean "no filter," not enum value `0`.
- VERIFIED: Receipt storage key max length was increased from 320 to 1024.
  - Reason: Object-storage keys can be longer than original filenames.
- VERIFIED: Content type max length was increased from 50 to 100.
  - Reason: MIME types can be longer than short common examples.
- VERIFIED: Unique filename index was replaced by a unique content-hash index scoped by user.
  - Reason: Filename is not reliable identity; file content hash is a stronger duplicate signal.

## Remaining Technical Debt And Unfinished Work

- VERIFIED: `ReceiptsController.cs` is not implemented yet.
- VERIFIED: `POST /receipts` is not implemented yet.
- VERIFIED: `GET /receipts` is not implemented yet.
- VERIFIED: `GET /receipts/{id}` is not implemented yet.
- VERIFIED: `DELETE /receipts/{id}` is not implemented yet.
- VERIFIED: Actual file persistence is not implemented yet.
- VERIFIED: File stream hashing is not implemented yet.
- VERIFIED: Upload validation is not implemented yet.
- VERIFIED: Filename sanitization is not implemented yet.
- VERIFIED: Storage cleanup on delete is not implemented yet.
- VERIFIED: No receipt service/storage abstraction exists yet.
- PLANNED: Object storage abstraction is expected in Milestone 5.
- PARTIAL: `ReceiptStatus.Deleted` exists, but deletion strategy is undecided between hard delete and soft delete.
- PARTIAL: Cascade delete from `AppUser` to receipts removes receipt metadata but does not yet address physical file cleanup.
- PARTIAL: `ContentHash` length is max-limited in database but not exact-length constrained.
- PARTIAL: No automated tests exist for receipt schema, duplicate constraints, ownership, or upload validation.
- PARTIAL: Current controllers elsewhere duplicate current-user claim parsing; receipt controller may initially follow the existing pattern, with possible later refactor to shared current-user service.

## Evidence Files

- `README.md`
- `documents/milestone_4_note.md`
- `documents/milestone_4_handoff.md`
- `expense_management_app/Models/Receipts/Receipt.cs`
- `expense_management_app/Models/Receipts/ReceiptStatus.cs`
- `expense_management_app/Models/Identity/AppUser.cs`
- `expense_management_app/Infrastructure/Persistence/AppDbContext.cs`
- `expense_management_app/Migrations/20260814002125_AddReceiptUploadModule.cs`
- `expense_management_app/Migrations/20260814002125_AddReceiptUploadModule.Designer.cs`
- `expense_management_app/Migrations/AppDbContextModelSnapshot.cs`
- `expense_management_app/Contracts/Receipts/Requests/CreateReceiptRequest.cs`
- `expense_management_app/Contracts/Receipts/Responses/ReceiptResponse.cs`
- `expense_management_app/Contracts/Receipts/Responses/ReceiptListResponse.cs`
- `expense_management_app/Contracts/Receipts/ReceiptQueryParameters.cs`

## Relevant Migration Evidence

- VERIFIED: `20260814002125_AddReceiptUploadModule.cs` creates table `Receipts`.
- VERIFIED: `Receipts.AppUserId` is a non-null foreign key to `AppUsers.Id`.
- VERIFIED: `Receipts.Status` is stored as integer.
- VERIFIED: `Receipts.FileSize` is stored as bigint.
- VERIFIED: `Receipts.CreatedAt` and `Receipts.UpdatedAt` are stored as `timestamp with time zone`.
- VERIFIED: `IX_Receipts_AppUserId_ContentHash` is unique.
- VERIFIED: `IX_Receipts_AppUserId_CreatedAt` exists.
- VERIFIED: `IX_Receipts_AppUserId_Status` exists.

## Verification Commands And Results

- VERIFIED: Migration file was inspected after generation.
- VERIFIED: `git status --short` shows Milestone 4 receipt files as untracked/modified at handoff time.
- PARTIAL: User reported running `dotnet ef database update --project expense_management_app`.
- UNVERIFIED: Database update result was not independently checked through a database query in this audit pass.
- UNVERIFIED: `dotnet build` was not run after the latest receipt DTO/query changes during this handoff creation.
- UNVERIFIED: No automated tests were run for Milestone 4 during this handoff creation.

## Exclusions

- VERIFIED: Receipt upload endpoint implementation is outside the completed portion of this handoff.
- VERIFIED: Object storage abstraction is deferred to Milestone 5.
- VERIFIED: Background processing, OCR, AI parsing, review flow, budgeting, analytics, notifications, and production deployment are outside the current completed Milestone 4 work.
