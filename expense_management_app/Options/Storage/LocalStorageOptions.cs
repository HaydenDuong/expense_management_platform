namespace expense_management_app.Options.Storage;

public sealed class LocalStorageOptions
{
    public const string SectionName = "Storage:Local";

    public string RootPath { get; set; } = string.Empty;
}