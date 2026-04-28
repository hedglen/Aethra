using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace Aethra;

public sealed partial class VideoAdjustmentsPanel : UserControl
{
    private readonly Dictionary<string, Slider> _slidersById = new();
    private readonly Dictionary<string, TextBlock> _readoutsById = new();
    private readonly Dictionary<string, double> _lastEmittedValuesById = new();
    private bool _suppressSliderChange;

    public event EventHandler? CloseRequested;
    public event EventHandler<VideoAdjustmentChangedEventArgs>? AdjustmentChanged;

    public VideoAdjustmentsPanel()
    {
        InitializeComponent();
        BuildSliders();
    }

    private void BuildSliders()
    {
        foreach (var adj in VideoAdjustments.All)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 0) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            row.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var label = new TextBlock
            {
                Text = adj.DisplayName,
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["AethraTextBrush"],
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetRow(label, 0);
            Grid.SetColumn(label, 0);

            var readout = new TextBlock
            {
                Text = FormatValue(adj, adj.Default),
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["AethraMutedTextBrush"],
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0),
                MinWidth = 56,
                TextAlignment = Microsoft.UI.Xaml.TextAlignment.Right,
            };
            Grid.SetRow(readout, 0);
            Grid.SetColumn(readout, 1);
            _readoutsById[adj.Id] = readout;

            var resetBtn = new Button
            {
                Style = (Style)Resources["DrawerResetSmallButtonStyle"],
                Content = new FontIcon
                {
                    FontFamily = new FontFamily("Segoe Fluent Icons"),
                    Glyph = "\uE777",
                    FontSize = 11,
                },
                Tag = adj.Id,
            };
            ToolTipService.SetToolTip(resetBtn, $"Reset {adj.DisplayName}");
            resetBtn.Click += ResetSingle_Click;
            Grid.SetRow(resetBtn, 0);
            Grid.SetColumn(resetBtn, 2);

            var slider = new Slider
            {
                Minimum = adj.Min,
                Maximum = adj.Max,
                Value = adj.Default,
                StepFrequency = adj.Step,
                SmallChange = adj.Step,
                LargeChange = Math.Max(adj.Step * 10, (adj.Max - adj.Min) / 20),
                Tag = adj.Id,
                IsThumbToolTipEnabled = false,
                // Track value-fill follows the live AethraAccentBrush so adjustments
                // sliders match the rest of the app instead of the Windows system accent.
                // The brush instance is mutated in place by AccentColorService, so the
                // slider auto-updates when the accent color is changed in Preferences.
                Foreground = (Brush)Application.Current.Resources["AethraAccentBrush"],
            };
            slider.ValueChanged += Slider_ValueChanged;
            Grid.SetRow(slider, 1);
            Grid.SetColumn(slider, 0);
            Grid.SetColumnSpan(slider, 3);
            _slidersById[adj.Id] = slider;

            row.Children.Add(label);
            row.Children.Add(readout);
            row.Children.Add(resetBtn);
            row.Children.Add(slider);

            SlidersHost.Children.Add(row);
        }
    }

    private void Slider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_suppressSliderChange) return;
        if (sender is not Slider slider || slider.Tag is not string id) return;

        var adj = FindById(id);
        if (adj is null) return;

        var value = NormalizeValue(adj, e.NewValue);
        if (_readoutsById.TryGetValue(id, out var readout))
            readout.Text = FormatValue(adj, value);

        EmitAdjustment(adj, value, force: false);
    }

    private void ResetSingle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string id) return;
        var adj = FindById(id);
        if (adj is null) return;
        ApplyValue(adj, adj.Default);
    }

    private void ResetAllButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var adj in VideoAdjustments.All)
            ApplyValue(adj, adj.Default);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyValue(VideoAdjustment adj, double value)
    {
        if (!_slidersById.TryGetValue(adj.Id, out var slider)) return;

        var normalizedValue = NormalizeValue(adj, value);
        _suppressSliderChange = true;
        try
        {
            slider.Value = normalizedValue;
        }
        finally
        {
            _suppressSliderChange = false;
        }

        if (_readoutsById.TryGetValue(adj.Id, out var readout))
            readout.Text = FormatValue(adj, normalizedValue);

        EmitAdjustment(adj, normalizedValue, force: true);
    }

    private void EmitAdjustment(VideoAdjustment adj, double value, bool force)
    {
        if (!force
            && _lastEmittedValuesById.TryGetValue(adj.Id, out var lastValue)
            && Math.Abs(lastValue - value) < adj.Step / 2)
        {
            return;
        }

        _lastEmittedValuesById[adj.Id] = value;
        AdjustmentChanged?.Invoke(this, new VideoAdjustmentChangedEventArgs(adj.MpvProperty, value));
    }

    private static VideoAdjustment? FindById(string id)
    {
        foreach (var a in VideoAdjustments.All)
            if (a.Id == id) return a;
        return null;
    }

    private static string FormatValue(VideoAdjustment adj, double value) =>
        value.ToString(adj.ValueFormat, CultureInfo.CurrentCulture);

    private static double NormalizeValue(VideoAdjustment adj, double value)
    {
        var clamped = Math.Clamp(value, adj.Min, adj.Max);
        if (adj.Step <= 0)
            return clamped;

        var stepped = Math.Round((clamped - adj.Min) / adj.Step) * adj.Step + adj.Min;
        stepped = Math.Clamp(stepped, adj.Min, adj.Max);
        return Math.Abs(stepped) < adj.Step / 2 ? 0 : stepped;
    }
}

public sealed class VideoAdjustmentChangedEventArgs : EventArgs
{
    public string MpvProperty { get; }
    public double Value { get; }

    public VideoAdjustmentChangedEventArgs(string mpvProperty, double value)
    {
        MpvProperty = mpvProperty;
        Value = value;
    }
}
