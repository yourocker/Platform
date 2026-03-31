using Core.Data;
using Core.Entities.System;
using Core.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace CRM.Infrastructure;

public sealed class FileStorageService : IFileStorageService
{
    private readonly AppDbContext _context;
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly IWebHostEnvironment _environment;
    private readonly FileStorageOptions _options;
    private readonly ILogger<FileStorageService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public FileStorageService(
        AppDbContext context,
        ITenantContextAccessor tenantContextAccessor,
        IWebHostEnvironment environment,
        IOptions<FileStorageOptions> options,
        ILogger<FileStorageService> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _tenantContextAccessor = tenantContextAccessor;
        _environment = environment;
        _options = options.Value;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<StoredFile> SaveAsync(
        IFormFile file,
        string category,
        string? ownerEntityCode = null,
        Guid? ownerEntityId = null,
        CancellationToken cancellationToken = default)
    {
        var tenant = _tenantContextAccessor.CurrentTenant
            ?? throw new InvalidOperationException("Не удалось определить tenant для сохранения файла.");

        if (file.Length <= 0)
        {
            throw new InvalidOperationException("Пустой файл не может быть сохранён.");
        }

        var originalFileName = Path.GetFileName(file.FileName);
        var extension = Path.GetExtension(originalFileName);
        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var safeCategory = SanitizePathSegment(category, "misc");
        var safeEntityCode = SanitizePathSegment(ownerEntityCode, null);
        var rootPath = GetRootPath();

        var segments = new List<string>
        {
            tenant.Id.ToString("N"),
            safeCategory
        };

        if (!string.IsNullOrWhiteSpace(safeEntityCode))
        {
            segments.Add(safeEntityCode!);
        }

        if (ownerEntityId.HasValue)
        {
            segments.Add(ownerEntityId.Value.ToString("N"));
        }

        var relativeDirectory = Path.Combine(segments.ToArray());
        var absoluteDirectory = Path.Combine(rootPath, relativeDirectory);
        Directory.CreateDirectory(absoluteDirectory);

        var absolutePath = Path.Combine(absoluteDirectory, storedFileName);
        await using (var targetStream = new FileStream(absolutePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await file.CopyToAsync(targetStream, cancellationToken);
        }

        var storedFile = new StoredFile
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            OriginalFileName = originalFileName,
            StoredFileName = storedFileName,
            RelativePath = Path.Combine(relativeDirectory, storedFileName),
            ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? null : file.ContentType,
            Category = safeCategory,
            OwnerEntityCode = ownerEntityCode,
            OwnerEntityId = ownerEntityId,
            UploadedByEmployeeId = TryGetCurrentEmployeeId(),
            Size = file.Length,
            CreatedAt = DateTime.UtcNow
        };

        _context.StoredFiles.Add(storedFile);
        await _context.SaveChangesAsync(cancellationToken);

        return storedFile;
    }

    public async Task<StoredFile?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.StoredFiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<(StoredFile File, Stream Stream)?> OpenReadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var file = await _context.StoredFiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (file == null)
        {
            return null;
        }

        var absolutePath = Path.Combine(GetRootPath(), file.RelativePath);
        if (!System.IO.File.Exists(absolutePath))
        {
            _logger.LogWarning("Stored file metadata exists but physical file is missing: {FileId} {Path}", file.Id, absolutePath);
            return null;
        }

        var stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return (file, stream);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var storedFile = await _context.StoredFiles.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (storedFile == null)
        {
            return;
        }

        var absolutePath = Path.Combine(GetRootPath(), storedFile.RelativePath);
        if (System.IO.File.Exists(absolutePath))
        {
            System.IO.File.Delete(absolutePath);
        }

        _context.StoredFiles.Remove(storedFile);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteByReferenceAsync(string? reference, CancellationToken cancellationToken = default)
    {
        if (!TryParseReference(reference, out var fileId))
        {
            return;
        }

        await DeleteAsync(fileId, cancellationToken);
    }

    public string BuildAccessPath(Guid id) => $"/files/{id}";

    public bool TryParseReference(string? reference, out Guid fileId)
    {
        fileId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(reference))
        {
            return false;
        }

        var trimmed = reference.Trim();
        if (Guid.TryParse(trimmed, out fileId))
        {
            return true;
        }

        var withoutQuery = trimmed.Split('?', 2)[0];
        var lastSegment = withoutQuery.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        return lastSegment != null && Guid.TryParse(lastSegment, out fileId);
    }

    private Guid? TryGetCurrentEmployeeId()
    {
        var employeeIdRaw = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(employeeIdRaw, out var employeeId) ? employeeId : null;
    }

    private string GetRootPath()
    {
        if (!string.IsNullOrWhiteSpace(_options.RootPath))
        {
            var configuredPath = _options.RootPath!;
            return Path.IsPathRooted(configuredPath)
                ? Path.GetFullPath(configuredPath)
                : Path.GetFullPath(Path.Combine(_environment.ContentRootPath, configuredPath));
        }

        return Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "App_Data", "tenant-files"));
    }

    private static string SanitizePathSegment(string? raw, string? fallback)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback ?? string.Empty;
        }

        var sanitized = raw.Trim();
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            sanitized = sanitized.Replace(invalidChar, '_');
        }

        sanitized = sanitized.Replace(".", "_").Replace("/", "_").Replace("\\", "_");
        return string.IsNullOrWhiteSpace(sanitized) ? (fallback ?? string.Empty) : sanitized;
    }
}
