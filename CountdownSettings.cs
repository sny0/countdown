using System;
using System.Globalization;
using System.IO;
using System.Windows.Media;
using System.Xml;
using System.Xml.Linq;

namespace Countdown
{
    internal sealed class CountdownSettings
    {
        internal const double DefaultShadowOpacity = 85;
        internal const double DefaultShadowBlur = 7;

        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Countdown", "settings.xml");

        internal int DurationSeconds { get; set; } = 300;
        internal double BackgroundOpacity { get; set; } = 100;
        internal Color? DigitColor { get; set; }
        internal int DigitWeight { get; set; } = 600;
        internal double ShadowOpacity { get; set; } = DefaultShadowOpacity;
        internal double ShadowBlur { get; set; } = DefaultShadowBlur;

        internal static CountdownSettings Load()
        {
            var settings = new CountdownSettings();
            try
            {
                var root = XElement.Load(SettingsPath);
                if (root.Name != "countdownSettings")
                {
                    return settings;
                }

                settings.DurationSeconds = ReadInt(root, "durationSeconds", 1, 359999, settings.DurationSeconds);
                settings.BackgroundOpacity = ReadDouble(root, "backgroundOpacity", 100, settings.BackgroundOpacity);
                settings.DigitColor = ReadColor((string)root.Attribute("digitColor"));
                settings.DigitWeight = ReadInt(root, "digitWeight", 1, 999, settings.DigitWeight);
                settings.ShadowOpacity = ReadDouble(root, "shadowOpacity", 100, settings.ShadowOpacity);
                settings.ShadowBlur = ReadDouble(root, "shadowBlur", 20, settings.ShadowBlur);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is XmlException)
            {
                // Missing or unreadable settings leave the default values available.
            }

            return settings;
        }

        internal void SaveIfChanged(CountdownSettings previous)
        {
            var root = ToXml();
            if (XNode.DeepEquals(root, previous.ToXml()))
            {
                return;
            }

            var directory = Path.GetDirectoryName(SettingsPath);
            Directory.CreateDirectory(directory);
            var temporaryPath = Path.Combine(directory, Path.GetRandomFileName());
            try
            {
                using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write))
                {
                    root.Save(stream);
                }

                try
                {
                    File.Move(temporaryPath, SettingsPath);
                }
                catch (IOException) when (File.Exists(SettingsPath))
                {
                    File.Replace(temporaryPath, SettingsPath, null);
                }
            }
            finally
            {
                File.Delete(temporaryPath);
            }
        }

        private XElement ToXml()
        {
            var root = new XElement("countdownSettings",
                new XAttribute("durationSeconds", DurationSeconds),
                new XAttribute("backgroundOpacity", BackgroundOpacity),
                new XAttribute("digitWeight", DigitWeight),
                new XAttribute("shadowOpacity", ShadowOpacity),
                new XAttribute("shadowBlur", ShadowBlur));

            if (DigitColor is Color color)
            {
                root.SetAttributeValue("digitColor", FormattableString.Invariant($"#{color.R:X2}{color.G:X2}{color.B:X2}"));
            }

            return root;
        }

        private static int ReadInt(XElement root, string name, int min, int max, int fallback)
        {
            return int.TryParse((string)root.Attribute(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                && value >= min && value <= max ? value : fallback;
        }

        private static double ReadDouble(XElement root, string name, double max, double fallback)
        {
            return double.TryParse((string)root.Attribute(name), NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                && value >= 0 && value <= max ? value : fallback;
        }

        private static Color? ReadColor(string value)
        {
            if (value?.Length == 7 && value[0] == '#' &&
                int.TryParse(value.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb))
            {
                return Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
            }

            return null;
        }
    }
}
