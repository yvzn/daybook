using System.Globalization;
using System.Windows.Data;

namespace Daybook.App.Converters;

/// <summary>true when both bound values are the same object: "is this row the chosen one?"</summary>
public sealed class SameObjectConverter : IMultiValueConverter
{
    public static readonly SameObjectConverter Instance = new();

    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture) =>
        values.Length == 2 && ReferenceEquals(values[0], values[1]);

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
