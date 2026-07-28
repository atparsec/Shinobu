using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Shinobu.Helpers;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage;

namespace Shinobu.Controls;

public sealed partial class LLMChatControl : UserControl
{
    private LLMProvider? _provider;
    private string _selectedText = string.Empty;
    private bool _initialized;
    private JlptLevel _jlptLevel = ApplicationData.Current.LocalSettings.Values.TryGetValue("JlptLevel", out object? levelObj) && levelObj is int levelInt ? (JlptLevel)levelInt : JlptLevel.N5;

    public LLMChatControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    public string SelectedText
    {
        get => _selectedText;
        set
        {
            if (_selectedText != value)
            {
                _selectedText = value ?? string.Empty;
                if (_initialized)
                {
                    PrepareInitialPrompt();
                }
            }
        }
    }

    private string _fileName = "N/A";
    public string FileName {
        get => _fileName;
        set
        {
            if (_fileName != value)
            {
                _fileName = value ?? "N/A";
            }
        }
    } 

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _initialized = true;
        await InitializeProviderAsync();
        PrepareInitialPrompt();
    }

    private void PrepareInitialPrompt()
    {
        MessagesPanel.Children.Clear();

        if (_provider == null)
        {
            AddMessage("System", "LLM provider not configured. Check AI settings (URL / Key).");
            InputTextBox.IsEnabled = false;
            SendButton.IsEnabled = false;
            ClearButton.IsEnabled = true;
            return;
        }

        if (string.IsNullOrWhiteSpace(_selectedText))
        {
            AddMessage("System", "No text selected.");
            InputTextBox.IsEnabled = false;
            SendButton.IsEnabled = false;
            ClearButton.IsEnabled = true;
            ExplainEnglishButton.IsEnabled = false;
            ExplainJapaneseButton.IsEnabled = false;
            TranslateButton.IsEnabled = false;
            SelectedTextHeader.Visibility = Visibility.Collapsed;
            return;
        }

        string trimmedSelection = _selectedText.Length > 20 ? _selectedText.Substring(0, 20) + "..." : _selectedText;
        SelectedTextHeader.Text = $"\"{trimmedSelection}\"";
        SelectedTextHeader.Visibility = Visibility.Visible;

        InputTextBox.Text = string.Empty;
        InputTextBox.IsEnabled = true;
        SendButton.IsEnabled = true;
        ClearButton.IsEnabled = true;
        ExplainEnglishButton.IsEnabled = true;
        ExplainJapaneseButton.IsEnabled = true;
        TranslateButton.IsEnabled = true;
    }

    private async Task InitializeProviderAsync()
    {
        ApplicationDataContainer settings = ApplicationData.Current.LocalSettings;

        string url = settings.Values.TryGetValue("LLMUrl", out object? u) && u is string s && !string.IsNullOrWhiteSpace(s)
            ? s
            : "http://localhost:11434";

        string? key = settings.Values.TryGetValue("ApiKey", out object? k) && k is string ks && !string.IsNullOrWhiteSpace(ks)
            ? ks
            : null;

        try
        {
            _provider = new LLMProvider(url, key);
            var models = await _provider.GetModelsAsync();
            ModelComboBox.ItemsSource = models;
            if (models.Count > 0)
            {
                ModelComboBox.SelectedIndex = 0;
            }
            ModelComboBox.IsEnabled = true;
        }
        catch
        {
            _provider = null;
        }
    }



    private async void SendButton_Click(object sender, RoutedEventArgs e)
    {
        await SendFollowUpAsync();
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        MessagesPanel.Children.Clear();
        _provider?.ClearChatContext();

        InputTextBox.Text = string.Empty;
        bool hasSelection = !string.IsNullOrWhiteSpace(_selectedText);
        InputTextBox.IsEnabled = hasSelection;
        SendButton.IsEnabled = hasSelection && _provider != null;
        ExplainEnglishButton.IsEnabled = hasSelection && _provider != null;
        ExplainJapaneseButton.IsEnabled = hasSelection && _provider != null;
        TranslateButton.IsEnabled = hasSelection && _provider != null;

        if (hasSelection)
        {
            string trimmedSelection = _selectedText.Length > 20 ? _selectedText.Substring(0, 20) + "..." : _selectedText;
            SelectedTextHeader.Text = $"\" {trimmedSelection} \"";
            SelectedTextHeader.Visibility = Visibility.Visible;
        }
        else
        {
            SelectedTextHeader.Visibility = Visibility.Collapsed;
        }
    }

    private async void ExplainEnglishButton_Click(object sender, RoutedEventArgs e)
    {
        await SendFollowUpAsync($"Explain the Japanese text selection in English. Give readings and meanings for words at or above the {_jlptLevel} level. Use simple plaintext formatting.");
    }

    private async void ExplainJapaneseButton_Click(object sender, RoutedEventArgs e)
    {
        await SendFollowUpAsync($"Explain in {_jlptLevel} Japanese, Give readings and meanings for words at or above the {_jlptLevel} level. Use simple plaintext formatting.");
    }

    private async void TranslateButton_Click(object sender, RoutedEventArgs e)
    {
        await SendFollowUpAsync("Translate the given text to english. Use simple plaintext formatting.");
    }

    private async void InputTextBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter && !string.IsNullOrWhiteSpace(InputTextBox.Text))
        {
            e.Handled = true;
            await SendFollowUpAsync();
        }
    }

    private async Task SendFollowUpAsync(string? instruction = null)
    {
        string userInput = instruction ?? InputTextBox.Text.Trim();
        if (_provider == null || string.IsNullOrWhiteSpace(userInput) || ModelComboBox.SelectedItem == null)
        {
            return;
        }

        string selectedModel = ModelComboBox.SelectedItem.ToString() ?? string.Empty;
        AddMessage("You", userInput);

        bool isFirstSend = !HasAiResponse();
        string toSend = isFirstSend
            ? $"Source: \"{_fileName}\"\n\nSelection Text: \"{_selectedText}\"\n\nInstruction: {userInput}"
            : userInput;

        InputTextBox.Text = string.Empty;
        InputTextBox.IsEnabled = false;
        SendButton.IsEnabled = false;
        ModelComboBox.IsEnabled = false;
        ExplainEnglishButton.IsEnabled = false;
        ExplainJapaneseButton.IsEnabled = false;
        TranslateButton.IsEnabled = false;

        var aiResponseBorder = AddMessage("AI", string.Empty);
        var aiResponseTextBlock = (TextBlock)aiResponseBorder.Child;

        try
        {
            var resultBuilder = new System.Text.StringBuilder();

            await Task.Run(async () =>
            {
                await foreach (var chunk in _provider.GetCompletionStreamAsync(toSend, selectedModel).ConfigureAwait(false))
                {
                    resultBuilder.Append(chunk);
                    var currentText = resultBuilder.ToString();

                    DispatcherQueue.TryEnqueue(() =>
                    {
                        aiResponseTextBlock.Text = currentText;
                        ScrollToBottom();
                    });
                }
            });

            _provider.AddChatContext($"User: {userInput}\nAI: {resultBuilder}");
        }
        catch (Exception ex)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                aiResponseTextBlock.Text += $"\n[Error: {ex.Message}]";
            });
        }
        finally
        {
            InputTextBox.IsEnabled = true;
            SendButton.IsEnabled = true;
            ModelComboBox.IsEnabled = true;
            ExplainEnglishButton.IsEnabled = true;
            ExplainJapaneseButton.IsEnabled = true;
            TranslateButton.IsEnabled = true;
            ScrollToBottom();
        }
    }

    private bool HasAiResponse()
    {
        foreach (var child in MessagesPanel.Children)
        {
            if (child is Border b && b.Child is TextBlock tb)
            {
                if (tb.Text != "Thinking..." && MessagesPanel.Children.IndexOf(child) > 0)
                    return true;
            }
        }
        return false;
    }

    private Border AddMessage(string role, string text)
    {
        var border = new Border
        {
            Background = role switch
            {
                "You" => (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AccentFillColorDefaultBrush"],
                "AI" => (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                _ => (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"]
            },
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(0, 2, 0, 2),
            Child = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 14
            }
        };

        if (role == "You")
        {
            border.HorizontalAlignment = HorizontalAlignment.Right;
            ((TextBlock)border.Child).Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextOnAccentFillColorPrimaryBrush"];
        }

        MessagesPanel.Children.Add(border);
        ScrollToBottom();

        return border;
    }

    private void ScrollToBottom()
    {
        var dq = this.DispatcherQueue;
        if (dq != null)
        {
            _ = dq.TryEnqueue(() =>
            {
                ChatScrollViewer.ChangeView(null, ChatScrollViewer.ScrollableHeight, null);
            });
        }
    }
}
