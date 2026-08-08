using System;
using System.Drawing;
using System.Xml;
using LiveSplit.UI;

namespace BrownSplits;

internal sealed class BrownSplitsSettings
{
    internal const double DefaultPercentile = 90.0; // How bad the splits have to be to be considered "brown"
    internal const int DefaultRecentAttemptCount = 20; // How many recent attempts are considered when calculating the percentile
    internal static readonly Color DefaultOverrideColor = Color.FromArgb(0x5F, 0x40, 0x00); // Color of the brown splits

    public Color OverrideColor { get; set; } = DefaultOverrideColor;

    public bool UsePercentile { get; set; } = true;

    public double Percentile { get; set; } = DefaultPercentile;

    public bool LimitToRecentAttempts { get; set; }

    public int RecentAttemptCount { get; set; } = DefaultRecentAttemptCount;

    public int GetEvaluationHashCode()
    {
        unchecked
        {
            int hashCode = OverrideColor.GetHashCode();
            hashCode = (hashCode * 397) ^ UsePercentile.GetHashCode();
            hashCode = (hashCode * 397) ^ Percentile.GetHashCode();
            hashCode = (hashCode * 397) ^ LimitToRecentAttempts.GetHashCode();
            return (hashCode * 397) ^ RecentAttemptCount;
        }
    }

    public XmlNode ToXml(XmlDocument document)
    {
        XmlElement settings = document.CreateElement("Settings");
        SettingsHelper.CreateSetting(document, settings, nameof(OverrideColor), OverrideColor);
        SettingsHelper.CreateSetting(document, settings, nameof(UsePercentile), UsePercentile);
        SettingsHelper.CreateSetting(document, settings, nameof(Percentile), Percentile);
        SettingsHelper.CreateSetting(document, settings, nameof(LimitToRecentAttempts), LimitToRecentAttempts);
        SettingsHelper.CreateSetting(document, settings, nameof(RecentAttemptCount), RecentAttemptCount);
        return settings;
    }

    public void FromXml(XmlNode? node)
    {
        if (node is not XmlElement settings)
        {
            return;
        }

        OverrideColor = SettingsHelper.ParseColor(settings[nameof(OverrideColor)], DefaultOverrideColor);
        XmlElement? percentileElement = settings[nameof(Percentile)];
        UsePercentile = percentileElement is not null
            && SettingsHelper.ParseBool(settings[nameof(UsePercentile)], true);
        Percentile = Math.Max(0.0, Math.Min(100.0,
            SettingsHelper.ParseDouble(percentileElement, DefaultPercentile)));
        LimitToRecentAttempts = SettingsHelper.ParseBool(settings[nameof(LimitToRecentAttempts)]);
        RecentAttemptCount = Math.Max(1,
            SettingsHelper.ParseInt(settings[nameof(RecentAttemptCount)], DefaultRecentAttemptCount));
    }
}
