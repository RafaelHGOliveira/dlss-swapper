using DLSS_Swapper.Helpers;
using DLSS_Swapper.Platform.Windows;
using DLSS_Swapper.UserControls;
using Microsoft.UI.Xaml.Controls;
using NvAPIWrapper.DRS;
using SQLite;
using System;
using System.Threading.Tasks;

namespace DLSS_Swapper.Data;

public abstract partial class Game : WindowsGame, IComparable<Game>, IEquatable<Game>
{
    [Ignore]
    public DriverSettingsProfile? DriverSettingsProfile { get; set; }

    #region IComparable<Game>
    public int CompareTo(Game? other)
    {
        if (other is null)
        {
            return -1;
        }

        return Title.CompareTo(other.Title);
    }
    #endregion

    public bool Equals(Game? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ID == other.ID)
        {
            return true;
        }

        if (PlatformId == other.PlatformId)
        {
            return true;
        }

        return false;
    }

    protected bool ParentUpdateFromGame(Game game)
    {
        return ParentUpdateFromGameBase(game);
    }

    public abstract bool UpdateFromGame(Game game);

    // -----------------------------------------------------------------------
    // Windows-specific UI methods (dialog + file picker)
    // -----------------------------------------------------------------------

    public async Task PromptToRemoveCustomCover()
    {
        var dialog = new EasyContentDialog(App.CurrentApp.MainWindow.Content.XamlRoot)
        {
            Title = ResourceHelper.GetString("Game_CustomCoverRemove"),
            PrimaryButtonText = ResourceHelper.GetString("General_Remove"),
            CloseButtonText = ResourceHelper.GetString("General_Cancel"),
            DefaultButton = ContentDialogButton.Primary,
            Content = ResourceHelper.GetString("Game_AreYouSureRemoveCustomCover"),
        };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await RemoveCustomCoverAsync();
        }
    }

    public void PromptToBrowseCustomCover()
    {
        try
        {
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(App.CurrentApp.MainWindow);

            var fileFilters = new System.Collections.Generic.List<FileSystemHelper.FileFilter>()
            {
                new FileSystemHelper.FileFilter("Image files", "*.jpg; *.jpeg; *.png; *.webp"),
            };

            var coverImageFile = FileSystemHelper.OpenFile(hWnd, fileFilters, Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));

            if (string.IsNullOrWhiteSpace(coverImageFile))
            {
                return;
            }

            ApplyCustomCoverAsync(coverImageFile);
        }
        catch (Exception err)
        {
            Logger.Error(err);
        }
    }
}
