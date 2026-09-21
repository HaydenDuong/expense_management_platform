# Milestone 5 - Object Storage

## Goal

    Separate file storage from application server.

## Learning Objectives

    - Amazon S3.
    - MinIO.
    - File Streaming.
    - Object Storage Concepts.
    - Signed URLs.

## Business Requirements

    - Application should never store receipt inside PostgreSQL.
    - Only store metadata.

## Tasks

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

## Notes

### The central lesson of Milestone 5 is dependency inversion

    ReceiptsController
            |
            v
    IObjectStorage
            |
            +-- LocalObjectStorage
            +-- MinioObjectStorage
            +-- S3ObjectStorage

### The learning sequence for this Milestone

1. Define what belongs to the storage abstraction.

    Created: /Service/Storage/IObjectStorage.cs

        Interface does not accept:
            userId
            originalFileName
            Receipt
            AppDbContext
            A local root directory
            AWS / MinIO settings
        
        The storage provider does not make authorization / business decisions.
            It stores bytes at a key supplied by the calling application.
        
        The purpose of this interface is: it declares a shared contract that every storage provider promises to fulfill.

            e.g:
                    public class LocalObjectStorage : IObjectStorage
                    {
                        // Uses FileStream and local directories
                    }

                    public class MinioObjectStorage : IObjectStorage
                    {
                        // Uses the MinIO client
                    }

                    public class S3ObjectStorage : IObjectStorage
                    {
                        // Uses the AWS S3 SDK
                    }
            
            They are sharing the same operations but the implementation for each of them is different:

                | Contract operation | Local                | MinIO                     | Amazon S3               |
                |--------------------|----------------------|---------------------------|-------------------------|
                | `UploadAsync`      | Writes to disk       | Uploads to a MinIO bucket | Uploads to an S3 bucket |
                | `DownloadAsync`    | Opens a `FileStream` | Requests a MinIO object   | Requests an S3 object   |
                | `DeleteAsync`      | Deletes a local file | Deletes a MinIO object    | Deletes an S3 object    |

        The controller depends only on the contract:

                private readonly IObjectStorage _objectStorage;

            But not the whole code like:

                if (provider == "Local")
                {
                    // local logic
                }
                else if (provider == "MinIO")
                {
                    // MinIO logic
                }
                else if (provider == "S3")
                {
                    // S3 logic
                }
        
        Dependency Injection selects the implementation: 

                services.AddScoped<IObjectStorage, LocalObjectStorage>();
        
            Later, this registration can be changed into:

                services.AddScoped<IObjectStorage, MinioObjectStorage>();
            
            While the controller remain unchanged.
        
        What we do here is an example of dependency inversion:

            ReceiptsController ──depends on──> IObjectStorage

            LocalObjectStorage ──implements──> IObjectStorage
            MinioObjectStorage ──implements──> IObjectStorage
            S3ObjectStorage ─────implements──> IObjectStorage

        Sum-up:

            We created IObjectStorage so application code can depend on a stable storage contract instead of depending directly on local filesystem, MinIO, or Amazon S3 APIs. 
            
            Each provider offers the same application-level operations through different internal mechanisms, allowing dependency injection to replace the provider without changing receipt business logic.

    Questions:

        1/ Why does the interface accept "Stream" instead of "IFormFile"?

            IObjectStorage accepts Stream because streams are independent of ASP.NET and can represent content from HTTP uploads, files, memory, or network services. 
            
            Accepting IFormFile would couple the storage abstraction to the API transport layer.

        2/ Why is "storageKey" not an absolute filesystem path?

            A storage key is a provider-neutral object identifier. 
            
            An absolute path would expose local-storage implementation details and would be meaningless to MinIO or S3.

        3/ Why should "IObjectStorage" not receive "AppDbContext"?

            IObjectStorage should not receive AppDbContext because its responsibility is only to store, retrieve, and delete file objects. 
            
            Database metadata persistence is a separate application concern. 
            
            Giving the storage abstraction access to EF Core would couple it to PostgreSQL, violate the Single Responsibility Principle, make it harder to test or replace storage providers, and mix two different resources that cannot share one normal transaction. 
            
            The calling application should coordinate object storage and database operations, including cleanup when one operation fails.

2. Decide what a storage key represents and who generates it.

    Problem with current stage (milestone 4 -> before this step):

        /Controllers/ReceiptsController.cs hardcodes: "uploaded-receipts"

            This creates several problems:

                Production may require a different location.

                Tests should user temporary directories.

                The working directory can vary depending on how the API startss.

                Infrastructure settings become embedded in source code.

    Solution:

        Created: /Options/LocalStorageOptions.cs

    Questions:

        1/ Why use an "Options" class?

            Configuration begins as key / value data:

                Storage:Local:RootPath = uploaded-receipts
            
            The "Options" pattern converts it into a strongly typed C# object: 

                LocalStorageOptions options
            
            This is preferable to scattering calls like throughout the code:

                configuration["Storage:Local:RootPath"]
            
            Benefits:

                Centralized configuration shape.

                Compile-time property names.

                Easier validation.

                Easier injection.

                Easier testing.

        2/ Why "string.Empty"?

            Configuration binding happens after C# constructs the object.
                => Therefore give the property a valid initial value of: public string RootPath { get; set; } = string.Empty;
            
            Later, startup validation will reject an empty value.
                Thus "= string.Empty" is for nullability warning only.
            
        3/ Why "SectionName"?

            Instead of repeating "Storage:Local", "SectionName" can be used instead in DI registration as below:
            
                services
                    .AddOptions<LocalStorageOptions>()
                    .Bind(configuration.GetSection(LocalStorageOptions.SectionName))        LocalStorageOptions.SectionName vs. "Storage:Local"
                    .Validate(
                        options => !string.IsNullOrWhiteSpace(options.RootPath),
                        "Local storage root path is required.")
                    .ValidateOnStart();
            
            .ValidateOnStart() = means invalid configuration prevents the application from starting
                => Better than discovering the problem when the first user uploads a receipt.

    In the current stage:

        The storage key is:
            A provider-neutral identifier.
            Relative - not an absolute filesystem path.
            Stored in PostgreSQL.
            Generated by the application workflow.
            Consumed by IObjectStorage.

        It is generated by the following code in /Controllers/ReceiptsController.cs:

            var storageKey = $"receipts/{userId}/{now:yyyy/MM}/{Guid.NewGuid()}{extension}";
        
        In the later stage, this can be moved into a receipt application service / key generator => Loosely decoupling the ReceiptsController.cs

    The plan for this milestone is:

        Application:
            Generates "receipts/7/2026/08/abc.pdf"

        LocalObjectStorage:
            Maps it to C:\...\uploaded-receipts\receipts\7\2026\08\abc.pdf

        MinioObjectStorage:
            Maps it to an object inside a bucket

        S3ObjectStorage:
            Maps it to an object inside an S3 bucket

3. Extract the existing local filesystem behavior into LocalObjectStorage.

    A - Register and validate the options

        Registered into DependencyInjection.cs

            services

                // Registers the strongly typed "Options" object with dependency injection
                .AddOptions<LocalStorageOptions>()

                // Maps the following value in appsettings.Development.json
                // {
                //    "Storage": {
                //        "Local": {
                //        "RootPath": "uploaded-receipts"
                //        }
                //    }
                // }
                // Onto:
                // new LocalStorageOptions
                // {
                //     RootPath = "uploaded-receipts"
                // }
                .Bind(configuration.GetSection(LocalStorageOptions.SectionName))

                // Defines a configuration validity rule: Empty / whitespace-only paths are rejected
                .Validate(
                    options => !string.IsNullOrWhiteSpace(options.RootPath),
                    "Local storage root path is required.")
                
                // Checks the configuration when the application starts instead of waiting until the first upload request
                .ValidateOnStart();

        Without startup validation in above step:

            Application starts successfully
                        ↓
            First user uploads a receipt
                        ↓
            LocalObjectStorage discovers RootPath is missing
                        ↓
            Request fails at runtime
        
        This follows the "fail-fast" principle: configuration errors should be discovered as early as possible

        Note: 

            LocalStorageOptions
                Configuration data
                └── RootPath

            LocalObjectStorage
                Performs file operations
                ├── UploadAsync
                ├── DownloadAsync
                └── DeleteAsync

            Class A : Interface Class S
                => This creates a compile-time promise that:
                        Class A will provide every member required by "IObjectStorage"

                        If the Interface Class S requires:
                            UploadAsync
                            DownloadAsync
                            DeleteAsync
                        
                        But Class A omits one of them => the compiler will reject this class A

        Question:

            1/ Why "sealed" is being used for both classes of "LocalStorageOptions" and "LocalObjectStorage" ?

                "Sealed" == this class is not allowed to be inherited by another class.

                Without "sealed":

                    Other developers could use either of those classes as the base:

                        public class SpecialLocalObjectStorage : LocalObjectStorage
                        {
                            ...
                        }
                    
                The reason why it is being used here is that "LocalObjectStorage" is intended to be a finished, concrete implementation

                    IObjectStorage
                        ├── LocalObjectStorage
                        ├── MinioObjectStorage
                        └── S3ObjectStorage
                    
                    We expect new storage providers to implement "IObjectStorage" instead of inherit from another storage provider

                "Sealed" communicates this design intention:
                    Depend on and extend through "IObjectStorage".
                    Treat "LocalObjectStorage" as a complete leaf implementation.
                    Use composition rather than subclassing it.
                    Mock / fake "IObjectStorage" in tests instead of subclassing the concrete class.
                
                Sum-up:
                    "LocalObjectStorage" & "LocalStorageOptions" are sealed because they are a concrete lead implementation of "IObjectStorage".
                    They are not the base classed designed for inheritance
                        => Other storage providers should implement the interface independently
                    
                    => Use "sealed" when a class is complete and not designed as an extension point.
            
            2/ Different between "public async Task UploadAsync(...)" & "public Task DeleteAsync(...)"

                "Task" = the method's caller-facing return type
                "async" = tells the compiler to construct / manage that Task for you

                    public async Task UploadAsync(...)
                    {
                        await content.CopyToAsync(...);
                    }

                    This method does return a "Task" to its caller:

                            Task uploadTask = objectStorage.UploadAsync(...)

                        Because the method has the "async" keyword ==> compiler creates and completes that "Tasks" automatically.

                        Inside an "async Task" method, reaching the final closing brace, means:

                            Complete the compiler-managed Task successfully.
                        
                        Thus, we do not write "return Task.CompletedTask;"
                    
                    The following is also legal when we need to exit early:

                                public async Task DoSomethingAsync(bool shouldStop)
                                {
                                    if (shouldStop)
                                    {
                                        return;
                                    }

                                    await SomeOperationAsync();
                                }

                        We returns with "return;" but not with "return someValue;" because "async Task" operation does not produce a result value.
                
                For plain "Task":

                            public Task DeleteAsync(...)
                            {
                                cancellationToken.ThrowIfCancellationRequested();

                                var fullPath = ResolveFullPath(storageKey);

                                File.Delete(fullPath);

                                return Task.CompletedTask;
                            }

                    It does not have the "async" keyword ==> Therefore, the compiler does not automatically create the returned task.
                        => The method body must explicitly return an object of type "Task".
                    
                    Thus, after synchronous deletion succeeds, we need to return:

                            return Task.CompletedTask;  // This is an already-successfully-completed reusable task.
                    
                    Conceptually:

                        File.Delete finished synchronously
                                ↓
                        Nothing remains to wait for
                                ↓
                        Return an already-completed Task
                    
                    The caller can still use the interface consistently:

                        await objectStorage.DeleteAsync(
                            storageKey,
                            cancellationToken);
                    
                    Eventhough, we could include "async" as follow:

                                public async Task DeleteAsync(...)
                                {
                                    File.Delete(fullPath);
                                }
                        
                        But there is nothing to "await" => The compiler would report warning "CS1998": This async method lacks 'await' operators and will run synchronously

            3/ Why keep "DeleteAsync" a task-based instead of just "public void DeleteAsync()"?

                Local deletion is synchronous, because .NET has no "File.DeleteAsync()" method

                But future storage providers, like MinIO, Amazon S3, will performing network operations, which means time will be consumed while waiting for the operation to complete:
                            MinioObjectStorage.DeleteAsync(...)
                            S3ObjectStorage.DeleteAsync(...)
                    These will definitely be asynchronous.

                The interface, thus, needs one consistent contract: Task DeleteAsync(...)

                Therefore:

                    LocalObjectStorage
                        Performs synchronous File.Delete
                        Returns Task.CompletedTask

                    MinioObjectStorage
                        Awaits a network deletion request

                    S3ObjectStorage
                        Awaits a network deletion request
                
                Sum-up:
                    UploadAsync returns a Task, but because it is declared async, the compiler creates and completes that task around the awaited copy operation. 
                    
                    DeleteAsync is not declared async because local deletion is synchronous, so it explicitly returns Task.CompletedTask to satisfy the common asynchronous storage contract.
            
            4/ Why the caller must provide the correct storageKey

                    "LocalObjectStorage" does not know:

                        Which user owns the object.
                        Which receipt row references it.
                        Whether its status permits deletion.
                        Whether the current caller is authorized.
                    
                    Those checks belong to ReceiptsController.cs

                            Controller/application workflow
                                1. Authenticate user
                                2. Query receipt belonging to that user
                                3. Check receipt status permits deletion
                                4. Read receipt.StorageKey
                                5. Call DeleteAsync(receipt.StorageKey)

                            LocalObjectStorage
                                6. Resolve key safely beneath _rootPath
                                7. Delete the exact local file
            
        The runtime flow at this point:

            1. ASP.NET dependency injection creates LocalObjectStorage.
            2. IOptions supplies RootPath = "uploaded-receipts".
            3. IWebHostEnvironment supplies the application content root.
            4. Constructor calculates the absolute _rootPath.
            5. Controller calls UploadAsync with a storage key and stream.
            6. UploadAsync calls ResolveFullPath(storageKey).
            7. ResolveFullPath validates and converts the key.
            8. UploadAsync creates the parent directory.
            9. UploadAsync copies the stream into a local file.

        The security sequence behind that flow is:

            Path.GetFullPath
                ↓
            Canonicalizes the path and exposes its real destination

            Path.GetRelativePath
                ↓
            Determines where that destination is relative to the storage root

            Containment checks
                ↓
            Reject "..\", exact "..", or a rooted result
        
        Path.GetFullPath(...) does remove ".." from textual representation, but that alone does not prevent an escape
            e.g: Path.GetFullPath(@"C:\app\uploaded-receipts\..\appsettings.json"); 

                    produces: C:\app\appsettings.json, because ".." == move up one directory - remove the directory segment immediately before ".."
                        ==> uploaded-receipts\.. cancel each other out.

                 Path.GetFullPath(@"C:\app\uploaded-receipts\.\appsettings.json");

                    produces: C:\app\uploaded-receipts\appsetting.json, because "." == remain in the curren directory

        For Task UploadAsync(...):

            When no exception occurs:

                    File created
                        ↓
                    All remaining input bytes copied
                        ↓
                    Destination stream asynchronously disposed
                        ↓
                    Buffers flushed and file handle released
                        ↓
                    Method's Task completes successfully
            
                Because the method returns plain "Task", the caller receives no storage result => await objectStorage.UploadAsync(...)
            
            When exceptions occur:

                1. Invalid storageKey.
                        ResolveFullPath throws
                        No directory or file created

                2. Input stream cannot be read.
                        Validation throws
                        No directory or file created

                3. Parent directory creation fails.
                        CreateDirectory throws
                        No file created

                4. Destination already exists.
                        FileMode.CreateNew throws
                        Existing file remains untouched
                        Cleanup catch is not entered
                
                5. Copy fails after file creation.
                        Catch executes
                            Partial file deletion attempted
                            Original exception rethrown
                
                6. Client cancels during upload.
                        CopyToAsync observes cancellation
                        OperationCanceledException thrown
                        Partial file deletion attempted
                        Cancellation continues to caller
                
                7. Copy succeeds but database save later fails
                        UploadAsync succeeds
                                ↓
                        Database SaveChangesAsync fails
                                ↓
                        Application calls DeleteAsync as compensation
            
        

4. Refactor upload and deletion to use the interface.
5. Add configuration and provider selection through dependency injection.
6. Add MinIO locally through Docker.
7. Implement object download/access, authorization, and signed URLs.
8. Decide deliberately between hard delete and soft delete.
9. Add storage metadata and migration changes.
10. Test failure and cleanup behavior, not merely the successful path.
