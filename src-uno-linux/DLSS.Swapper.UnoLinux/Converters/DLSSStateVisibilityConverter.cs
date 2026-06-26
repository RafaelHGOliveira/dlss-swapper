using System;
using DLSS_Swapper.Data;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace DLSS.Swapper.UnoLinux.Converters;

partial class DLSSStateVisibilityConverter : DependencyObject, IValueConverter
{
    public static readonly DependencyProperty DesierdStateProperty =
        DependencyProperty.Register(nameof(DesierdState), typeof(string),
            typeof(DLSSStateVisibilityConverter), new PropertyMetadata(null));

    public string DesierdState
    {
        get => (string)GetValue(DesierdStateProperty);
        set => SetValue(DesierdStateProperty, value);
    }

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not LocalRecord localRecord)
            return Visibility.Collapsed;

        return DesierdState switch
        {
            "Downloading" => localRecord.FileDownloader is not null
                ? Visibility.Visible : Visibility.Collapsed,
            "Downloaded" => localRecord.IsDownloaded
                ? Visibility.Visible : Visibility.Collapsed,
            "NotFound" => localRecord.FileDownloader is null && !localRecord.IsDownloaded
                ? Visibility.Visible : Visibility.Collapsed,
            "Imported" => localRecord.IsImported
                ? Visibility.Visible : Visibility.Collapsed,
            _ => Visibility.Collapsed,
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotImplementedException();
}
