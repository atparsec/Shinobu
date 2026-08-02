using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Shinobu.Helpers.Books;
using Shinobu.Helpers.Content;
using Shinobu.Helpers.Dictionary;
using Shinobu.Helpers.Reader;
using Shinobu.Helpers.Services;
using Shinobu.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.System;

namespace Shinobu.Controls.Chat;

public sealed partial class LLMChatControl : UserControl
{
    private sealed class ChatTurn
    {
        public required string Role { get; set; }
        public required string Content { get; set; }
        public string? ToolCallId { get; set; }
        public string? ToolName { get; set; }
        public IReadOnlyList<LlmToolCall>? ToolCalls { get; set; }
    }

    private static readonly HttpClient ToolHttpClient = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly List<ChatTurn> _conversation = [];
    private LLMProvider? _provider;
    private string _selectedText = string.Empty;
    private string _fileName = "N/A";
    private string _currentPageText = string.Empty;
    private string _systemPrompt = string.Empty;
    private bool _initialized;
    private bool _isSending;
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
            string next = value ?? string.Empty;
            if (_selectedText != next)
            {
                _selectedText = next;
                if (_initialized)
                {
                    PrepareInitialPrompt();
                }
            }
        }
    }

    public string FileName
    {
        get => _fileName;
        set
        {
            string next = string.IsNullOrWhiteSpace(value) ? "N/A" : value;
            if (_fileName != next)
            {
                _fileName = next;
                if (_initialized)
                {
                    PrepareInitialPrompt();
                }
            }
        }
    }

    public string CurrentPageText
    {
        get => _currentPageText;
        set
        {
            string next = value ?? string.Empty;
            if (_currentPageText != next)
            {
                _currentPageText = next;
                if (_initialized)
                {
                    PrepareInitialPrompt();
                }
            }
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _initialized = true;
        await InitializeProviderAsync();
        PrepareInitialPrompt();
    }

    private async Task InitializeProviderAsync()
    {
        ApplicationDataContainer settings = ApplicationData.Current.LocalSettings;

        string url = settings.Values.TryGetValue("LLMUrl", out object? u) && u is string s && !string.IsNullOrWhiteSpace(s)
            ? s
            : "http://localhost:5000";

        string? key = settings.Values.TryGetValue("ApiKey", out object? k) && k is string ks && !string.IsNullOrWhiteSpace(ks)
            ? ks
            : null;

        try
        {
            _provider = new LLMProvider(url, key);
            var models = await _provider.GetModelsAsync();
            ModelComboBox.ItemsSource = models;
            ModelComboBox.IsEnabled = models.Count > 0;
            if (models.Count > 0)
            {
                ModelComboBox.SelectedIndex = 0;
            }
        }
        catch
        {
            _provider = null;
            ModelComboBox.ItemsSource = null;
            ModelComboBox.IsEnabled = false;
        }
    }

    private void PrepareInitialPrompt()
    {
        MessagesPanel.Children.Clear();
        _conversation.Clear();
        _systemPrompt = BuildSystemPrompt();

        if (_provider == null)
        {
            AddSystemNotice("LLM provider not configured. Check AI settings (URL / Key).");
            SetControlsEnabled(false);
            ClearButton.IsEnabled = true;
            //SelectedTextHeader.Visibility = Visibility.Collapsed;
            return;
        }

        if (string.IsNullOrWhiteSpace(_selectedText))
        {
            AddSystemNotice("No text selected.");
            SetControlsEnabled(false);
            ClearButton.IsEnabled = true;
            // SelectedTextHeader.Visibility = Visibility.Collapsed;
            return;
        }

        string trimmedSelection = Truncate(_selectedText, 20);
        //SelectedTextHeader.Text = $"\"{trimmedSelection}\"";
        //SelectedTextHeader.Visibility = Visibility.Visible;

        SetControlsEnabled(true);
        InputTextBox.Text = string.Empty;
    }

    private void SetControlsEnabled(bool enabled)
    {
        bool hasSelection = enabled && !string.IsNullOrWhiteSpace(_selectedText);
        bool hasProvider = enabled && _provider != null;
        bool canChat = hasSelection && hasProvider && ModelComboBox.SelectedItem != null;

        InputTextBox.IsEnabled = canChat && !_isSending;
        SendButton.IsEnabled = canChat && !_isSending;
        ExplainEnglishButton.IsEnabled = canChat && !_isSending;
        ExplainJapaneseButton.IsEnabled = canChat && !_isSending;
        TranslateButton.IsEnabled = canChat && !_isSending;
        ClearButton.IsEnabled = true;
        ModelComboBox.IsEnabled = _provider != null && ModelComboBox.ItemsSource is { } models && ((System.Collections.ICollection)models).Count > 0;
    }

    private string BuildSystemPrompt()
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a reading assistant for learning Japanese.");
        sb.AppendLine("Use tools when they help answer the user's request:");
        sb.AppendLine("- web_search(query): search the web for up-to-date information - get correct readings for character names, context, etc.");
        sb.AppendLine("- current_page_search(query): search the current reader page text for matching terms or phrases, or get context.");
        sb.AppendLine("When you use a tool, say so briefly in the response.");
        sb.AppendLine("Use simple plaintext formatting, conversational style and tone.");
        sb.AppendLine($"Source: {_fileName}");
        sb.AppendLine($"Selected text: {Truncate(_selectedText, 800)}");
        //if (!string.IsNullOrWhiteSpace(_currentPageText))
        //{
        //    sb.AppendLine($"Current page preview: {Truncate(_currentPageText, 1000)}");
        //}

        return sb.ToString();
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength] + "...";
    }

    private void AddSystemNotice(string text)
    {
        var bubble = CreateBubbleVisual(
            role: "System",
            text: text,
            isLoading: false,
            isUser: false,
            isTool: false,
            showActions: false);

        bubble.Root.HorizontalAlignment = HorizontalAlignment.Stretch;
    }

    private sealed class BubbleVisualWrapper
    {
        public BubbleVisualWrapper(StackPanel outer, Border root, TextBlock body, ProgressRing spinner, TextBlock status, StackPanel actions)
        {
            Outer = outer;
            Root = root;
            Body = body;
            Spinner = spinner;
            Status = status;
            Actions = actions;
        }

        public StackPanel Outer { get; }
        public Border Root { get; }
        public TextBlock Body { get; }
        public ProgressRing Spinner { get; }
        public TextBlock Status { get; }
        public StackPanel Actions { get; }
    }

    private BubbleVisualWrapper CreateBubbleVisual(
        string role,
        string text,
        bool isLoading,
        bool isUser,
        bool isTool,
        bool showActions,
        int historyIndex = -1,
        string? statusText = null)
    {
        var outer = new StackPanel
        {
            Spacing = 4,
            HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            MaxWidth = 740
        };

        outer.Children.Add(new TextBlock
        {
            Text = role,
            FontSize = 11,
            Opacity = 0.65,
            Margin = new Thickness(isUser ? 0 : 4, 0, isUser ? 4 : 0, 0),
            HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left
        });

        var body = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.WrapWholeWords,
            FontSize = 14,
            LineHeight = 20,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MaxWidth = 640
        };

        var spinner = new ProgressRing
        {
            IsActive = isLoading,
            Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed,
            Width = 16,
            Height = 16,
            Margin = new Thickness(0, 0, 8, 0)
        };

        var status = new TextBlock
        {
            Text = statusText ?? string.Empty,
            TextWrapping = TextWrapping.WrapWholeWords,
            MaxWidth = 640,
            FontSize = 12,
            Opacity = 0.72,
            Margin = new Thickness(0, 4, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var contentRow = new Grid();
        contentRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        contentRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        contentRow.HorizontalAlignment = HorizontalAlignment.Stretch;
        contentRow.VerticalAlignment = VerticalAlignment.Top;

        if (isLoading)
        {
            Grid.SetColumn(spinner, 0);
            contentRow.Children.Add(spinner);
        }

        Grid.SetColumn(body, isLoading ? 1 : 0);
        contentRow.Children.Add(body);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            Visibility = showActions ? Visibility.Visible : Visibility.Collapsed
        };

        if (isUser)
        {
            var editButton = new Button
            {
                Content = new FontIcon { Glyph = "\uE70F", FontSize = 12 },
                Padding = new Thickness(8, 4, 8, 4),
                MinHeight = 28,
                MinWidth = 0,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            ToolTipService.SetToolTip(editButton, "Edit and resend");
            editButton.Click += async (_, _) => await EditAndResendAsync(historyIndex);

            var regenButton = new Button
            {
                Content = new FontIcon { Glyph = "\uE72C", FontSize = 12 },
                Padding = new Thickness(8, 4, 8, 4),
                MinHeight = 28,
                MinWidth = 0,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            ToolTipService.SetToolTip(regenButton, "Regenerate response");
            regenButton.Click += async (_, _) => await RegenerateFromAsync(historyIndex);

            actions.Children.Add(editButton);
            actions.Children.Add(regenButton);
        }
        else if (role == "AI")
        {
            var copyButton = new Button
            {
                Content = new FontIcon { Glyph = "\uE8C8", FontSize = 12 },
                Padding = new Thickness(8, 4, 8, 4),
                MinHeight = 28,
                MinWidth = 0,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            ToolTipService.SetToolTip(copyButton, "Copy response");
            copyButton.Click += async (_, _) => await CopyTextAsync(body.Text);
            actions.Children.Add(copyButton);
        }

        var bubble = new Border
        {
            Background = role switch
            {
                "You" => (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"],
                "AI" => (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                "Tool" => (Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"],
                _ => (Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"]
            },
            BorderBrush = isTool ? (Brush)Application.Current.Resources["AccentFillColorSecondaryBrush"] : null,
            BorderThickness = isTool ? new Thickness(1) : new Thickness(0),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 2, 0, 2),
            Child = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    contentRow,
                    status,
                    actions
                }
            }
        };

        if (isUser)
        {
            body.Foreground = (Brush)Application.Current.Resources["TextOnAccentFillColorPrimaryBrush"];
        }

        outer.Children.Add(bubble);
        MessagesPanel.Children.Add(outer);
        ScrollToBottom();

        return new BubbleVisualWrapper(outer, bubble, body, spinner, status, actions);
    }

    private static string GetToolUsageStatus(IReadOnlyList<LlmToolCall>? toolCalls)
    {
        if (toolCalls is null || toolCalls.Count == 0)
        {
            return string.Empty;
        }

        return string.Join("\n", toolCalls.Select(t => $"{t.Name} tool used"));
    }

    private void AddHistoryBubble(int index)
    {
        var turn = _conversation[index];
        if (turn.Role == "system")
        {
            return;
        }

        if (turn.Role == "user")
        {
            CreateBubbleVisual("You", turn.Content, false, true, false, true, index);
            return;
        }

        if (turn.Role == "assistant")
        {
            if (turn.ToolCalls is { Count: > 0 })
            {
                CreateBubbleVisual(
                    "AI",
                    turn.Content,
                    false,
                    false,
                    false,
                    true,
                    statusText: GetToolUsageStatus(turn.ToolCalls));
                return;
            }

            CreateBubbleVisual("AI", turn.Content, false, false, false, true);
            return;
        }

        if (turn.Role == "tool")
        {
            return;
        }
    }

    private void RenderConversationHistory()
    {
        MessagesPanel.Children.Clear();
        for (int i = 0; i < _conversation.Count; i++)
        {
            AddHistoryBubble(i);
        }
    }

    private void AddUserTurn(string text)
    {
        _conversation.Add(new ChatTurn
        {
            Role = "user",
            Content = text
        });

        AddHistoryBubble(_conversation.Count - 1);
    }

    private BubbleVisualWrapper AddAssistantLoadingBubble(string text = "Thinking...")
    {
        return CreateBubbleVisual("AI", text, true, false, false, false);
    }

    private void UpdateBubble(BubbleVisualWrapper bubble, string text, bool loading, string? statusText = null)
    {
        bubble.Body.Text = text;
        if (statusText is not null)
        {
            bubble.Status.Text = statusText;
        }

        bubble.Spinner.IsActive = loading;
        bubble.Spinner.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
        bubble.Actions.Visibility = Visibility.Collapsed;
        ScrollToBottom();
    }

    private async Task SendFollowUpAsync(string? instruction = null)
    {
        string userInput = instruction ?? InputTextBox.Text.Trim();
        if (_provider == null || _isSending || string.IsNullOrWhiteSpace(userInput) || ModelComboBox.SelectedItem == null)
        {
            return;
        }

        string selectedModel = ModelComboBox.SelectedItem.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(selectedModel))
        {
            return;
        }

        if (_conversation.Count == 0)
        {
            _conversation.Add(new ChatTurn
            {
                Role = "system",
                Content = _systemPrompt
            });
        }

        InputTextBox.Text = string.Empty;
        AddUserTurn(userInput);
        ScrollToBottom();

        await RunConversationAsync(selectedModel);
    }

    private async Task RunConversationAsync(string selectedModel)
    {
        _isSending = true;
        SetControlsEnabled(true);
        ModelComboBox.IsEnabled = false;

        var assistantBubble = AddAssistantLoadingBubble();
        string assistantStatus = string.Empty;

        try
        {
            var messages = BuildMessagesForApi();
            var tools = new List<object>
            {
                LLMProvider.CreateWebSearchTool(),
                LLMProvider.CreateCurrentPageSearchTool()
            };

            while (true)
            {
                var result = await _provider!.GetChatCompletionAsync(messages, selectedModel, tools);
                var toolCalls = result?.ToolCalls ?? Array.Empty<LlmToolCall>();
                string responseContent = result?.Content ?? string.Empty;

                if (toolCalls.Count == 0)
                {
                    string finalText = string.IsNullOrWhiteSpace(responseContent) ? "(No response)" : responseContent;
                    UpdateBubble(assistantBubble, finalText, false, assistantStatus);
                    _conversation.Add(new ChatTurn
                    {
                        Role = "assistant",
                        Content = finalText
                    });
                    break;
                }

                _conversation.Add(new ChatTurn
                {
                    Role = "assistant",
                    Content = responseContent,
                    ToolCalls = toolCalls
                });

                foreach (var toolCall in toolCalls)
                {
                    string toolResult = await ExecuteToolAsync(toolCall);
                    assistantStatus = string.IsNullOrWhiteSpace(assistantStatus)
                        ? $"{toolCall.Name} tool used"
                        : $"{assistantStatus}\n{toolCall.Name} tool used";

                    UpdateBubble(
                        assistantBubble,
                        string.IsNullOrWhiteSpace(responseContent) ? assistantBubble.Body.Text : responseContent,
                        true,
                        assistantStatus);

                    _conversation.Add(new ChatTurn
                    {
                        Role = "tool",
                        ToolCallId = toolCall.Id,
                        ToolName = toolCall.Name,
                        Content = toolResult
                    });
                }

                messages = BuildMessagesForApi();
            }
        }
        catch (Exception ex)
        {
            UpdateBubble(assistantBubble, $"[Error: {ex.Message}]", false, assistantStatus);
        }
        finally
        {
            _isSending = false;
            SetControlsEnabled(true);
            InputTextBox.IsEnabled = _provider != null && !string.IsNullOrWhiteSpace(_selectedText) && ModelComboBox.SelectedItem != null;
            SendButton.IsEnabled = InputTextBox.IsEnabled;
            ExplainEnglishButton.IsEnabled = InputTextBox.IsEnabled;
            ExplainJapaneseButton.IsEnabled = InputTextBox.IsEnabled;
            TranslateButton.IsEnabled = InputTextBox.IsEnabled;
            ScrollToBottom();
        }
    }

    private IReadOnlyList<LlmChatMessage> BuildMessagesForApi()
    {
        return _conversation.Select(turn => new LlmChatMessage(turn.Role, turn.Content, turn.ToolCallId, turn.ToolCalls)).ToList();
    }

    private async Task<string> ExecuteToolAsync(LlmToolCall toolCall)
    {
        return toolCall.Name switch
        {
            "web_search" => await ExecuteWebSearchAsync(toolCall.ArgumentsJson),
            "current_page_search" => ExecuteCurrentPageSearch(toolCall.ArgumentsJson),
            _ => $"Unknown tool: {toolCall.Name}"
        };
    }

    private async Task<string> ExecuteWebSearchAsync(string argumentsJson)
    {
        string query = ExtractArgument(argumentsJson, "query");
        if (string.IsNullOrWhiteSpace(query))
        {
            return "Missing query.";
        }

        string url = $"https://html.duckduckgo.com/html/?q={Uri.EscapeDataString(query)}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await ToolHttpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        string html = await response.Content.ReadAsStringAsync();
        var matches = Regex.Matches(
            html,
            "<a[^>]*class=\"result__a\"[^>]*href=\"(?<url>[^\"]+)\"[^>]*>(?<title>.*?)</a>.*?<a[^>]*class=\"result__snippet\"[^>]*>(?<snippet>.*?)</a>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        var results = new List<string>();
        foreach (Match match in matches.Cast<Match>().Take(5))
        {
            string title = StripHtml(System.Net.WebUtility.HtmlDecode(match.Groups["title"].Value));
            string snippet = StripHtml(System.Net.WebUtility.HtmlDecode(match.Groups["snippet"].Value));
            string resultUrl = System.Net.WebUtility.HtmlDecode(match.Groups["url"].Value);
            results.Add($"- {title}\n  {snippet}\n  {resultUrl}");
        }

        if (results.Count == 0)
        {
            return $"No web results found for \"{query}\".";
        }

        return $"Web results for \"{query}\":\n{string.Join("\n", results)}";
    }

    private string ExecuteCurrentPageSearch(string argumentsJson)
    {
        string query = ExtractArgument(argumentsJson, "query");
        if (string.IsNullOrWhiteSpace(query))
        {
            return "Missing query.";
        }

        if (string.IsNullOrWhiteSpace(_currentPageText))
        {
            return "No current page text is available.";
        }

        var snippets = FindPageSnippets(query, _currentPageText, 5);
        if (snippets.Count == 0)
        {
            return $"No matches found on the current page for \"{query}\".";
        }

        var builder = new StringBuilder();
        builder.AppendLine($"Current page matches for \"{query}\":");
        for (int i = 0; i < snippets.Count; i++)
        {
            builder.AppendLine($"{i + 1}. {snippets[i]}");
        }

        return builder.ToString().TrimEnd();
    }

    private static List<string> FindPageSnippets(string query, string pageText, int maxResults)
    {
        var results = new List<string>();
        string normalizedQuery = query.Trim();
        if (normalizedQuery.Length == 0)
        {
            return results;
        }

        int firstIndex = pageText.IndexOf(normalizedQuery, StringComparison.OrdinalIgnoreCase);
        if (firstIndex >= 0)
        {
            results.Add(CreateSnippet(pageText, firstIndex, normalizedQuery.Length));
        }

        foreach (var term in normalizedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (results.Count >= maxResults)
            {
                break;
            }

            int termIndex = pageText.IndexOf(term, StringComparison.OrdinalIgnoreCase);
            if (termIndex >= 0)
            {
                string snippet = CreateSnippet(pageText, termIndex, term.Length);
                if (!results.Contains(snippet))
                {
                    results.Add(snippet);
                }
            }
        }

        if (results.Count == 0)
        {
            var lines = pageText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var line in lines)
            {
                if (line.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(Truncate(line, 220));
                    if (results.Count >= maxResults)
                    {
                        break;
                    }
                }
            }
        }

        return results.Distinct().Take(maxResults).ToList();
    }

    private static string CreateSnippet(string text, int index, int length)
    {
        int start = Math.Max(0, index - 80);
        int end = Math.Min(text.Length, index + Math.Max(length, 1) + 80);
        string snippet = text[start..end].Replace("\r", " ").Replace("\n", " ").Trim();
        if (start > 0)
        {
            snippet = "..." + snippet;
        }

        if (end < text.Length)
        {
            snippet += "...";
        }

        return snippet;
    }

    private static string StripHtml(string text)
    {
        return Regex.Replace(text, "<.*?>", string.Empty).Trim();
    }

    private static string ExtractArgument(string argumentsJson, string key)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson))
        {
            return string.Empty;
        }

        try
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            if (doc.RootElement.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString() ?? string.Empty;
            }
        }
        catch
        {
        }

        return string.Empty;
    }

    private async Task CopyTextAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        DataPackage data = new();
        data.SetText(text);
        Clipboard.SetContent(data);
        await Task.CompletedTask;
    }

    private async Task EditAndResendAsync(int historyIndex)
    {
        if (_isSending || historyIndex < 0 || historyIndex >= _conversation.Count || _conversation[historyIndex].Role != "user")
        {
            return;
        }

        string? editedText = await ShowEditDialogAsync(_conversation[historyIndex].Content);
        if (editedText == null)
        {
            return;
        }

        _conversation[historyIndex].Content = editedText;
        TrimConversationAfter(historyIndex);
        RenderConversationHistory();

        if (ModelComboBox.SelectedItem == null)
        {
            return;
        }

        string selectedModel = ModelComboBox.SelectedItem.ToString() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(selectedModel))
        {
            await RunConversationAsync(selectedModel);
        }
    }

    private async Task RegenerateFromAsync(int historyIndex)
    {
        if (_isSending || historyIndex < 0 || historyIndex >= _conversation.Count || _conversation[historyIndex].Role != "user")
        {
            return;
        }

        string text = _conversation[historyIndex].Content;
        TrimConversationAfter(historyIndex);
        RenderConversationHistory();

        if (ModelComboBox.SelectedItem == null)
        {
            return;
        }

        string selectedModel = ModelComboBox.SelectedItem.ToString() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(selectedModel))
        {
            await RunConversationAsync(selectedModel);
        }
    }

    private void TrimConversationAfter(int historyIndex)
    {
        if (historyIndex < 0 || historyIndex >= _conversation.Count)
        {
            return;
        }

        _conversation.RemoveRange(historyIndex + 1, _conversation.Count - historyIndex - 1);
    }

    private async Task<string?> ShowEditDialogAsync(string initialText)
    {
        var editor = new TextBox
        {
            Text = initialText,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinWidth = 420,
            MinHeight = 160
        };

        var dialog = new ContentDialog
        {
            Title = "Edit prompt",
            Content = editor,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary ? editor.Text.Trim() : null;
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        MessagesPanel.Children.Clear();
        _conversation.Clear();
        _systemPrompt = BuildSystemPrompt();
        InputTextBox.Text = string.Empty;
        PrepareInitialPrompt();
    }

    private async void SendButton_Click(object sender, RoutedEventArgs e)
    {
        await SendFollowUpAsync();
    }

    private async void ExplainEnglishButton_Click(object sender, RoutedEventArgs e)
    {
        await SendFollowUpAsync($"Explain the selected Japanese text in English. Give readings and meanings for words at or above the {_jlptLevel} level.");
    }

    private async void ExplainJapaneseButton_Click(object sender, RoutedEventArgs e)
    {
        await SendFollowUpAsync($"Explain the selected Japanese text in {_jlptLevel} Japanese. Give readings and meanings for words at or above the {_jlptLevel} level.");
    }

    private async void TranslateButton_Click(object sender, RoutedEventArgs e)
    {
        await SendFollowUpAsync("Translate the selected text to English.");
    }

    private async void InputTextBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && !string.IsNullOrWhiteSpace(InputTextBox.Text))
        {
            e.Handled = true;
            await SendFollowUpAsync();
        }
    }

    private bool HasAiResponse()
    {
        return _conversation.Any(turn => turn.Role == "assistant" && !string.IsNullOrWhiteSpace(turn.Content));
    }

    private void ScrollToBottom()
    {
        var dq = DispatcherQueue;
        if (dq != null)
        {
            _ = dq.TryEnqueue(() =>
            {
                ChatScrollViewer.ChangeView(null, ChatScrollViewer.ScrollableHeight, null);
            });
        }
    }
}



