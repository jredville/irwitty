using System;
using System.Windows.Data;
using TwitterLib;

namespace Common.Converters
{
    public class CharRemainingValueConverter : IValueConverter
    {
        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            string[] statuses = TweetSplitter.SplitTweet((string) value);
            if (statuses.Length == 0) return TwitterNet.CharacterLimit;
            if (statuses.Length == 1) return TwitterNet.CharacterLimit - statuses[0].Length;
            return string.Format("{1}: {0}", statuses[statuses.Length - 1].Length % TwitterNet.CharacterLimit, statuses.Length);
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException("The method or operation is not implemented.");
        }

        #endregion
    }

    public class CharRemainingForegroundColorConverter : IValueConverter
    {

        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            float warningSize = 15F;

            if (targetType != typeof(System.Windows.Media.Brush))
                return null;

            int i = (int)value;

            if (i > TwitterNet.CharacterLimit)
                return System.Windows.Media.Brushes.Red;

            float threshhold = (float)TwitterNet.CharacterLimit - warningSize;

            float delta = (float)i - threshhold;

            byte a = 0xff;
            byte r = 0x52;
            byte g = 0x62;
            byte b = 0x6F;

            if (delta > 0)
            {
                float ratio = (0.6F * delta / warningSize); // gradually go towards red, but snap to full red when length exceeded

                r += (byte)((0xff - r) * ratio);
                g -= (byte)(g * ratio);
                b -= (byte)(b * ratio);
            }

            return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(a, r, b, g));

        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
