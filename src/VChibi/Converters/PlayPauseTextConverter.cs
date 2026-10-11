using System.Globalization;
using System.Windows.Data;

namespace VChibi.Converters;

public class PlayPauseTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isPlaying)
        {
            return isPlaying ? "Tạm dừng" : "Phát tiếp";
        }
        return "Tạm dừng";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
