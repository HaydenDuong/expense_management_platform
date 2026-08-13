# Milestone 3 - Expense Management Module

## Goal

Allow authenticated users to manually create, update, search, and organize expenses before introducing receipt upload and AI automation.

## Scope

This milestone focuses on user-owned expense records. Receipt upload, OCR, AI parsing, and budgeting are handled in later milestones.

## Learning Objectives

- Domain modeling
- User-owned data access
- CRUD API design
- Pagination, filtering, and sorting
- EF Core relationships
- Database indexes
- Transaction boundaries

## Business Requirements

- A user can create an expense manually.
- A user can view only their own expenses.
- A user can update or delete their own expenses.
- A user can categorize expenses.
- A user can tag expenses.
- A user can filter expenses by date, category, merchant, and amount.
- A user can sort expenses by date, amount, or merchant.
- Expense amounts must be positive.
- Expense dates must be valid.

## API Endpoints

- [x] `POST /expenses`
- [x] `GET /expenses`
- [x] `GET /expenses/{id}`
- [x] `PUT /expenses/{id}`
- [x] `DELETE /expenses/{id}`
- [x] `GET /categories`
- [x] `POST /categories`
- [x] `GET /tags`
- [x] `POST /tags`

## Data Model

Expense
    - Id
    - AppUserId
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

## Tasks

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

## Definition of Done

- Authenticated users can manage their own expenses.
- Users cannot access another user's expenses.
- Expense list supports pagination, filtering, and sorting.
- Categories and tags can be assigned to expenses.
- Core expense validation is covered by tests.

## Step-by-step

Mental note:
    Foreign key property = database link
    Navigation property = C# object link

### Create Entities

#### Create Expense Entity

Real-life example:
    Expense:
        Merchant: Woolworths
        Amount: 42.50
        Curency: AUD
        Category: Groceries
        Tags: food, weekly-shop, household
    In code / database terms:
        Expense
            -> Belongs to one Category
            -> Has many Tags

#### Create Category Entity

Categories

    Id | AppUserId | Name
    ---|-----------|----------
    1  | 7         | Groceries
    2  | 7         | Transport

Expenses

    Id | AppUserId | Merchant   | Amount | CategoryId
    ---|-----------|------------|--------|-----------
    10 | 7         | Woolworths | 42.50  | 1

This means: CategoryId = 1 => Expense 10 belongs to Category 1
                              Expense 10 belongs to Groceries

Both are needed, because:
    CategoryId = the actual database column
    Category = The C# navigattion property EF Core can load when we want the full "category" object

So:
    expense.CategoryId => Gives "1"
    expense.Category?.Name => Gives "Groceries"

Thus:
    Category is one-to-many relation with expense because:
        one expense -> 0 or 1 category
        one category -> many expenses

#### Create Tag Entity

Real-life examples:
    Expense 10: Woolworths
    Tags: food, weekly-shop

    Expense 11: Coles
    Tags: food

    Expense 12: Uber
    Tags: transport, work

So the relationship is: Expense has many-to-many relationship with tags because
    one expense -> many tags
    one tag -> many expenses

Thus, a single "TagId" column on "Expense" table would not work, because an expense can have multiple tags

    => This is why we need a join table & a tags table:

        ExpenseTags

            ExpenseId | TagId
            ----------|------
            10        | 1
            10        | 2
            11        | 1
            12        | 3
            12        | 4

        Tags

            Id | AppUserId | Name
            ---|-----------|------------
            1  | 7         | food
            2  | 7         | weekly-shop
            3  | 7         | transport
            4  | 7         | work
        
        Expense 10 has Tag 1 & Tag 2
        Tag 1 is used by Expense 10 and Expense 11
    
    => Hence: public List<ExpenseTag> ExpenseTags { get; set; } = [];

However, this does not mean the databate stores a list inside the expense row. PostGreSQL is still relational
    => This means EF Core can understand:
        Expense -> ExpenseTags -> Tags

    So later we can query: var expense = await _context.Expenses
                                    .Include(expense => expense.Category)
                                    .Include(expense => expense.ExpenseTags)
                                        .ThenInclude(expenseTag => expenseTag.Tag)
                                    .FirstOrDefaultAsync(expense => expense.Id == id);

    And the C# object graph becomes:
        Expense
            Merchant: Woolworths
            Category:
                Name: Groceries
            ExpenseTags:
                - Tag:
                    Name: food
                - Tag:
                    Name: weekly-shop

#### Why include both "Category" and "Tag" as properties for "Expense" object?

Without them, EF only knows the raw rows, not the relationship

    public int? CategoryId { get; set; } => Lets EF store the relationship

    public Category? Category { get; set; } => Lets C# code navigate the relationship

    public List<ExpenseTag> ExpenseTags { get; set; } = [] => Lets C# code navigate many tags

#### "Required" vs. "= null!" vs. "= []"

"required" for scalar values that your app must provide when creating the object
    public required string Name { get; set; }
    public required string Currency { get; set; }

"= null!" for EF navigation references:
    public AppUser AppUser { get; set; } = null!;

    This means:
        At runtime, the value is initially null.
        But compiler, please do not warn me about it.
        I know EF Core will populate this navigation property when appropriate.
    
    The "!" is called: The null-forgiving operator => This is null, but sppress nullable warning
        Does not make the property safer, but suppress nullable warning => Only silence the compiler
    
    Commonly use for EF navigation properties, because:

        var expense = new Expense
            {
                AppUserId = currentUserId,
                Merchant = "Woolworths",
                Amount = 42.50m,
                Currency = "AUD",
                ExpenseDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        
        We provide "AppUserId" but not "AppUser" = this is valid because the database relationship is stored through "AppUserId"

        Later, if we query with: ".Include(expense => expense.AppUser)" => EF fills in that "expense.AppUser"

        Thus:
            Foreign key ID (e.g AppUserId): needed to save relationship
            Navigation object (e.g AppUser): useful when loading related data

        Use this when:
            EF Core owns/populates this required navigation property,
            but your application often creates the entity using only the foreign key ID.

"= []" for EF navigation collections:
    public List<ExpenseTag> ExpenseTags { get; set;} = [];

#### Future Consideration

Implement the update endpoints for both Categories & Tags

### Create EF Core relationships

Configure each relationship once, usually from the dependent side.

The dependent side is the table with the foreign key.
    e.g: Expense.CategoryId => "Expense" is the dependent side.

When two things are many-to-many, it common to introduce a third object that represent the join / relationship entity of the formers.
    Expense many-to-many Tag

        becomes:

            Expense one-to-many ExpenseTag
            Tag     one-to-many ExpenseTag

Instead of trying to store tags in a list like: Expense.Tags = [food, groceries, work], we can store relationship rows in the database as:

        ExpenseTags

            ExpenseId | TagId
            ----------|------
            10        | 1
            10        | 2
            10        | 3

        Where each row means Expense 10 has that tag.

Further more, relational database are best at rows and relationships => storing values in a string / list like "Tags = 'food,groceries,work' will be painful when filtering like:
    Find all expenses tagged food
    Find all users who used tag work
    Prevent duplicate tag assignment
    Rename tag food to Food
    Delete a tag safely

Thus, a join table is a better solution.

### Add expense CRUD endpoints

Current Approach:
    Create reusable: categories & tags first                                                        => POST /categories & POST /tags
    CreateExpensesRequest with used those stored Id to included within the request fields           => POST /expenses with categoryId and tagId
    This will teach:
        resources
        foreign keys
        many-to-many joins
        ownership checks
        basic validation

Later Approach:
    Allow the category and tag creation during CreateExpenseRequest, where this will teach:
        find-or-create logic
        normalization
        transaction boundaries
        unique constraints
        race conditions
        partial failure decisions
        API design tradeoffs

Production-shaped flow:
    1. Normalize tag name.
    2. Look up existing tags for this user.
    3. Create missing tags.
    4. If unique conflict happens, re-query.
    5. Create expense.
    6. Create ExpenseTag rows.
    7. Save all inside a transaction.

Created: /Contracts/Expenses

    Requests:
        CreateExpenseRequest.cs
        UpdateExpenseRequest.cs

    Responses:
        ExpenseResponse.cs
        ExpenseListResponse.cs
        TagResponse.cs

    ExpenseQueryParameter.cs for GET /expenses

Created: /Controllers

    CategoriesController.cs

    TagsController.cs

    ExpensesController.cs

        [HttpPost]

            Explain for the valid count tags in the Database for check the validity of TagIds:
            
                var validTagCount = await _context.Tags
                    .CountAsync(tag =>
                        tag.AppUserId == userId &&
                        distinctTagIds.Contains(tag.Id));

                This asks the database: How many tags exist where:
                    - tag belongs to the current user
                    - tag's Id is one of the IDs the client requested
                
                example:

                The above command syntax is equivalent to:
                    SELECT COUNT(*)
                    FROM Tags
                    WHERE AppUserId = userId  (e.g: 7)
                    AND Id IN distinctTagIds; (e.g: (1, 2, 5))

                Id | AppUserId | Name
                ---|-----------|---------
                1  | 7         | WORK
                2  | 7         | LUNCH
                3  | 7         | TAX
                5  | 8         | PRIVATE

                The query above will check as:
                    Id 1: belongs to user 7 and id is requested -> valid
                    Id 2: belongs to user 7 and id is requested -> valid
                    Id 3: belongs to user 7 but id is not requested -> ignored
                    Id 5: id is requested but belongs to user 8 -> not valid
                
                So the validTagCount = 2 != distinctTagIds.Count = 3 => One requested tag is invalid / belongs to someone else which is 5 in this case
                
                Thus, distinctTagIds.Contains(tag.Id) == only look at database tags whose ID was requested by the client.

            Explain for the following code:

                ExpenseTags = distinctTagIds
                    .Select(tagId => new ExpenseTag
                    {
                        TagId = tagId
                    })
                    .ToList()  
                
                Purpose: converting a list of Tag Ids stored in distinctTagIds into a list of ExpenseTag join objects

                A.k.a: for each tagId in distinctTagIds, create a new ExpenseTag object

                Example:

                    distinctTagIds = [1, 2, 5] will be transformed into:

                    [
                        new ExpenseTag { TagId = 1},
                        new ExpenseTag { TagId = 2},
                        new ExpenseTag { TagId = 5}
                    ]

                    .ToList() will turn the result above into a real List<ExpenseTag>

                    ExpenseTags = new List<ExpenseTag>
                    {
                        new ExpenseTag { TagId = 1},
                        new ExpenseTag { TagId = 2},
                        new ExpenseTag { TagId = 5}
                    }

                    Because "Expense" entity does not store tags directly as integers, it stores relationship through: "public List<ExpenseTag> ExpenseTags { get; set; } = [];
                        and "ExpenseTag" represents one database row in the join table:

                    ExpenseId | TagId
                    ----------|------
                    10        | 1
                    10        | 2
                    10        | 5

                    Only "TagId" is set here because the current "Expense" object is not exist in the database yet until:
                        _context.Expense.Add(expense);
                        await _context.SaveChangesAsync();
                    
                    Only after the above code lines, then EF Core sees:
                        New Expense entity
                        With 3 new ExpenseTag children
                    
                    EF Core will do the following:

                        1. Insert expense:
                            Merchant = Sushi Hub
                            Amount = 18.90

                        2. Database generates:
                            Expense.Id = 10

                        3. Insert join rows:
                            ExpenseId = 10, TagId = 1
                            ExpenseId = 10, TagId = 2
                            ExpenseId = 10, TagId = 5
                    
                    Eventhough, we did not include this code line: "ExpenseId = expense.Id" - EF can fill it because the "ExpenseTags" are attached to the new created "Expense" object

                    A small analogy:
                        You create a new parent object: Expense
                        You attach child relationship objects: ExpenseTags
                        EF saves the parent first
                        Then EF saves the children with the parent's generated ID
            
            In the current update (Aug-08-26): var validTagCount is replaced with var "tagResponse"

                var tagResponses = await _context.Tags
                    .Where(tag =>
                        tag.AppUserId == userId &&
                        distinctTagIds.Contains(tag.Id))
                    .Select(tag => new TagResponse
                    {
                        Id = tag.Id,
                        Name = tag.Name
                    })
                    .ToListAsync();
                
                Purposes:
                    1. Validation:
                        If count does not match, at least one tag id was invalid.

                    2. Response:
                        It already has Id and Name ready to return.
                
                Code explain:
                    Request:
                        {
                            "merchant": "Sushi Hub",
                            "amount": 18.90,
                            "currency": "AUD",
                            "expenseDate": "2026-08-08",
                            "tagIds": [1, 2]
                        }

                    Database Tags:
                        Id | AppUserId | Name
                        ---|-----------|----------
                        1  | 7         | WORK
                        2  | 7         | LUNCH
                    
                    Query result:
                        tagResponses = [
                            new TagResponse { Id = 1, Name = "WORK" },
                            new TagResponse { Id = 2, Name = "LUNCH" }
                        ];
                    
                    Response:
                        {
                            "id": 10,
                            "merchant": "Sushi Hub",
                            "amount": 18.90,
                            "currency": "AUD",
                            "expenseDate": "2026-08-08T00:00:00",
                            "categoryId": null,
                            "notes": null,
                            "createdAt": "...",
                            "updatedAt": "...",
                            "tags": [
                                { "id": 1, "name": "WORK" },
                                { "id": 2, "name": "LUNCH" }
                            ]
                        }

        [HttpGet] - this is a pipeline of:

            1. Start with all expenses for the current user.
            2. Apply filters if the client provided them.
                a. Date Filters [FromDate to ToDate]
                b. CategoryId Filter
                c. Merchant Filter
                d. Amount Filters
            3. Count how many rows match before pagination.
            4. Apply sorting.
            5. Apply pagination.
                Page tells which group.
                PageSize tells how large each group is.
                Skip jumps past earlier groups.
                Take returns the current group.

                a.k.a
                    pageSize = amount per page
                    page = page number
                    skip = how many previous pages worth of items to jump over
            6. Project selected entities into ExpenseResponse DTOs
            7. Return ExpenseListResponse
        
        Additional note:
            Read filter with invalid category => empty result is acceptable.
            Write using invalid category => reject with BadRequest()
        
        [HttpDelete("{ExpenseId}")]
            1. Get current userId from JWT access code and validate it.
            2. Find the expense from this user based on the input "ExpenseId"
            3. Delete if Found else return NotFound()
            4. Save change.
        
        [HttpPost("{ExpenseId}")] - it has optional "CategoryId" & "TagIds"
            Find current user's expense, including ExpenseTags
                If missing -> NotFound

                If CategoryId is supplied:
                    Check category belongs to current user
                    If not -> BadRequest

                    Note: AnyAsync() return bool - purpose: Does at least one matching row exist? - Use it when we only care about existence
                          .Where(...) return a query IQueryable(Category) - a database query that could return matching categories later
                                => No SQL executed yet => No rows loaded yet until ...sync() is executed 

                If CategoryId is null:
                    Allow uncategorized expense

                Deduplicate TagIds
                    Check all tags belong to current user
                    If mismatch -> BadRequest

                Update scalar fields:
                    Merchant
                    Amount
                    Currency
                    ExpenseDate
                    CategoryId
                    Notes
                    UpdatedAt

                Replace tag relationships:
                    Clear existing ExpenseTags
                    Add new ExpenseTag rows from TagIds

                Save changes

                Return ExpenseResponse

### Add pagination

### Add filtering

### Add sorting

### Add ownership checks

### Add indexes for common queries

Index the columns that you commonly use in:
    WHERE
    ORDER BY
    JOIN
    UNIQUE business rule

### Add tests for expense rules and user isolation

### Syntax Command for Data Migration & Database Update

    "migration add" - create C# migration file from model changes.

        dotnet ef migrations add AddExpenseManagementModule --project expense_management_app

    "database update" - apply migration to PostgreSQL

        dotnet ef database update --project expense_management_app
    
    Note: it is important where the terminal to run these two commands from:

        If it is currently at the folder level where .csproj is presenting => --project expense_management_app is optional

        Else, it must be included because EF Core not sure where is the .csproj => Can't peforming migration & database update

        Remember: --project name-of-the-folder-where-.csproj-present
        