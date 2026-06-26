namespace DLSS.Swapper.UnoLinux.Services;

public interface IFilePickerService
{
    Task<IReadOnlyList<string>> PickFilesAsync(string[] extensions, bool allowMultiple);
    Task<string?> PickSaveFileAsync(string defaultName, string extension);
    Task<string?> PickFolderAsync();
}
