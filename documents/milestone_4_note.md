# Milestone 4 - Receipt Upload Module

## Goal

    Allow users to securely upload receipts (images / PDFs) and register them for processing.

## Learning Objectives

    - Multipart Form Upload.
    - File Streaming.
    - File Validation.
    - Object Metadata.
    - Secure File Handling.
    - File Size Limitation.
    - MIME Type Validation.

## Business Requirements

    - User uploads receipt.
    - Receipt belongs to one user.
    - Receipt can be image or PDF.
    - Receipt status starts as "Pending".
    - Receipt has upload timestamp.
    - Receipt stores original filename.
    - Receipt stores storage path.
    - Receipt can be deleted before processing.

## Task

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
    - [ ] Reject corrupted uploads

    Database:
        - [x]Receipt
                Id
                UserId
                Status
                OriginalFileName
                StorageKey
                ContentType
                ContentHash
                FileSize
                CreatedAt
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

## Definition of Done

    - Upload feature works.
    - Receipt metadate saved.
    - File stored successfully.
    - Validation working.

## Step-by-step implementation

### Create Receipt object

    Current model design:
        Id
        AppUserId
        AppUser
        Status - Enum is used instead of string.
        OriginalFileName
        StorageKey - Current local path - Future will be an "object key where the upload receipt file can be found"
        ContentType
        ContentHash - hashing the file content with SHA-256 will produce a 32 bytes where each byte ~ 2 hex characters => HasMaxLength(64)
        FileSize
        CreatedAt
        UpdatedAt - This will be updated based on the processing state of the receipt (Status change)
    
    Created: /Models/Receipts/ReceiptStatus.cs
                
                Explanation why Enum was used instead of string:
                    
                    With a string, C# will allows anything as follow:

                        receipt.Status = "Pending";
                        receipt.Status = "pending";
                        receipt.Status = "PENDING";
                        receipt.Status = "Pendng";
                        receipt.Status = "banana";
                        receipt.Status = "";
                    
                        Because these are string-type => compiler does not care => The app can accidentally save invalid business states into the database, for example, "pendng" instead of "pending"

                            => Other parts of the system will be affected because of that:

                                if (receipt.Status == "Pending")        // Can't process further because receipt.Status is saved as "pendng" but not as "Pending"
                                {
                                    // process receipt
                                }
                    
                    With an enum, we define the allowed states (standard):

                        public enum ReceiptStatus
                        {
                            Pending,
                            Processing,
                            Completed,
                            Failed,
                            Deleted
                        }

                        then: with public ReceiptStatus status { get; set; }, C# will allow: 

                            receipt.Status = ReceiptStatus.Pending;
            
            /Models/Receipts/Receipt.cs

                Again: Difference between "required" vs. "= null!"

                    "= null!" - "Trust me, it will be set later."
                              
                              - Userful for EF navigation properties & framework-populated values
                                    
                                    e.g: public AppUser AppUser { get; set; } = null!;
                            
                        => Allow the creation of a "receipt" with just: 

                            var receipt = new Receipt();

                            However, if field like "StorageKey" is declared as public string StorageKey { get; set; } = null!

                                => Will blow up if we do: receipt.StorageKey.Length but
                    
                    "required" - Useful for required domain data / your app must know this immediately => "Whoever creates a Receipt must provide this value"
                                                                
                                                                 Or "This object is incomplete unless you provide this required value"

                        => C# pushes toward the following for creating a "receipt" object:

                            var receipt = new Receipt
                            {
                                StorageKey = "...",
                                OriginalFileName = "...",
                                ContentType = "application/pdf",
                                // other required fields...
                            }
    
    Updated: /Models/Identity/AppUser.cs

                Added: public List<Receipt> Receipts { get; set; } = [];
            
            /Infrastructure/Persistence/AppDbContext.cs
                
                Added: "Receipt" section

                Lessons:

                    Index fields = you search / filter / sort by.

                        Index is useful when the database often searches, filters, sorts, or enforces uniqueness on a field.

                        For this app's index-logic:

                            Find all receipts for current user ordered by upload date

                            Find all pending receipts for processing

                            Find one receipt by ReceiptId & UserId

                            => Thus, better indexes than using "OriginalFileName" are:

                                    (AppUserId, CreatedAt)

                                    (AppUserId, Status)

                                    (AppUserId, ContentHash) unique

                                        entity.HasIndex(receipt => new
                                        {
                                            receipt.AppUserId,
                                            receipt.ContentHash
                                        })
                                            .IsUnique();        => This enforces "One user cannot upload the exact same file content twice".
                        
                        => Before adding an index, ask: "What query or rule is this index supporting?"

                        => Before adding uniqueness, ask "Is this value truly unique in the real world?"

                                Thus, "OriginalFileName" is not, but a combination of "UserId + ContentHash of the upload file" is.

                        However, indexes are not free - because:

                            They make reads faster - but writes slightly slower as insert a new receipt, PostgreSQL must update:

                                Receipt Table
                                
                                AppUserId + CreatedAt index

                                AppUserId + Status index

                                AppUserId + ContentHash index
                        
                        Production habit:

                            Do not index everything.
                            
                            Index based on real query patterns and real uniqueness rules.

                    
                    User unique indexes only when the business rule is truly unique

                        "OriginalFileName" is not truly unqiue in this case - because two different files can share the same name, e.g:

                                receipt.pdf = Woolworth receipt, $18.20
                                
                                receipt.pdf = Coles receipt, $50.00
                        
                        The same file can have different names like:

                                woolworths.pdf

                                receipt-copy.pdf

                                tax-2026-food.pdf
                        
                        Thus, filename is useful for display - but weak for identity

                        If we consider the "bytes" part of the upload files, then:

                                File A bytes:
                                10110100 01010110 11100001 ...

                                File B bytes:
                                10110100 01010110 11100001 ...
                        
                                If every byte is identical => these two files are the same content, even if the names differ. Else, they are different
                        
                        To detect the byte, systems calculate a hash, often SHA-256: file bytes -> SHA-256 -> fixed fingerprint, e.g:

                            Same hash means the file content is almost certainly identical:

                                    woolworths.pdf       -> a84c9f...

                                    receipt-copy.pdf     -> a84c9f...
                                
                            Different hash means different bytes:

                                    receipt.pdf          -> a84c9f...

                                    another-receipt.pdf  -> 91bd02...
                        
                        Note: We should allow User A and User B may both upload the same receipt / shared invoiced => The business rule should be: One user should not upload the exact same file twice

                            => The unique index would be: AppUserId + ContentHash (Same User + Same Hash)
                    
                    Production Products tend to separate 3 concerns:

                        Display name:
                            OriginalFileName - used for showing the user what they uploaded.

                        Storage identity:
                            StorageKey - used for finding the file in S3/MinIO/local storage.

                        Content identity:
                            ContentHash - used for duplicate detection, integrity checks, caching, or security scanning.
                        
                        Example record:

                            OriginalFileName: "receipt.pdf"
                            StorageKey: "receipts/user-42/2026/08/01J8KZ9A3.pdf"
                            ContentHash: "a84c9f0d..."
                            ContentType: "application/pdf"
                            FileSize: 248193
                        
                            StorageKey does not trust the user's original filename, because user-provided filenames can be messy or unsafe as follow:
                                ../../../some-file.txt
                                my receipt final FINAL (2).pdf
                                résumé receipt.pdf
                                receipt<script>.pdf
                        
                        Some production systems often anitize the filename for display, but generate their own StorageKey by using something like below:

                            receipts/{userId}/{year}/{month}/{guid}.pdf

                            e.g: receipts/42/2026/08/6f7a91e4-72d2-4a3e-a0dd.pdf

                            This helps avoiding collisions and avoids trusting user input.
    
    Created data migration: dotnet ef migrations add AddReceiptUploadModule
    
    Applied this migration: dotnet ef database update

### API Design

#### Create API - Request &  - Response (DTOs) for Receipt Object

    Added: /Contracts/Receipts/Requests/CreateReceiptRequest.cs

           /Contracts/Receipts/Responses/ReceiptResponse.cs
           /Contracts/Receipts/Responses/ReceiptListResponse.cs
           /Contracts/Receipts/ReceiptQueryParameters.cs

                public ReceiptStatus? Status { get; set; } instead of just ReceiptStatus Status, because:

                    Non-nullable enum defaults to 0, but the current design of ReceiptStatus starts at 1 (Pending = 1)

                    So, if the client does provide "status" value in the query, then the query object still has: Status = 0

                        => This makes it hard to known whether the client requrested status filtering.
                        
#### GET /receipts

    Similar to ExpensesController.cs GET /receipts

#### GET /receipts/{id}

    Similar to ExpensesController.cs GET /receipts/{id}

#### POST /receipts

    This endpoint is a pipeline, that:

        HTTP multipart upload
                ↓
        Get current user
                ↓
        Validate file exists
                ↓
        Validate file size
                ↓
        Validate extension
                ↓
        Validate MIME type
                ↓
        Generate storage key
                ↓
        Calculate content hash
                ↓
        Check duplicate hash for this user
                ↓
        Save file bytes
                ↓
        Save metadata row
                ↓
        Return ReceiptResponse
    
    Why do we need both file exist validation checks ( "CreateReceiptRequest.cs" - [Required] & manual logic check in "ReceiptsController.cs" - Http POST)?

        [Required] belongs to model validation, or, validates the request shape.
        ~ means: "The request contract requires a file field".
            
            ASP.NET Core can inspect the request and say: "The form did not include file."

            However, this alone is not enough for file upload business validation, because:

                A client could send a "file" field that technically exists but has no bytes:

                    file exists

                    file length = 0
                
        Thus, the manual validation checks the actual runtime object 
        ~ means: enforces the business rule which the uploaded file must exist and contain bytes => no byte == BadRequest()

        Moreover, attributes like [Required] is generic. The manual checks are domain-specific.
            
            [Required] depends on ASP.NET's model binding and validation pipeline.

            Manual validation is explicit and local => must be included for security-sensitive / file-upload code.
                Because the failure mode is obvious when reading the action
    
    Why do we need both file size validation checks ([RequestSizeLimit(10 * 1024 * 1024)] vs. manual check)?

        [RequestSizeLimit] protects the HTTP request pipeline ~ an infrastructure guardrail that prevents oversized HTTP request from being accepted by the server. => Protect server resources early.

            ~ means: it tells ASP.NET to not allow the HTTP request-body size to exceed 10 MB
            ~ Help: protect the server from large request bodies
        
        Manual Check = check for actual file size != the while HTTP request body ~ an application-level rule that validates the uploaded receipt file itself. => Keeps the domain rule explicit and testable.

            ~ means: this check want to confirm if the receipt file is larger than 10 MB or not
        
        Note:
            Http Request Size = entire HTTP multipart request that includes:

                Boundary metadata
                Form field headers
                File headers
                Possibly other fields
                File bytes

            File Size = just the uploaded file content

    What is "Guid.NewGuid()"?

        A "Guid" is a globally unique identifier

        In C#: Guid.NewGuid() will generate a new value that might look like: 6f7a91e4-72d2-4a3e-a0dd-8b6f0e783c21

            It is commonly used when the backend server need a unique identifier without asking the database first

            => For storage keys case, this helps avoid filename collisions
        
        Why it is useful for storage key case (now):

            Bad storage key idea: receipts/7/2026/08/receipt.pdf

            Problem with that:

                User uploads "receipt.pdf" today.

                User uploads that same receipt tomorrow => collision happen
            
            Better idea: receipts/7/2026/08/6f7a91e4-72d2-4a3e-a0dd-8b6f0e783c21.pdf

                The OriginalFileName can be stored for display only

                    OriginalFileName = "receipt.pdf"
                
                The storage path will use server-generated unique name:

                    StorageKey = "receipts/7/2026/08/6f7a91e4-72d2-4a3e-a0dd-8b6f0e783c21.pdf"
        
        In production, many systems use GUIDs, UUIDs, ULIDs, or similar generated IDs for object keys.

        Important habit: DO NOT TRUST the user's filename as the app's storage identity.
    
    In-depth Details about Content Hash Mechanism:

                using System.Security.Cryptography;

                // Open a readable stream for uploaded file bytes
                await using var stream = request.File.OpenReadStream();
                
                // Creating a hashing algorithm object, "using" = disposes it after use.
                using var sha256 = SHA256.Create();
                
                // Read all bytes from the stream and compute the hash
                var hashBytes = await sha256.ComputeHashAsync(stream);
                
                // Convert raw hash bytes into readable text like: a84c9f0d...
                var contentHash = Convert.ToHexString(hashBytes).ToLowerInvariant();
                

        A file stream is like a cursor reading through bytes from start to end.

            e.g: a file bytes can be

                [byte 1][byte 2][byte 3][byte 4][byte 5]...[byte n]
                    ^
                Current position
        
        When you calculate a hash, SHA-256 reads the stream from the "current position" until the end.

            await using var stream = request.File.OpenReadStream();

            using var sha256 = SHA256.Create();
            var hashBytes = await sha256.ComputeHashAsync(stream);
        
        After the above steps, the stream position is at the end ([Byte n]):

                [byte 1][byte 2][byte 3][byte 4][byte 5]...[byte n]
                                                               ^
                                                        Current position

        
        At this point, if following with this code: await stream.CopyToAsync(...); ~ Then CopyToAsync(...) may copy zero bytes, because there is nothing left to read

            To avoid that:

                Option A: open a fresh stream

                    await using var hashStream = request.File.OpenReadStream();

                    using var sha256 = SHA256.Create();
                    var hashBytes = await sha256.ComputeHashAsync(hashStream);

                    await using var saveStream = request.File.OpenReadStream();
                    // saveStream starts at the beginning again
                
                Option B: reset the stream position, if the stream supports seeking - since not every stream can seek, some network streams are forward-only
                    
                    uploadStream.Position = 0;
        
        Purpose of sha-256 in this case:

            SHA-256 reads all bytes and produces a fixed-size fingerprint:
                e.g:
                    receipt A bytes -> SHA-256 -> abc123...
                    receipt B bytes -> SHA-256 -> 91bd02...
        
    Explain in-depth for File Saving Locally:

        var uploadRoot = Path.Combine(
            Directory.GetCurrentDirectory(),
            "uploaded-receipts");

                This creates the base folder when uploaded files will live.

                "Directory.GetCurrentDirectory()": returns the folder where the app is running

                "Path.Combine(...)": joins path parts safely for the operating system.

                    => Path.Combine(appFolder, "uploaded-receipts") will becomes: C:\...\expense_management_app\uploaded-receipts

                    This is used instead of string-concatenation, because: Directory.GetCurrentDirectory() + "\\uploaded-receipts" is fragile

                        We might accidently create: C:\appuploaded-receipts or C:\\appuploaded_receipts 

                        "Path.Combine" handles seperators for us safely

        var safeRelativePath = storageKey.Replace('/', Path.DirectorySeparatorChar);

            The usage of "/" is normal for Amazon S3-like storageKeys.

            But, local disk paths should use the OS path separator => Path.DirectorySeparatorChar on:

                Windows: "\"

                Linux / MasOS: "/"
            
            => This code line will replace any "/" in storageKey with the current OS Path.DirectorySeparatorChar, correspondingly.

        var fullPath = Path.Combine(uploadRoot, safeRelativePath);

            This joins: 

                uploadRoot: C:\...\expense_management_app\uploaded-receipts

                    &
                
                safeRelativePath: receipts\7\2026\08\6f7a91e4-72d2-4a3e-a0dd-8b6f0e783c21.pdf
            
            Into:

                fullPath = C:\...\expense_management_app\uploaded-receipts\receipts\7\2026\08\6f7a91e4-72d2-4a3e-a0dd-8b6f0e783c21.pdf
            

        var directory = Path.GetDirectoryName(fullPath);

            Path.GetDirectoryName(...) will extracts only the folder part from the full file path

                Input: C:\...\expense_management_app\uploaded-receipts\receipts\7\2026\08\6f7a91e4-72d2-4a3e-a0dd-8b6f0e783c21.pdf

                Output: C:\...\expense_management_app\uploaded-receipts\receipts\7\2026\08

        Directory.CreateDirectory(directory!);

            This will creates the folder path if it does not already exist.

                If the folder is exists, it does not throw an error. It just returns successfully.

                Why "!" null-forgiving operator is here?

                    "Path.GetDirectoryName(fullPath) technically returns "string?", because there are cases where it might return null => null-forgiving is needed.

                    e.g: input: "abc.pdf" has no directory part (not possible in this app setting because the fullPath is built from "uploadRoot + safeRelativePath")

        await using var fileStream = System.IO.File.Create(fullPath);

            This creates a new file at "fullPath" directory / folder and gives you a stream you can write into

            System.IO.File.Create(fullPath) == Create / Open destination file for writing.

            => Output: returns a FileStream

                A FileStream = a pipe into the file on disk

                    Your app bytes => fileStream => actual file on disk
                
            Why "System.IO.File" instead of just "File"?

                ASP.NET controllers also have a method neamed "File(...)" for returning file responses

                    => So inside a controller, File.Create(...) may be confusing / conflict with ControllerBase.File(...)

                    => Using "System.IO.File.Create(...)" == "the filesystem File class, not the controller response helper"

        await request.File.CopyToAsync(fileStream);

            This copies uploaded file bytes into the destination file stream.

        
        "Await" is used here, because: copying a file can involve I/O & I/O can take time.

            => Instead of blocking the server thread while waiting for disk writes, "await" lets ASP.NET free the thread to handle other work.

        The goal:
            Take uploaded file bytes
            Write them into a file on disk
            Save the storage key in PostgreSQL

        Mechanism behind the implemented codes:

            The generated storageKey from the code will have the following format: 

                var storageKey = $"receipts/{userId}/{now:yyyy/MM}/{Guid.NewGuid()}{extension}";

                e.g: "receipts/7/2026/08/6f7a91e4-72d2-4a3e-a0dd-8b6f0e783c21.pdf"

            However, Windows file paths use "\", instead of, "/", thus we need to convert the "storageKey" value into the correct Windows format through the following code:

                var safeRelativePath = storageKey.Replace('/', Path.DirectorySeparatorChar);
    
    In POST /receipts, it touches 2 places:

            1. Local Disk - Save the actual receipt file bytes.

            2. PostgreSQL - Saves the receipt metadata row.
    
        Problem: these 2 operations are not automatically one transaction

            Database Transaction - PostgreSQL can do the following safely:

                        Insert receipt row

                        Insert other row

                        Update something else
                        
                        Commit all together

                => If something fails before commit, PostgreSQL can roll everything back
            
            Local Disk is not PostgreSQL => Cannot roll back

                The current code does:

                    Save file to disk

                    Save metadate to database
                
                Thus, there is a possible failure, where:

                    Save file to disk succeeds

                    Database save fails

                    => Create orphaned file, where the actual file exist but its corresponding metadata row is not saved / exist in the DB

                            One orphaned file may not be a problem, but, thousands of them will become:

                                Wasted Storage

                                Harder backups

                                Privacy risk

                                Cleanup difficulty

                                Confusing audits
                 
                On the other hand, opposite failure:

                    Save metadata to database

                    File save fails

                    => Database row say file exists, but the actual file is missing ~ this is called a broken reference.
                
            In production-level, these are the patterns that commonly use:

                Pattern A: Save file first => Save DB second => Cleanup file on DB failure (Current Approach)

                Pattern B: Save DB row first as PendingUpload => Save file second => Then mark Uploaded

                Pattern C: Upload directly to object storage with pre-signed URL, then callback / confirm metadata.

                Pattern D: Background reconcilliation job (Common in large system)
            
    Why "Throw" instead of "Throw ex"?

        Inside a "catch", there are 2 different ideas: 

            throw;
                It rethrows the same exception while preserving the original stack trace of functions called.

            throw ex;
                It throws the exception again from the current line where this code is implemented, "reset" the stack trace => Hide away where the problem originally happened.
        

        Stack Trace = the trail of method calls that led to the error
            e.g: Imagine this call chain
                    CreateReceipt()
                        => SaveUploadedFile()
                            => File.Create()
                    
                    If "File.Create()" fails, a good stack trace tells:
                        Error happened in File.Create()
                        Called by SaveUploadedFile()
                        Called by CreatedReceipt()
                    
                    => This is useful for debugging
            
            e.g:
                try
                {
                    await SaveUploadedFile();
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Upload failed");
                    throw;
                }

                "throw;" == Rethrow the current exception exactly as it was.
                            Preserve the original stack trace
                                => Logs still point back to the real failure location
                
                "throw ex;" == Throw this exception object from here
                                    => That can make the stack trace look like the rror originated at: "throw ex;" instead of the original location.
    
    Reject Corrupted Uploads = usually inspecting the actual file content, often called:

            Magic Number Validation

            File Signature Validation

            Header Validation

        At the current stage, a simple signature check is implemented:

            .pdf   -> first bytes should match PDF header
            .png   -> first bytes should match PNG signature
            .jpg   -> first bytes should match JPEG signature
            .jpeg  -> first bytes should match JPEG signature
        
        A good rule to remember:

            Extension = what filename claims

            MIME = what request claims

            Signature = what bytes claim

#### DELETE /receipts/{id}

    The order of deletion is in the reverse manner compare to POST /receipts:

        1. Remove the stored receipt metadata row from PostgreSQL.
        2. Attempt to delete digital receipt at the local disk level.
            
            In the event that this attempt is failed, it can be fixed (in the future) with background job that perform file cleanup for those orphaned files.

            Still returning with StatusCode of 204 because API resource from the database (source of truth) is deleted instead of any error Http Code.

### Testing for File Upload

    For JSON requests, the body is one simple thing:

        Content-Type: application/json

        {
        "merchant": "Woolworths",
        "amount": 12.50
        }

        => The server can read the whole body as one JSON document.
    
    For multipart / form-data means: this request body is made of multiple parts.

            e.g: part 1: file
                 part 2: description
                 part 3: categoryId

        Eventhough the current app's approach http request for receipt only has one file, the format is still multipart

        The "boundary", used in expense_management_platform.http, tells the server: "here is the marker that separates parts of the request".

        "Content-Disposition: form-data; name="file"; filename="sample_file.pdf"
            This tells ASP.NET that:
                This part is a form field.
                    The form field names is "file".                 => name="file" is how ASP.NET maps it to: public IFormFile File { get; set; }
                    The uploaded filename is "sample_file.pdf"
        
        Compare to JSON:
            
            JSON request:
                one body, one content type

            Multipart request:
                many possible body parts,
                each part has its own mini headers,
                boundary separates the parts
