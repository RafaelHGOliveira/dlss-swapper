using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace DLSS.Swapper.UnoLinux.Converters;

public class StringToImageSourceConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, string language)
    {
        if (value is string path && !string.IsNullOrEmpty(path) && File.Exists(path))
        {
            // CRITICAL: WinUI/Uno BitmapImage.UriSource only supports ms-appx:///,
            // ms-appdata:/// and http(s) — NOT raw filesystem paths like the Unix path
            // in GameBase.CoverImage. `new BitmapImage(new Uri("/home/..."))` either throws
            // or silently renders nothing (which would be misread as an Uno cover-fidelity
            // failure on success-criterion 3). Load the local file via SetSourceAsync.
            // Convert() must return synchronously, so kick the load off fire-and-forget.
            var bitmap = new BitmapImage();
            var stream = File.OpenRead(path);
            _ = bitmap.SetSourceAsync(stream.AsRandomAccessStream())
                .AsTask()
                .ContinueWith(_ => stream.Dispose(), TaskScheduler.Default);
            return bitmap;
        }
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, string language)
        => throw new NotImplementedException();
}
