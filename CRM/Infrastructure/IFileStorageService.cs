using Core.Entities.System;
using Microsoft.AspNetCore.Http;

namespace CRM.Infrastructure;

public interface IFileStorageService
{
    Task<StoredFile> SaveAsync(
        IFormFile file,
        string category,
        string? ownerEntityCode = null,
        Guid? ownerEntityId = null,
        CancellationToken cancellationToken = default);

    Task<StoredFile?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(StoredFile File, Stream Stream)?> OpenReadAsync(Guid id, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task DeleteByReferenceAsync(string? reference, CancellationToken cancellationToken = default);

    string BuildAccessPath(Guid id);

    bool TryParseReference(string? reference, out Guid fileId);
}
