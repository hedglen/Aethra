using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Aethra;

public sealed partial class SettingsPanel : UserControl
{
    public event EventHandler? CloseRequested;
    public event EventHandler? OpenAllSettingsRequested;
    private bool _syncingAccentText;

    public SettingsPanel()
    {
        InitializeComponent();
        InitializeAccentControls();
        AccentColorService.AccentColorChanged += AccentColorService_AccentColorChanged;
        Unloaded += SettingsPanel_Unloaded;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void AllSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        OpenAllSettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem selectedItem)
        {
            var tag = selectedItem.Tag?.ToString() ?? string.Empty;

            VideoPanel.Visibility = tag == "Video" ? Visibility.Visible : Visibility.Collapsed;
            ShadersPanel.Visibility = tag == "Shaders" ? Visibility.Visible : Visibility.Collapsed;
            AudioPanel.Visibility = tag == "Audio" ? Visibility.Visible : Visibility.Collapsed;
            AccentPanel.Visibility = tag == "Accent" ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void InitializeAccentControls()
    {
        SyncAccentText(AccentColorService.CurrentHex);
        AccentStatusText.Text = $"Using {AccentColorService.CurrentHex}";
    }

    private void AccentHexBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_syncingAccentText)
            return;

        var text = AccentHexBox.Text;
        AccentStatusText.Text = AccentColorService.TryParseHexColor(text, out _, out var normalizedHex)
            ? $"Ready to apply {normalizedHex}"
            : "Enter a hex color like #7B2FFF or #A0F.";
    }

    private void ApplyAccentButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyAccentFromText();
    }

    private void ResetAccentButton_Click(object sender, RoutedEventArgs e)
    {
        AccentColorService.TryApplyHex(AccentColorService.DefaultAccentHex, out var normalizedHex);
        SyncAccentText(normalizedHex);
        AccentStatusText.Text = $"Using {normalizedHex}";
    }

    private void ApplyAccentFromText()
    {
        if (AccentColorService.TryApplyHex(AccentHexBox.Text, out var normalizedHex))
        {
            SyncAccentText(normalizedHex);
            AccentStatusText.Text = $"Using {normalizedHex}";
            return;
        }

        AccentStatusText.Text = "Enter a hex color like #7B2FFF or #A0F.";
    }

    private void AccentColorService_AccentColorChanged(object? sender, AccentColorChangedEventArgs e)
    {
        SyncAccentText(e.Hex);
        AccentStatusText.Text = $"Using {e.Hex}";
    }

    private void SettingsPanel_Unloaded(object sender, RoutedEventArgs e)
    {
        AccentColorService.AccentColorChanged -= AccentColorService_AccentColorChanged;
    }

    private void SyncAccentText(string hex)
    {
        _syncingAccentText = true;
        try
        {
            AccentHexBox.Text = hex;
        }
        finally
        {
            _syncingAccentText = false;
        }
    }
}
