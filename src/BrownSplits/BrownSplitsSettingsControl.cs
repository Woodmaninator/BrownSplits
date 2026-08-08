using System;
using System.Drawing;
using System.Windows.Forms;

namespace BrownSplits;

internal sealed class BrownSplitsSettingsControl : UserControl
{
    private readonly BrownSplitsSettings settings;
    private readonly Button colorButton;
    private readonly CheckBox usePercentileCheckBox;
    private readonly NumericUpDown percentileInput;
    private readonly CheckBox limitHistoryCheckBox;
    private readonly NumericUpDown recentCountInput;

    public BrownSplitsSettingsControl(BrownSplitsSettings settings)
    {
        this.settings = settings;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Dock = DockStyle.Fill;
        Padding = new Padding(8);

        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            Dock = DockStyle.Top,
            Padding = new Padding(0),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        colorButton = new Button
        {
            BackColor = settings.OverrideColor,
            AccessibleDescription = "Selects the color used for qualifying slow splits.",
            AccessibleName = "Brown split override color",
            FlatStyle = FlatStyle.Flat,
            Height = 25,
            UseVisualStyleBackColor = false,
            Width = 180,
        };
        UpdateColorButton();
        colorButton.Click += ChooseColor;

        percentileInput = new NumericUpDown
        {
            AccessibleDescription = "The historical split-time percentile that a completed segment must reach.",
            AccessibleName = "Bad-time percentile",
            DecimalPlaces = 1,
            Enabled = settings.UsePercentile,
            Increment = 0.5M,
            Maximum = 100,
            Minimum = 0,
            Value = (decimal)settings.Percentile,
            Width = 90,
        };
        percentileInput.ValueChanged += (_, _) => settings.Percentile = (double)percentileInput.Value;

        usePercentileCheckBox = new CheckBox
        {
            AccessibleDescription = "When disabled, only a split as slow as the slowest recorded time turns brown.",
            AccessibleName = "Use percentile threshold",
            AutoSize = true,
            Checked = settings.UsePercentile,
            Text = "Use a percentile threshold",
        };
        usePercentileCheckBox.CheckedChanged += (_, _) =>
        {
            settings.UsePercentile = usePercentileCheckBox.Checked;
            percentileInput.Enabled = usePercentileCheckBox.Checked;
        };

        limitHistoryCheckBox = new CheckBox
        {
            AccessibleDescription = "Restricts the historical sample to the newest recorded times for this segment.",
            AccessibleName = "Limit split-time history",
            AutoSize = true,
            Checked = settings.LimitToRecentAttempts,
            Text = "Only use the most recent split times",
        };
        recentCountInput = new NumericUpDown
        {
            AccessibleDescription = "The maximum number of recent recorded times used for this segment.",
            AccessibleName = "Recent split-time count",
            Enabled = settings.LimitToRecentAttempts,
            Maximum = 100000,
            Minimum = 1,
            Value = settings.RecentAttemptCount,
            Width = 90,
        };
        recentCountInput.ValueChanged += (_, _) => settings.RecentAttemptCount = (int)recentCountInput.Value;
        limitHistoryCheckBox.CheckedChanged += (_, _) =>
        {
            settings.LimitToRecentAttempts = limitHistoryCheckBox.Checked;
            recentCountInput.Enabled = limitHistoryCheckBox.Checked;
        };

        AddDescription(layout, 0,
            "BrownSplits changes semantic live timers and deltas as soon as the segment meets all enabled conditions below (If the split was hot garbage).",
            false);
        AddRow(layout, 1, "Brown Split Color:", colorButton);
        AddDescription(layout, 2,
            "The replacement color used everywhere LiveSplit uses its shared delta and best-segment colors.");
        AddSpanningControl(layout, 3, usePercentileCheckBox);
        AddRow(layout, 4, "Shit-Time Percentile:", percentileInput);
        AddDescription(layout, 5,
            "If enabled, 95 means the live or completed segment must be at least as slow as the 95th percentile. Otherwise it must reach the slowest recorded time, to be considered a Brown Split.");
        AddSpanningControl(layout, 6, limitHistoryCheckBox);
        AddRow(layout, 7, "Split-History:", recentCountInput);
        AddDescription(layout, 8,
            "When enabled, only the newest N recorded times for this segment are considered for the calculation of the Brown Split.");
        AddDescription(layout, 9,
            "The live or completed segment must always be strictly slower than its best segment time in order to become Brown Splits.",
            false);

        Controls.Add(layout);
    }

    private static void AddRow(TableLayoutPanel layout, int row, string labelText, Control control)
    {
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var label = new Label
        {
            Anchor = AnchorStyles.Left,
            AutoSize = true,
            Margin = new Padding(0, 6, 12, 6),
            Text = labelText,
        };
        control.Anchor = AnchorStyles.Left;
        control.Margin = new Padding(0, 3, 0, 3);
        layout.Controls.Add(label, 0, row);
        layout.Controls.Add(control, 1, row);
    }

    private static void AddSpanningControl(TableLayoutPanel layout, int row, Control control)
    {
        control.Anchor = AnchorStyles.Left;
        control.Margin = new Padding(0, 8, 0, 3);
        layout.Controls.Add(control, 0, row);
        layout.SetColumnSpan(control, 2);
    }

    private static void AddDescription(TableLayoutPanel layout, int row, string text, bool muted = true)
    {
        var description = new Label
        {
            AutoSize = true,
            ForeColor = muted ? SystemColors.GrayText : SystemColors.ControlText,
            Margin = new Padding(0, 3, 0, 6),
            MaximumSize = new Size(460, 0),
            Text = text,
        };
        layout.Controls.Add(description, 0, row);
        layout.SetColumnSpan(description, 2);
    }

    private void ChooseColor(object? sender, EventArgs eventArgs)
    {
        using var dialog = new ColorDialog
        {
            AllowFullOpen = true,
            AnyColor = true,
            Color = settings.OverrideColor,
            FullOpen = true,
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            settings.OverrideColor = dialog.Color;
            UpdateColorButton();
        }
    }

    private void UpdateColorButton()
    {
        colorButton.BackColor = settings.OverrideColor;
        int perceivedBrightness = ((settings.OverrideColor.R * 299)
            + (settings.OverrideColor.G * 587)
            + (settings.OverrideColor.B * 114)) / 1000;
        colorButton.ForeColor = perceivedBrightness >= 128 ? Color.Black : Color.White;
        colorButton.Text = $"Choose... (#{settings.OverrideColor.R:X2}{settings.OverrideColor.G:X2}{settings.OverrideColor.B:X2})";
    }
}
