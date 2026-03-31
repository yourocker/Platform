namespace CRM.Infrastructure;

public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    public string? RootPath { get; set; }
}
