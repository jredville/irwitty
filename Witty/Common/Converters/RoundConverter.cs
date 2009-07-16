using System;
using System.Windows.Data;

namespace Common.Converters
{
    /// <summary>
    /// Takes a double value and round it to the nearest integer
    /// </summary>
    public class RoundConverter : IValueConverter
    {
        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            double inputValue = Math.Round(Double.Parse(value.ToString()));
            return inputValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            try
            {
                double inputValue = Math.Round(Double.Parse(value.ToString()));
                return inputValue;
            }
            catch
            {
                return 0;
            }
        }

        #endregion
    }
}
