using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using expense_management_app.Contracts.Receipts;
using expense_management_app.Contracts.Receipts.Requests;
using expense_management_app.Contracts.Receipts.Responses;
using expense_management_app.Infrastructure.Persistence;
using expense_management_app.Models.Receipts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace expense_management_app.Controllers;

[ApiController]
[Authorize]
[Route("receipts")]
public class ReceiptsController : ControllerBase
{
    // Dependencies
    private readonly AppDbContext _context;
    private readonly ILogger<ReceiptsController> _logger;

    // Constructor
    public ReceiptsController(
        AppDbContext context, 
        ILogger<ReceiptsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // HTTP Methods
    // GET /receipts/... - Retrieve all current UserId's receipts that match the request query parameters
    [HttpGet]
    public async Task<ActionResult<ReceiptListResponse>> GetReceipts(
        [FromQuery] ReceiptQueryParameters query)
    {
        // UserId validity check
        if (!TryGetCurrentUserId(out var userId))
        {
            _logger.LogWarning("Request rejected because the subject claim was missing or invalid.");
            return Unauthorized();
        }

        // Building SQL query request
        var receiptQuery = _context.Receipts
            .Where(receipt => receipt.AppUserId == userId);
        
        // Date Filters
        if (query.FromCreatedAt is not null)
        {
            receiptQuery = receiptQuery
                .Where(receipt => receipt.CreatedAt >= query.FromCreatedAt);
        }

        if (query.ToCreatedAt is not null)
        {
            receiptQuery = receiptQuery
                .Where(receipt => receipt.CreatedAt <= query.ToCreatedAt);
        }

        // Status Filter
        if (query.Status is not null)
        {
            receiptQuery = receiptQuery
                .Where(receipt => receipt.Status == query.Status);
        }

        // Pagination Processing
        var totalCount = await receiptQuery.CountAsync();

        var sortBy = query.SortBy.Trim().ToLowerInvariant();
        var sortDirection = query.SortDirection.Trim();

        receiptQuery = sortBy switch
        {
            "filesize" => sortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase)
                ? receiptQuery.OrderBy(receipt => receipt.FileSize)
                : receiptQuery.OrderByDescending(receipt => receipt.FileSize),
            
            "status" => sortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase)
                ? receiptQuery.OrderBy(receipt => receipt.Status)
                : receiptQuery.OrderByDescending(receipt => receipt.Status),

            "originalfilename" => sortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase)
                ? receiptQuery.OrderBy(receipt => receipt.OriginalFileName)
                : receiptQuery.OrderByDescending(receipt => receipt.OriginalFileName),

            _ => sortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase)
                ? receiptQuery.OrderBy(receipt => receipt.CreatedAt)
                : receiptQuery.OrderByDescending(receipt => receipt.CreatedAt)
        };
        
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var page = Math.Max(query.Page, 1);
        var skip = (page - 1) * pageSize;

        receiptQuery = receiptQuery
            .Skip(skip)
            .Take(pageSize);
        
        // Mapping selected receipt entities that matching the query parameters into ReceiptResponse DTO
        var items = await receiptQuery
            .Select(receipt => new ReceiptResponse
            {
                Id = receipt.Id,
                Status = receipt.Status,
                OriginalFileName = receipt.OriginalFileName,
                ContentType = receipt.ContentType,
                FileSize = receipt.FileSize,
                CreatedAt = receipt.CreatedAt,
                UpdatedAt = receipt.UpdatedAt
            })
            .ToListAsync();
        
        // Return List Response for Mapped Receipts
        var response = new ReceiptListResponse
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };

        return Ok(response);
    }

    // GET /receipts/{receiptId} - Retrieve one receipt of the current UserId based on input receiptId
    [HttpGet("{receiptId}")]
    public async Task<ActionResult<ReceiptResponse>> GetOneReceipt(
        [FromRoute] int receiptId)
    {
        // UserId validity Check
        if (!TryGetCurrentUserId(out var userId))
        {
            _logger.LogWarning("Request rejected because the subject claim is missing or invalid.");
            return Unauthorized();
        }

        var response = await _context.Receipts
            .Where(receipt =>
                receipt.AppUserId == userId &&
                receipt.Id == receiptId)
            .Select(receipt => new ReceiptResponse
            {
                Id = receipt.Id,
                Status = receipt.Status,
                OriginalFileName = receipt.OriginalFileName,
                ContentType = receipt.ContentType,
                FileSize = receipt.FileSize,
                CreatedAt = receipt.CreatedAt,
                UpdatedAt = receipt.UpdatedAt
            })
            .FirstOrDefaultAsync();
        
        if (response is null)
        {
            _logger.LogWarning("Request rejected because this receipt is not exist or invalid.");
            return NotFound();
        }

        return Ok(response);
    }
    
    // POST /receipts - Create a new receipt for the current UserId
    [HttpPost]
    [RequestSizeLimit(10 * 1024 * 1024)] // protects the HTTP request pipeline
    public async Task<ActionResult<ReceiptResponse>> CreateReceipt(

        // [FromForm] is used because file uploads usually come as: multipart/form-data
        // Not JSON - the request body is not:
        // {
        //      "file": "..."
        // }
        // Rather: 
        // Form field named "file" containing binary file bytes
        [FromForm] CreateReceiptRequest request)
    {
        // UserId Validity Check
        if (!TryGetCurrentUserId(out var userId))
        {
            _logger.LogWarning("Request rejected because the subject claim is missing or invalid");
            return Unauthorized();
        }

        // Validate File Exists
        // Eventhough [Required] is declared in "CreateReceiptRequest.cs" for IFormFile "File"
        // Upload validation is important & need to be added again & explicit
        if (request.File is null || request.File.Length == 0)
        {
            _logger.LogWarning("Request rejected because the upload file is missing or invalid.");
            return BadRequest();
        }

        // Validate File Size
        // Manual request.File.Length check == protects this app business rule
        const long maxFileSize = 10 * 1024 * 1024;
        if (request.File.Length > maxFileSize)
        {
            _logger.LogWarning("Request rejected because the upload file is over 10 MB.");
            return BadRequest();
        }

        // Validate Extension
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf" };
        var extension = Path.GetExtension(request.File.FileName).ToLowerInvariant();
        
        if (!allowedExtensions.Contains(extension))
        {
            _logger.LogWarning("Request rejected because the upload file extension is not supported.");
            return BadRequest();
        }

        // Validate MIME (Content) Type
        // Note: MIME type from the request can be spoofed => this is not full security.
        // Later: corrupted-file / header validation will improving this
        var allowedContentType = new[]
        {
            "image/jpg",
            "image/jpeg",
            "image/png",
            "application/pdf"
        };

        if (!allowedContentType.Contains(request.File.ContentType))
        {
            _logger.LogWarning("Request rejected because the upload file content type is not supported.");
            return BadRequest();
        }

        // File - Corruption Check
        // Currently, this only support obvious fake / corrupted files
        // Does not fully prove the entire file is valid like: A PDF might start with valid headerBytes %PDF but broken halfway through
        if (!HasValidFileSignature(request.File, extension))
        {
            _logger.LogWarning("Request rejected because the upload file signature is invalid.");
            return BadRequest();
        }

        // Generate Storage Key
        // Note: Do not trust the original filename for storage
        // Generate a standard name => Avoids collisions and weird user filenames becoming storage paths.
        var now = DateTime.UtcNow;
        var storageKey = $"receipts/{userId}/{now:yyyy/MM}/{Guid.NewGuid()}{extension}";    // receipts/7/2026/08/0f8fad5b-d9cb-469f-a165-70867728950e.pdf

        // Calculate Content Hash
        await using var stream = request.File.OpenReadStream();
        using var sha256 = SHA256.Create();
        var hashBytes = await sha256.ComputeHashAsync(stream);
        var contentHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        // Check Duplicate ContentHash for this current UserId before saving
        // Avoid same file upload twice
        var duplicateExists = await _context.Receipts
            .AnyAsync(receipt =>
                receipt.AppUserId == userId &&
                receipt.ContentHash == contentHash);
        
        if (duplicateExists)
        {
            _logger.LogWarning("Request rejected because this file is already exist.");
            return Conflict();
        }

        // Save this file
        // Current: Locally
        var uploadRoot = Path.Combine(
            Directory.GetCurrentDirectory(),
            // "..", == if you want to move "uploaded-receipts" to above level of the current directory()
            "uploaded-receipts");
        
        var safeRelativePath = storageKey.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(uploadRoot, safeRelativePath);
        var directory = Path.GetDirectoryName(fullPath);

        if (directory is null)
        {
            _logger.LogWarning("Request rejected due to internal server error.");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        Directory.CreateDirectory(directory);

        // Create a new Receipt object row
        var receipt = new Receipt
        {
            AppUserId = userId,
            Status = ReceiptStatus.Pending,
            OriginalFileName = Path.GetFileName(request.File.FileName),
            StorageKey = storageKey,
            ContentType = request.File.ContentType,
            ContentHash = contentHash,
            FileSize = request.File.Length,
            CreatedAt = now,
            UpdatedAt = now
        };

        // Save file and Receipt object row
        // Pattern: Save file first
        //          Save metadata to DB
        //          Cleanup file on DB failure
        // Note:
        // Disk - cannot be rolled back by EF Core
        // Database - can be rolled back by EF Core
        try
        {
            // Try to save file to local disk
            // Possible failures:
            // Folder permission problem
            // Invalid path
            // Disk full
            // File locked
            await using var fileStream = System.IO.File.Create(fullPath);

            // Possible failures:
            // Connection interrupted
            // Disk write failure
            // Request body read issue
            await request.File.CopyToAsync(fileStream);
            _logger.LogInformation("The uploaded receipt is successfully saved to local disk.");

            // Try to save metadata to DB
            // This does not hit the DB yet - it only starts tracking the entity.
            _context.Receipts.Add(receipt);

            // Actually writes to PostgreSQL
            // Possible failures:
            // Database unavailable
            // Unique ContentHash constraint violation
            // Foreign Key Issue
            // Timeout
            await _context.SaveChangesAsync();
            _logger.LogInformation("This receipt is succesfully saved to the database.");

        }
        catch
        {
            // This checks whther the file was created yet.
            // If True, delete it so no orphaned file is left over.
            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
            }

            throw;
        }

        // Return ReceiptResponse DTO back
        var response = new ReceiptResponse
        {
            Id = receipt.Id,
            Status = receipt.Status,
            OriginalFileName = receipt.OriginalFileName,
            ContentType = receipt.ContentType,
            FileSize = receipt.FileSize,
            CreatedAt = receipt.CreatedAt,
            UpdatedAt = receipt.UpdatedAt
        };

        return StatusCode(StatusCodes.Status201Created, response);
    }

    // DELETE /receipts/{receiptId} - Delet
    [HttpDelete("{receiptId}")]
    public async Task<ActionResult> DeleteReceipt(
        [FromRoute] int receiptId)
    {
        // UserId validity check
        if (!TryGetCurrentUserId(out var userId))
        {
            _logger.LogWarning("Request rejected because the subject claim is missing or invalid.");
            return Unauthorized();
        }

        // Receipt Validity Check
        var receipt = await _context.Receipts
            .FirstOrDefaultAsync(receipt =>
                receipt.AppUserId == userId &&
                receipt.Id == receiptId);
        
        if (receipt is null)
        {
            _logger.LogWarning("Request rejected because this Receipt ID does not exist or invalid.");
            return NotFound();
        }

        if (receipt.Status != ReceiptStatus.Pending)
        {
            _logger.LogWarning("Request rejected because this receipt is processing now.");
            return Conflict();
        }

        var uploadRoot = Path.Combine(
            Directory.GetCurrentDirectory(),
            "uploaded-receipts");
        
        var safeRelativePath = receipt.StorageKey.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(uploadRoot, safeRelativePath);

        _context.Receipts.Remove(receipt);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Delete request for this receipt is successed.");

        try
        {
            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
                _logger.LogWarning("Related file has been deleted for storage successfully.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete local receipt file {StorageKey}", receipt.StorageKey);
        }
        
        return NoContent();
    }

    // UserId Validation Method
    private bool TryGetCurrentUserId(out int userId)
    {
        var userIdValue = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return int.TryParse(userIdValue, out userId);
    }

    // Simple file corruption check
    private static bool HasValidFileSignature(IFormFile file, string extension)
    {
        var signature = new Dictionary<string, byte[]>
        {
            // These are the values of expected first bytes of a file
            // .pdf = 25 50 44 46 == %PDF
            // .png =
            // .jpg =
            // .jpeg = 
            [".pdf"] = [0x25, 0x50, 0x44, 0x46],
            [".png"] = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A],
            [".jpg"] = [0xFF, 0xD8, 0xFF],
            [".jpeg"] = [0xFF, 0xD8, 0xFF] 
        };

        // Try to determine whether the upload file "extension" found in the dictionary of var signature
        // ".TryGetValue" == asks the dictionary of var Signature whether it contains the value of "extension"
        // If yes ==> return the corresponding byte[]
        // e.g: input extension = ".pdf" ==> output expectedSignature = [0x25, 0x50, 0x44, 0x46]
        // If not ==> return false
        if (!signature.TryGetValue(extension, out var expectedSignature))
        {
            return false;
        }

        // If a file is shorter than the required header => it cannot be valid
        // Continue with the e.g above: the expectedSignature is not 4 because of its value is [0x25, 0x50, 0x44, 0x46]
        // Thus, this check says that if the uploaded file has < 4 bytes => it cannot possibly start with the 4-byte PDF signature.
        // In that case ==> return False
        if (file.Length < expectedSignature.Length)
        {
            return false;
        }

        // This create an empty byte array tha big enough to hold the bytes of the uploaded file
        // By using value of "expectedSignature.Length"
        // In this case: var headerBytes = new byte[4] => [00, 00, 00, 00]
        var headerBytes = new byte[expectedSignature.Length];

        // Opens a stream to read the uploaded file's bytes.
        // By initialize a var, named "stream", stored the uploaded file's byte and dipose this var once the scope of work (method / block) is done
        using var stream = file.OpenReadStream();

        // Read bytes from the stream into headerBytes.
        // Start writing what was written into headerBytes at its index 0.
        // Read at most headerBytes.Length bytes.
        // Return how many bytes were actually read into var "bytesRead"
        // stream.Read(destinationArray, startIndexIndestinationArray, maxBytesToRead) ==> its output as an int value that corresponding to "headerBytes.Length"
        // Note: the int output is the actual number of bytes read, thus
        // ==> Could < maxBytesToRead (or count) if fewer bytes are available
        // Return 0 if the end of the stream is reached.
        var bytesRead = stream.Read(headerBytes, 0, headerBytes.Length);

        // This is a double-simultaneous logic check that determine whether:
        // 1. bytesRead == expectedSignature.Length ~ Did we read the exact number of bytes we expected?
        // 2. headerBytes.SequenceEqual(expectedSignature) ~ Are the actual header bytes exactly equal to the expected signature bytes, same order?
        return bytesRead == expectedSignature.Length &&
            headerBytes.SequenceEqual(expectedSignature);
    }
}