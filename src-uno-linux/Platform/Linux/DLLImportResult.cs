namespace DLSS_Swapper.Data;

public record DLLImportResult
{
    public bool Success { get; private set; }
    public string FilePath { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public bool ImportedAsDownload { get; private set; }

    private DLLImportResult()
    {
    }

    public static DLLImportResult FromSucces(string filePath, string message, bool importedAsDownload)
    {
        return new DLLImportResult()
        {
            Success = true,
            FilePath = filePath,
            ImportedAsDownload = importedAsDownload,
            Message = message,
        };
    }

    public static DLLImportResult FromFail(string filePath, string message)
    {
        return new DLLImportResult()
        {
            Success = false,
            FilePath = filePath,
            Message = message,
        };
    }

    public override string ToString()
    {
        return $"Success: {Success}, FilePath: {FilePath}, ImportedAsDownload: {ImportedAsDownload}, Message: {Message}";
    }
}
