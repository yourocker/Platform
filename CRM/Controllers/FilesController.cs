using CRM.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace CRM.Controllers;

public class FilesController : Controller
{
    private readonly IFileStorageService _fileStorageService;

    public FilesController(IFileStorageService fileStorageService)
    {
        _fileStorageService = fileStorageService;
    }

    [HttpGet("/files/{id:guid}")]
    public async Task<IActionResult> Get(Guid id, [FromQuery] bool download = false, CancellationToken cancellationToken = default)
    {
        var result = await _fileStorageService.OpenReadAsync(id, cancellationToken);
        if (result == null)
        {
            return NotFound();
        }

        var (file, stream) = result.Value;
        var contentType = string.IsNullOrWhiteSpace(file.ContentType)
            ? "application/octet-stream"
            : file.ContentType;

        return download
            ? File(stream, contentType, file.OriginalFileName)
            : File(stream, contentType, enableRangeProcessing: true);
    }
}
