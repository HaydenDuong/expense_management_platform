# Milestone 3 Engineering Handoff - Expense Management Module

## Scope And Claim Legend

- VERIFIED: Supported by repository files, migrations, or manual testing described in the project notes/conversation.
- PARTIAL: Implemented in part, but incomplete, not fully polished, or not covered by automated tests.
- PLANNED: Discussed or documented as future work, but not implemented in this milestone.
- UNVERIFIED: Claimed or implied but not confirmed from repository evidence.

This handoff is factual project-audit material. It is not resume copy.

## Goal

- VERIFIED: Implement a manually managed expense module for authenticated users before receipt upload, OCR, AI parsing, and budgeting.
- VERIFIED: Support user-owned expenses, categories, and tags.
- VERIFIED: Support expense CRUD, category/tag creation and listing, pagination, filtering, sorting, EF Core relationships, and database indexes.

Evidence:
- `documents/milestone_3_note.md`
- `expense_management_app/Controllers/ExpensesController.cs`
- `expense_management_app/Controllers/CategoriesController.cs`
- `expense_management_app/Controllers/TagsController.cs`
- `expense_management_app/Infrastructure/Persistence/AppDbContext.cs`
- `expense_management_app/Migrations/20260813072532_AddExpenseManagementModule.cs`

## Features Actually Completed

- VERIFIED: Added expense domain entities:
  - `Expense`
  - `Category`
  - `Tag`
  - `ExpenseTag`
- VERIFIED: Added EF Core DbSets for expenses, categories, tags, and expense-tag join rows.
- VERIFIED: Configured one-to-many relationship from `AppUser` to `Expense`.
- VERIFIED: Configured one-to-many relationship from `AppUser` to `Category`.
- VERIFIED: Configured one-to-many relationship from `AppUser` to `Tag`.
- VERIFIED: Configured optional one-to-many relationship from `Category` to `Expense`.
- VERIFIED: Configured many-to-many expense/tag relationship through explicit `ExpenseTag` join entity.
- VERIFIED: Configured composite key on `ExpenseTag` using `ExpenseId` and `TagId`.
- VERIFIED: Added `POST /categories` and `GET /categories`.
- VERIFIED: Added `POST /tags` and `GET /tags`.
- VERIFIED: Added `POST /expenses`.
- VERIFIED: Added `GET /expenses`.
- VERIFIED: Added `GET /expenses/{expenseId}`.
- VERIFIED: Added `PUT /expenses/{expenseId}`.
- VERIFIED: Added `DELETE /expenses/{expenseId}`.
- VERIFIED: Added user-owned query scoping for expense/category/tag APIs using JWT `sub` claim.
- VERIFIED: Added list filtering by date range, category, merchant substring, min amount, and max amount.
- VERIFIED: Added sorting by amount, merchant, and default expense date.
- VERIFIED: Added page/pageSize pagination with clamping.
- VERIFIED: Added category/tag DTO contracts rather than returning EF entities directly.
- VERIFIED: Added migration `AddExpenseManagementModule`.
- VERIFIED: Manual API testing was reported successful for category/tag creation/listing, expense CRUD, expense list filters/sorting/pagination, and cascade deletion of related `ExpenseTag` rows when deleting an expense.

## Features Partially Completed

- PARTIAL: Automated tests for expense validation and user isolation are not implemented.
- PARTIAL: `POST /expenses` and `PUT /expenses/{expenseId}` include category IDs and tag responses, but category display name in create/update responses depends on whether `expense.Category` is loaded. List and get-by-id project category name through EF query projection.
- PARTIAL: Category and tag management is limited to create/list. Update/delete/get-by-id for categories and tags were intentionally deferred because they are outside the milestone endpoint list.
- PARTIAL: Currency accepts exactly 3 characters by DTO validation, but full normalization/ISO currency validation is minimal.
- PARTIAL: Expense date uses `DateTime`; a later design may switch to `DateOnly` if the domain only needs calendar dates.
- PARTIAL: Error responses use standard status codes but mostly return empty `BadRequest`, `Conflict`, `Unauthorized`, or `NotFound` bodies rather than detailed domain-specific ProblemDetails.

## Architecture And Design Decisions

- VERIFIED: Expense-related entities were grouped under `Models/Expenses`.
- VERIFIED: Identity entities were moved under `Models/Identity`.
- VERIFIED: Explicit join entity `ExpenseTag` was used instead of storing tag IDs directly on `Expense`.
- VERIFIED: `Expense.CategoryId` is nullable so an expense can be uncategorized.
- VERIFIED: `Expense.Notes` is nullable because notes are optional.
- VERIFIED: Collection navigation properties are initialized with `[]`.
- VERIFIED: Required EF navigation references use `= null!` where EF Core owns relationship materialization.
- VERIFIED: API contracts were separated from EF entities under `Contracts/Expenses`.
- VERIFIED: API request DTOs do not accept `AppUserId`; ownership is derived from the authenticated JWT.
- VERIFIED: Server-generated fields such as `CreatedAt` and `UpdatedAt` are assigned server-side.
- VERIFIED: Category and tag names are normalized with `Trim().ToUpperInvariant()` before persistence.
- VERIFIED: Category and tag duplicate prevention is scoped per user via unique indexes on `(AppUserId, Name)`.
- VERIFIED: Expense list endpoint builds an `IQueryable<Expense>` and composes filters before executing with `CountAsync()` and `ToListAsync()`.
- VERIFIED: Expense list counts matching rows before pagination so response can return `TotalCount`.
- VERIFIED: Invalid category filters on read return an empty result because all expense reads are already scoped by `AppUserId`.
- VERIFIED: Invalid category/tag IDs on create/update are rejected because writes would create relationships.
- VERIFIED: Delete returns `404` for missing or non-owned expenses to avoid leaking whether another user's expense exists.

## Alternatives Considered

- VERIFIED: Creating categories/tags inside expense creation was discussed and rejected for this milestone.
  - Reason: It introduces find-or-create logic, normalization decisions, unique constraint race conditions, transaction boundaries, partial failure rules, and API design complexity.
  - Chosen approach: Create categories/tags separately, then reference them by ID in expense create/update requests.
- VERIFIED: Returning EF entities directly from controllers was rejected.
  - Reason: Entities include internal ownership fields and navigation properties such as `AppUserId`, `AppUser`, `ExpenseTags`, etc.
  - Chosen approach: Return DTOs such as `ExpenseResponse`, `CategoryResponse`, and `TagResponse`.
- VERIFIED: Accepting `AppUserId` in request DTOs was rejected.
  - Reason: It would allow clients to spoof ownership.
  - Chosen approach: Derive current user ID from JWT `sub` claim.
- VERIFIED: Loading all expenses into memory before filtering/sorting/pagination was rejected.
  - Reason: It would push database work into C# memory and scale poorly.
  - Chosen approach: Compose `IQueryable` so PostgreSQL performs filtering, sorting, and pagination.
- VERIFIED: Dynamic arbitrary sort input was rejected.
  - Reason: It is brittle and exposes more internal surface than needed.
  - Chosen approach: Explicit switch over supported sort keys.
- VERIFIED: Category deletion behavior was configured as `SetNull` rather than cascade-delete expenses.
  - Reason: Deleting a category should not delete historical financial records.

## Problems And Bugs Encountered

- VERIFIED: JSON request parsing failed when `.http` file requests were not separated with `###`.
  - Symptom: `400 Bad Request` with message similar to `"'G' is invalid after a single JSON value"`.
  - Resolution: Separate HTTP requests with `###` and ensure valid JSON bodies.
- VERIFIED: Expired or invalid JWT produced `401 Unauthorized`.
  - Resolution: Re-login and use a fresh access token.
- VERIFIED: `DateTime` save failed for `expenseDate` when JSON used date-only format.
  - Symptom: `Cannot write DateTime with Kind=Unspecified to PostgreSQL type 'timestamp with time zone', only UTC is supported`.
  - Resolution for testing: Send `expenseDate` as UTC ISO value, e.g. `2026-08-10T00:00:00Z`.
  - Remaining design consideration: `DateOnly` may be a better domain type later.
- VERIFIED: Initial delete implementation queried expense by ID only.
  - Risk: A user could delete another user's expense if they guessed the ID.
  - Resolution: Query by both `expense.Id == expenseId` and `expense.AppUserId == userId`.
- VERIFIED: Initial update attempt created a new `Expense` object instead of mutating the tracked entity.
  - Risk: No meaningful update would be applied to the loaded expense.
  - Resolution: Load the existing expense, mutate scalar fields, clear/rebuild `ExpenseTags`, then save.
- VERIFIED: Initial many-to-many response handling omitted tags.
  - Resolution: Fetch valid user-owned tags into `TagResponse` objects and use them both for validation and response mapping.
- VERIFIED: Attempting to access `expense.Category.Name` caused nullable reference warning.
  - Cause: Category is optional.
  - Resolution: Use null check when projecting `CategoryName`.

## Debugging And Resolution Approach

- VERIFIED: Used compiler/build errors to identify incomplete controller paths and invalid code.
- VERIFIED: Used EF Core exception details from logs to diagnose PostgreSQL `DateTime` UTC issue.
- VERIFIED: Used manual HTTP requests to validate endpoint behavior.
- VERIFIED: Verified that deleting an expense also removed corresponding `ExpenseTag` rows.
- VERIFIED: Used ownership checks consistently after identifying risk in delete-by-id-only query.
- VERIFIED: Reworked tag validation from count-only validation to fetching `TagResponse` records so the same query could support both validation and response shaping.

## Technical Concepts Learned Or Demonstrated

- VERIFIED: Foreign key property vs navigation property.
- VERIFIED: `required` vs nullable `?` vs null-forgiving `= null!` vs collection initialization `= []`.
- VERIFIED: One-to-many relationships in EF Core.
- VERIFIED: Many-to-many relationship using explicit join entity.
- VERIFIED: Composite keys for join tables.
- VERIFIED: Delete behavior differences: cascade vs set-null.
- VERIFIED: DTOs vs EF entities.
- VERIFIED: User ownership derived from JWT claims.
- VERIFIED: `[Authorize]` on user-owned controllers.
- VERIFIED: `[FromRoute]`, `[FromQuery]`, and `[FromBody]` usage.
- VERIFIED: `AnyAsync`, `Where`, `CountAsync`, `FirstOrDefaultAsync`, and `ToListAsync`.
- VERIFIED: `IQueryable` as a composable query before execution.
- VERIFIED: Projection with `Select` into response DTOs.
- VERIFIED: Filtering, sorting, and pagination sequence.
- VERIFIED: `Distinct()` for deduplicating tag IDs before creating join rows.
- VERIFIED: `List.Contains(value)` translation to SQL `IN`.
- VERIFIED: `string.Contains(text)` as substring filtering.
- VERIFIED: Validation of optional fields only when supplied.
- VERIFIED: Server-side timestamp assignment.
- VERIFIED: Date/time UTC behavior with PostgreSQL/Npgsql.

## Code And Components Implemented By Hayden

- VERIFIED: Created expense-related EF entities under `expense_management_app/Models/Expenses`.
- VERIFIED: Added/updated EF Core configuration in `AppDbContext`.
- VERIFIED: Added expense request/response contracts under `expense_management_app/Contracts/Expenses`.
- VERIFIED: Implemented category controller endpoints for create and list.
- VERIFIED: Implemented tag controller endpoints for create and list.
- VERIFIED: Implemented expense create/list/get/update/delete controller endpoints.
- VERIFIED: Added explanatory learning comments throughout controllers and milestone notes.
- VERIFIED: Ran manual endpoint testing and observed/validated database behavior.

## Areas Where Codex Provided Substantial Guidance

- VERIFIED: Explained domain model and EF relationships for `Expense`, `Category`, `Tag`, and `ExpenseTag`.
- VERIFIED: Guided the choice to use `Models/Expenses` while preserving or later grouping identity models.
- VERIFIED: Explained why API request DTOs should not accept `AppUserId`, `CreatedAt`, or `UpdatedAt`.
- VERIFIED: Guided request/response DTO shapes for expense/category/tag endpoints.
- VERIFIED: Explained and guided implementation of many-to-many tag validation and mapping.
- VERIFIED: Explained `IQueryable` query composition, deferred execution, and database-side filtering.
- VERIFIED: Guided pagination, sorting, and filtering implementation.
- VERIFIED: Explained route/query/body binding choices.
- VERIFIED: Identified ownership bug in delete endpoint.
- VERIFIED: Guided `PUT` update approach for replacing `ExpenseTag` join rows.
- VERIFIED: Diagnosed JSON parsing and DateTime/Npgsql testing issues from observed errors.
- VERIFIED: Recommended deferring category/tag update/delete and automated tests to keep milestone scope controlled.

## Work Not Fully Claimable As Independently Designed

- VERIFIED: Codex provided substantial design guidance for the EF relationship model and DTO boundaries.
- VERIFIED: Codex provided the conceptual algorithm for expense list filtering/sorting/pagination.
- VERIFIED: Codex provided the conceptual pattern for tag ID validation, tag response mapping, and `ExpenseTag` join row creation.
- VERIFIED: Codex provided the conceptual pattern for update tag replacement using loaded `ExpenseTags`, `Clear()`, and re-add.
- VERIFIED: Codex provided the explanation and suggested fix for `DateTime` UTC/Npgsql behavior.
- PARTIAL: Hayden typed, adapted, tested, and commented much of the implementation, but some controller patterns were strongly mentored and should not be represented as entirely independent system design.

## Tests, Validation, Security, And Error Handling

- VERIFIED: Manual testing completed successfully for milestone 3 workflows.
- VERIFIED: `dotnet build` passed during development checks.
- VERIFIED: `POST /categories` and `POST /tags` reject duplicates per user with `Conflict`.
- VERIFIED: Expense create/update validate category ownership if `CategoryId` is supplied.
- VERIFIED: Expense create/update validate all supplied tag IDs belong to current user.
- VERIFIED: Expense reads, updates, and deletes are scoped by authenticated user ID.
- VERIFIED: Expense list supports pagination bounds: page minimum 1, page size clamped 1-100.
- VERIFIED: Missing or invalid JWT subject claim returns `Unauthorized`.
- VERIFIED: Missing/non-owned expense returns `NotFound`.
- VERIFIED: Invalid category/tag relationship input returns `BadRequest`.
- PARTIAL: Request validation uses data annotations for DTO shape and positive amount checks, but no custom validation layer has been added.
- PARTIAL: No automated unit or integration tests exist for milestone 3.
- PARTIAL: No concurrency tests exist for category/tag uniqueness races.

## Refactors And Why They Happened

- VERIFIED: Identity models were moved under `Models/Identity`.
  - Reason: Separate identity objects from expense module objects.
- VERIFIED: Expense models were grouped under `Models/Expenses`.
  - Reason: Keep domain ownership visible as the modular monolith grows.
- VERIFIED: Tag validation changed from `CountAsync` only to fetching `TagResponse` DTOs.
  - Reason: Use one query result for both validation and API response.
- VERIFIED: `DELETE /expenses/{expenseId}` changed from ID-only lookup to ID + user lookup.
  - Reason: Enforce user isolation.
- VERIFIED: `PUT /expenses/{expenseId}` changed from creating a new `Expense` object to mutating the tracked loaded entity.
  - Reason: Correct EF Core update behavior.
- VERIFIED: Tag replacement in PUT changed to load existing `ExpenseTags`, clear them, and add new join rows.
  - Reason: PUT request represents replacement of final tag set.
- VERIFIED: `TagsController` route changed to plural `/tags`.
  - Reason: Match milestone API route list.

## Remaining Technical Debt And Unfinished Work

- VERIFIED: Automated tests for expense rules and user isolation are not implemented.
- VERIFIED: Milestone 2 auth tests are also deferred.
- VERIFIED: Category/tag update, delete, and get-by-id endpoints are not implemented.
- PARTIAL: Current controllers duplicate current-user claim parsing helper.
  - Possible future refactor: `ICurrentUserService` or base controller.
- PARTIAL: Currency normalization is minimal and not backed by a currency whitelist.
- PARTIAL: Category/tag names are stored uppercase only; original display casing is not preserved.
- PARTIAL: Some comments contain typos and learning-oriented verbosity.
- PARTIAL: `ExpenseDate` as `DateTime` caused UTC-kind issue; `DateOnly` may better fit the domain later.
- PARTIAL: CategoryName in create/update responses may require explicit query/projection if consistent category display is required.
- PARTIAL: No transaction boundary was explicitly added around multi-step create/update.
  - Current EF `SaveChangesAsync` provides a transaction for the save operation, but pre-save validation queries are separate.
- PARTIAL: No uniqueness conflict handling for simultaneous category/tag creation requests beyond database unique indexes.
- VERIFIED: Current repository has started receipt module work and migration after milestone 3; it should be excluded from this milestone handoff unless separately audited.

## Evidence Files

- `documents/milestone_3_note.md`
- `expense_management_app/Models/Expenses/Expense.cs`
- `expense_management_app/Models/Expenses/Category.cs`
- `expense_management_app/Models/Expenses/Tag.cs`
- `expense_management_app/Models/Expenses/ExpenseTag.cs`
- `expense_management_app/Contracts/Expenses/Requests/CreateExpenseRequest.cs`
- `expense_management_app/Contracts/Expenses/Requests/UpdateExpenseRequest.cs`
- `expense_management_app/Contracts/Expenses/Requests/CreateCategoryRequest.cs`
- `expense_management_app/Contracts/Expenses/Requests/CreateTagRequest.cs`
- `expense_management_app/Contracts/Expenses/Responses/ExpenseResponse.cs`
- `expense_management_app/Contracts/Expenses/Responses/ExpenseListResponse.cs`
- `expense_management_app/Contracts/Expenses/Responses/CategoryResponse.cs`
- `expense_management_app/Contracts/Expenses/Responses/TagResponse.cs`
- `expense_management_app/Contracts/Expenses/ExpenseQueryParameters.cs`
- `expense_management_app/Controllers/ExpensesController.cs`
- `expense_management_app/Controllers/CategoriesController.cs`
- `expense_management_app/Controllers/TagsController.cs`
- `expense_management_app/Infrastructure/Persistence/AppDbContext.cs`
- `expense_management_app/Migrations/20260813072532_AddExpenseManagementModule.cs`
- `expense_management_app/Migrations/20260813072532_AddExpenseManagementModule.Designer.cs`

## Verification Commands And Results

- VERIFIED: `dotnet build expense_management_app\expense_management_app.csproj --no-restore` succeeded during the audit pass.
- VERIFIED: Manual HTTP testing was reported successful by the developer for milestone 3 endpoints.
- UNVERIFIED: No automated test command exists for milestone 3 because tests are deferred.

## Exclusions

- VERIFIED: Receipt upload/module work is not part of milestone 3, even though current repository state includes receipt model/configuration and an `AddReceiptUploadModule` migration.
- VERIFIED: OCR, object storage, background processing, AI parsing, budgeting, analytics, notifications, and deployment are outside this milestone.
