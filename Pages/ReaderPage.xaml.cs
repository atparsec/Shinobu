using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Web.WebView2.Core;
using Shinobu.Dialogs.Common;
using Shinobu.Dialogs.Reader;
using Shinobu.Helpers.Books;
using Shinobu.Helpers.Content;
using Shinobu.Helpers.Dictionary;
using Shinobu.Helpers.Reader;
using Shinobu.Helpers.Services;
using Shinobu.Helpers;
using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Popups;

namespace Shinobu.Pages
{
    public sealed partial class ReaderPage : Page, INotifyPropertyChanged
    {
        private string _bookPath = string.Empty;
        private int _currentLogicalPage = 0; // 0-based for UI display
        private int _totalLogicalPages = 1;

        private bool _isDialogShowing = false;
        public event PropertyChangedEventHandler? PropertyChanged;

        private ReaderController? _controller;
        private FuriganaGenerator _furiganaGenerator = new();
        private JlptLevel _userJlptLevel;
        private bool _isVerticalText;
        private double _fontSize;
        private double _lineHeight;
        private FontFamily _readerFont;
        private double _pageMargin;
        private string _theme = "System";
        private readonly ApplicationDataContainer _settings = ApplicationData.Current.LocalSettings;
        private ReaderThemeManager _themeManager = new();
        private string _currentThemeName;

        public bool CanGoPrev => _currentLogicalPage > 0;
        public bool CanGoNext => _currentLogicalPage < _totalLogicalPages - 1;
        public string PageText => $"{_currentLogicalPage + 1} / {_totalLogicalPages}";
        public List<JlptLevel> JlptLevels { get; } = [.. Enum.GetValues<JlptLevel>()];

        public bool IsVerticalText
        {
            get => _isVerticalText;
            set
            {
                if (_isVerticalText != value)
                {
                    _isVerticalText = value;
                    _settings.Values["IsVerticalText"] = value;
                    _ = RefreshCurrentPageAsync();
                    OnPropertyChanged(nameof(IsVerticalText));
                }
            }
        }

        public double ReaderFontSize
        {
            get => _fontSize;
            set
            {
                if (_fontSize != value)
                {
                    _fontSize = value;
                    _settings.Values["FontSize"] = value;
                    _ = RefreshCurrentPageAsync();
                    OnPropertyChanged(nameof(ReaderFontSize));
                }
            }
        }

        public double LineHeight
        {
            get => _lineHeight;
            set
            {
                if (_lineHeight != value)
                {
                    _lineHeight = value;
                    _settings.Values["LineHeight"] = value;
                    _ = RefreshCurrentPageAsync();
                    OnPropertyChanged(nameof(LineHeight));
                }
            }
        }

        public FontFamily ReaderFont
        {
            get => _readerFont;
            set
            {
                if (_readerFont != value)
                {
                    _readerFont = value;
                    _settings.Values["FontFamily"] = value.Source;
                    _ = RefreshCurrentPageAsync();
                    OnPropertyChanged(nameof(ReaderFont));
                }
            }
        }

        public double ReaderMargin
        {
            get => _pageMargin;
            set
            {
                if (_pageMargin != value)
                {
                    _pageMargin = value;
                    _settings.Values["PageMargin"] = value;
                    _ = RefreshCurrentPageAsync();
                    OnPropertyChanged(nameof(ReaderMargin));
                }
            }
        }

        public JlptLevel UserJlptLevel
        {
            get => _userJlptLevel;
            set
            {
                if (_userJlptLevel != value)
                {
                    _userJlptLevel = value;
                    _settings.Values["JlptLevel"] = (int)value;
                    _ = RefreshCurrentPageAsync();
                    OnPropertyChanged(nameof(UserJlptLevel));
                }
            }
        }

        public string ReaderThemeName
        {
            get => _currentThemeName;
            set
            {
                if (_currentThemeName != value)
                {
                    _currentThemeName = value;
                    _settings.Values["ReaderTheme"] = value;
                    _ = RefreshCurrentPageAsync();
                    OnPropertyChanged(nameof(ReaderThemeName));
                }
            }
        }

        private BookTheme CurrentTheme
        {
            get
            {
                if (_currentThemeName == "Default")
                {
                    bool isDark = _theme == "Dark" || (_theme == "System" && Application.Current.RequestedTheme == ApplicationTheme.Dark);
                    return new BookTheme { Name = "Default", Background = isDark ? "#000" : "#FFF", Foreground = isDark ? "#FFF" : "#000" };
                }
                else
                {
                    return _themeManager.GetTheme(_currentThemeName) ?? _themeManager.GetTheme("Default") ?? _themeManager.Themes.FirstOrDefault() ?? new BookTheme();
                }
            }
        }

        public ReaderPage()
        {
            InitializeComponent();
            _userJlptLevel = _settings.Values.TryGetValue("JlptLevel", out object? levelObj) && levelObj is int levelInt ? (JlptLevel)levelInt : JlptLevel.N5;
            _isVerticalText = _settings.Values.TryGetValue("IsVerticalText", out object? vt) && vt is bool b && b;
            _fontSize = _settings.Values.TryGetValue("FontSize", out object? fs) && fs is double fsd ? fsd : 16.0;
            _lineHeight = _settings.Values.TryGetValue("LineHeight", out object? lh) && lh is double lhd ? lhd : 3.0;
            _readerFont = _settings.Values.TryGetValue("FontFamily", out object? ff) && ff is string ffs ? new FontFamily(ffs) : new FontFamily("Segoe UI");
            _pageMargin = _settings.Values.TryGetValue("PageMargin", out object? pm) && pm is double pmd ? pmd : 30.0;
            _theme = _settings.Values.TryGetValue("Theme", out object? t) && t is string themeStr ? themeStr : "Dark";
            ElementTheme requestedTheme = _theme switch
            {
                "Light" => ElementTheme.Light,
                "Dark" => ElementTheme.Dark,
                _ => ElementTheme.Default
            };
            _currentThemeName = _settings.Values.TryGetValue("ReaderTheme", out object? rt) && rt is string rts ? rts : "Default";
            ReaderWebView.WebMessageReceived += OnWebMessageReceived;
            ReaderWebView.NavigationCompleted += ReaderWebView_NavigationCompleted;
        }

        private async void ReaderWebView_NavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            await Task.CompletedTask;
        }

        private async void ReaderWebView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_controller != null && ReaderWebView.CoreWebView2 != null)
            {
                await RenderCurrentPageAsync();
            }
        }
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await _themeManager.LoadAsync();

            ConnectedAnimation anim = ConnectedAnimationService.GetForCurrentView().GetAnimation("ForwardConnectedAnimation");
            _ = anim?.TryStart(ReaderWebView);

            await ReaderWebView.EnsureCoreWebView2Async();
            ReaderWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            ReaderWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            ReaderWebView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;

            ReaderWebView.SizeChanged -= ReaderWebView_SizeChanged;
            ReaderWebView.SizeChanged += ReaderWebView_SizeChanged;

            LoadingRing.Visibility = Visibility.Visible;

            try
            {
                if (e.Parameter is string param && !string.IsNullOrWhiteSpace(param))
                {
                    string[] parts = param.Split(';');
                    _bookPath = parts[0];

                    await LoadDocumentAsync();

                    LoadingRing.Visibility = Visibility.Collapsed;

                    if (parts.Length > 1 && int.TryParse(parts[1], out int pageNum) && pageNum > 0)
                    {
                        for (int i = 0; i < pageNum - 1 && CanGoNext; i++)
                        {
                            await TryGoNextAsync();
                        }
                    }

                    if (parts.Length > 4 &&
                        int.TryParse(parts[2], out int offset) &&
                        int.TryParse(parts[3], out int endOffset) &&
                        int.TryParse(parts[4], out int pageNo))
                    {
                        await SelectTextAtOffsetAsync(offset, Math.Max(0, endOffset - offset));
                    }
                }
                else
                {
                    (string? sessionHash, int sessionPage) = ReaderSessionManager.GetSession();
                    if (!string.IsNullOrEmpty(sessionHash) && File.Exists(sessionHash))
                    {
                        _bookPath = sessionHash;
                        await LoadDocumentAsync();

                        for (int i = 0; i < sessionPage && CanGoNext; i++)
                        {
                            await TryGoNextAsync();
                        }
                    }
                    else if (!string.IsNullOrEmpty(_bookPath) && File.Exists(_bookPath))
                    {
                        await LoadDocumentAsync();
                    }

                    LoadingRing.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                LoadingRing.Visibility = Visibility.Collapsed;
                if (ReaderWebView.CoreWebView2 != null)
                {
                    ReaderWebView.CoreWebView2.NavigateToString($"<html><body style='color:red;padding:20px;font-family:sans-serif;'>{System.Net.WebUtility.HtmlEncode(ex.Message)}</body></html>");
                }
            }

            if (App.MainWindowInstance is MainWindow mainWindow)
            {
                mainWindow.SelectNavigation("reader");
            }
            ReaderWebView.Focus(FocusState.Programmatic);
        }

        private async Task SelectTextAtOffsetAsync(int offset, int length)
        {
            // Best-effort selection within the current per-page HTML.
            // Offsets are relative to the current page's text content only.
            await ReaderWebView.ExecuteScriptAsync($@"
                try {{
                    var range = document.createRange();
                    var selection = window.getSelection();
                    function getTextNodeAtOffset(root, targetOffset) {{
                        var walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, null, false);
                        var currentNode;
                        var currentOffset = 0;
                        while (currentNode = walker.nextNode()) {{
                            var nodeLength = currentNode.textContent.length;
                            if (currentOffset + nodeLength >= targetOffset) {{
                                return {{ node: currentNode, offset: targetOffset - currentOffset }};
                            }}
                            currentOffset += nodeLength;
                        }}
                        return null;
                    }}
                    var startInfo = getTextNodeAtOffset(document.body, {offset});
                    var endInfo = getTextNodeAtOffset(document.body, {offset + Math.Max(0, length)});
                    if (startInfo && endInfo) {{
                        range.setStart(startInfo.node, startInfo.offset);
                        range.setEnd(endInfo.node, endInfo.offset);
                        selection.removeAllRanges();
                        selection.addRange(range);
                    }}
                }} catch(e) {{ /* ignore */ }}
            ");
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            if (!string.IsNullOrEmpty(_bookPath))
            {
                ReaderSessionManager.SaveSession(_bookPath, _currentLogicalPage);
            }
            base.OnNavigatingFrom(e);
            ReaderWebView.NavigationCompleted -= ReaderWebView_NavigationCompleted;
            ReaderWebView.WebMessageReceived -= OnWebMessageReceived;

            if (_controller != null)
            {
                _ = _controller.DisposeAsync().AsTask();
                _controller = null;
            }
        }

        private async Task LoadDocumentAsync()
        {
            if (string.IsNullOrEmpty(_bookPath))
                return;

            string filePath = _bookPath;
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("The selected book file could not be found.", filePath);
            }

            string ext = Path.GetExtension(filePath).ToLowerInvariant();

            IBookDocument document = ext switch
            {
                ".txt" => new TxtDocument(filePath),
                ".pdf" => new PdfDocument(filePath),
                _ => throw new NotSupportedException($"Unsupported file type: {ext}")
            };

            // Dispose previous controller if any
            if (_controller != null)
            {
                await _controller.DisposeAsync();
            }

            _controller = new ReaderController(document, _furiganaGenerator);

            BookLocation? startLoc = null;

            await _controller.InitializeAsync(startLoc);

            _totalLogicalPages = _controller.TotalPages ?? 1;
            _currentLogicalPage = 0;
            OnPropertyChanged(nameof(CanGoPrev));
            OnPropertyChanged(nameof(CanGoNext));
            OnPropertyChanged(nameof(PageText));

            await RenderCurrentPageAsync();
        }

        private async Task RenderCurrentPageAsync()
        {
            if (_controller == null || ReaderWebView.CoreWebView2 == null)
                return;

            BookTheme theme = CurrentTheme;

            double w = ReaderWebView.ActualWidth > 10 ? ReaderWebView.ActualWidth : 900;
            double h = ReaderWebView.ActualHeight > 10 ? ReaderWebView.ActualHeight : 700;

            string html = await _controller.GetCurrentPageHtmlAsync(
                _isVerticalText,
                _fontSize,
                _lineHeight,
                _readerFont.Source,
                theme,
                _pageMargin,
                w,
                h,
                _userJlptLevel);

            try
            {
                ReaderWebView.CoreWebView2.NavigateToString(html);
            }
            catch (ArgumentException ex)
            {
                Debug.WriteLine($"NavigateToString failed, retrying without images: {ex.Message}");
                string noImages = RemoveImageTags(html);
                try
                {
                    ReaderWebView.CoreWebView2.NavigateToString(noImages);
                }
                catch (Exception ex2)
                {
                    Debug.WriteLine("NavigateToString fallback also failed: " + ex2.Message);
                    throw;
                }
            }

            if (_controller != null)
            {
                var loc = _controller.CurrentLocation;
                if (loc.ChapterIndex > 0 || _totalLogicalPages <= 1)
                {
                    _currentLogicalPage = loc.ChapterIndex;
                }
            }

            OnPropertyChanged(nameof(CanGoPrev));
            OnPropertyChanged(nameof(CanGoNext));
            OnPropertyChanged(nameof(PageText));
        }

        private async Task RefreshCurrentPageAsync()
        {
            if (_controller == null) return;
            await RenderCurrentPageAsync();
        }

        private void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            _ = TryGoPreviousAsync();
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            _ = TryGoNextAsync();
        }

        private async Task<bool> TryGoNextAsync()
        {
            if (_controller == null || !CanGoNext) return false;

            BookTheme theme = CurrentTheme;
            string? html = await _controller.GoNextAsync(
                _isVerticalText, _fontSize, _lineHeight, _readerFont.Source,
                theme, _pageMargin, ReaderWebView.ActualWidth, ReaderWebView.ActualHeight, _userJlptLevel);

            if (html != null && ReaderWebView.CoreWebView2 != null)
            {
                try
                {
                    ReaderWebView.CoreWebView2.NavigateToString(html);
                }
                catch (ArgumentException ex)
                {
                    Debug.WriteLine($"NavigateToString failed on next, stripping images: {ex.Message}");
                    string noImages = RemoveImageTags(html);
                    ReaderWebView.CoreWebView2.NavigateToString(noImages);
                }
                if (_controller != null)
                {
                    var loc = _controller.CurrentLocation;
                    if (_totalLogicalPages > 1 && loc.ChapterIndex >= 0)
                    {
                        _currentLogicalPage = loc.ChapterIndex;
                    }
                    else
                    {
                        _currentLogicalPage++;
                    }
                }
                else
                {
                    _currentLogicalPage++;
                }
                _currentLogicalPage = Math.Max(0, Math.Min(_currentLogicalPage, _totalLogicalPages - 1));
                OnPropertyChanged(nameof(CanGoPrev));
                OnPropertyChanged(nameof(CanGoNext));
                OnPropertyChanged(nameof(PageText));
                ReaderWebView.Focus(FocusState.Programmatic);
                return true;
            }
            return false;
        }

        private async Task<bool> TryGoPreviousAsync()
        {
            if (_controller == null || !CanGoPrev) return false;

            BookTheme theme = CurrentTheme;
            string? html = await _controller.GoPreviousAsync(
                _isVerticalText, _fontSize, _lineHeight, _readerFont.Source,
                theme, _pageMargin, ReaderWebView.ActualWidth, ReaderWebView.ActualHeight, _userJlptLevel);

            if (html != null && ReaderWebView.CoreWebView2 != null)
            {
                try
                {
                    ReaderWebView.CoreWebView2.NavigateToString(html);
                }
                catch (ArgumentException ex)
                {
                    Debug.WriteLine($"NavigateToString failed on previous, stripping images: {ex.Message}");
                    string noImages = RemoveImageTags(html);
                    ReaderWebView.CoreWebView2.NavigateToString(noImages);
                }
                if (_controller != null)
                {
                    var loc = _controller.CurrentLocation;
                    if (_totalLogicalPages > 1 && loc.ChapterIndex >= 0)
                    {
                        _currentLogicalPage = loc.ChapterIndex;
                    }
                    else
                    {
                        _currentLogicalPage = Math.Max(0, _currentLogicalPage - 1);
                    }
                }
                else
                {
                    _currentLogicalPage = Math.Max(0, _currentLogicalPage - 1);
                }
                _currentLogicalPage = Math.Max(0, Math.Min(_currentLogicalPage, _totalLogicalPages - 1));
                OnPropertyChanged(nameof(CanGoPrev));
                OnPropertyChanged(nameof(CanGoNext));
                OnPropertyChanged(nameof(PageText));
                ReaderWebView.Focus(FocusState.Programmatic);
                return true;
            }
            return false;
        }

        private async void OnWebMessageReceived(WebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            string msg = args.TryGetWebMessageAsString();

            if (msg.StartsWith("selected:"))
            {
                string[] parts = msg["selected:".Length..].Split(':', 2);
                if (parts.Length == 2 && int.TryParse(parts[0], out int start))
                {
                    await ShowSelectedTextPopup(parts[1], start);
                }
            }
            else if (msg.StartsWith("nav:"))
            {
                string direction = msg["nav:".Length..].Trim();
                if (direction == "next" && CanGoNext)
                {
                    _ = TryGoNextAsync();
                }
                else if (direction == "prev" && CanGoPrev)
                {
                    _ = TryGoPreviousAsync();
                }
            }
            else if (msg.StartsWith("image:"))
            {
                // Images are now rendered via data URLs in HtmlRenderer.
                // For full-screen view we would need to re-extract the specific image bytes.
                // As a minimal bridge, we currently ignore deep image navigation or could implement temp extraction here.
                // For now do nothing to avoid using removed BookManager APIs.
                await Task.CompletedTask;
            }
            else if (msg == "page-ready")
            {
                // Per-page HTML finished loading. Could be used for progress.
            }
        }

        private async Task ShowSelectedTextPopup(string text, int start)
        {
            if (_isDialogShowing)
            {
                return;
            }

            _isDialogShowing = true;

            // Resolve to the actual source file path for bookmarks / future features.
            string resolvedPath = _bookPath;

            string currentPageText = await GetCurrentPageTextAsync();

            SelectionDialog dialog = new(start, text.Length, text, _currentLogicalPage, resolvedPath, currentPageText);
            Grid overlay = new()
            {
                Background = new SolidColorBrush(Microsoft.UI.Colors.Black) { Opacity = 0.5 },
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            void closeDialog()
            {
                (Content as Panel)?.Children.Remove(overlay);
                (Content as Panel)?.Children.Remove(dialog);
                _isDialogShowing = false;
                _ = ReaderWebView.ExecuteScriptAsync("window.getSelection().removeAllRanges();");
            }
            dialog.CloseAction = closeDialog;
            overlay.PointerPressed += (s, e) => closeDialog();
            (Content as Panel)?.Children.Add(overlay);
            (Content as Panel)?.Children.Add(dialog);
            dialog.HorizontalAlignment = HorizontalAlignment.Center;
            dialog.VerticalAlignment = VerticalAlignment.Center;
        }

        private async Task<string> GetCurrentPageTextAsync()
        {
            if (ReaderWebView.CoreWebView2 == null)
            {
                return string.Empty;
            }

            try
            {
                string scriptResult = await ReaderWebView.ExecuteScriptAsync("document.body?.innerText ?? ''");
                return JsonSerializer.Deserialize<string>(scriptResult) ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string RemoveImageTags(string html)
        {
            if (string.IsNullOrEmpty(html)) return html;
            try
            {
                // Remove <img ...> tags (simple regex). Keeps other markup intact.
                return Regex.Replace(html, "<img[^>]*>", string.Empty, RegexOptions.IgnoreCase);
            }
            catch
            {
                return html;
            }
        }

        private async void PageOptionsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isDialogShowing)
            {
                return;
            }
            _isDialogShowing = true;
            var pageOptionsDialog = new PageOptionsDialog(this);
            Grid overlay = new()
            {
                Background = new SolidColorBrush(Microsoft.UI.Colors.Black) { Opacity = 0.5 },
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            void closeDialog()
            {
                (Content as Panel)?.Children.Remove(overlay);
                (Content as Panel)?.Children.Remove(pageOptionsDialog);
                _isDialogShowing = false;
                _ = ReaderWebView.ExecuteScriptAsync("window.getSelection().removeAllRanges();");
            }
            overlay.PointerPressed += (s, e) => closeDialog();
            (Content as Panel)?.Children.Add(overlay);
            (Content as Panel)?.Children.Add(pageOptionsDialog);
            pageOptionsDialog.HorizontalAlignment = HorizontalAlignment.Center;
            pageOptionsDialog.VerticalAlignment = VerticalAlignment.Center;

            pageOptionsDialog.CustomThemeRequested += async (s, e) =>
            {
                ContentDialog customDialog = new()
                {
                    Title = "Custom Theme",
                    PrimaryButtonText = "OK",
                    CloseButtonText = "Cancel",
                    XamlRoot = XamlRoot,
                    RequestedTheme = RequestedTheme
                };
                ColorSelectDialog themeCreator = new()
                {
                    ColorSelectText = "Select Background Color"
                };
                customDialog.Content = themeCreator;
                var bgresult = await customDialog.ShowAsync();
                string bg = string.Empty;
                string fg = string.Empty;
                if (bgresult == ContentDialogResult.Primary)
                {
                    bg = $"#{themeCreator.SelectedColor.R:X2}{themeCreator.SelectedColor.G:X2}{themeCreator.SelectedColor.B:X2}";

                    themeCreator.ColorSelectText = "Select Foreground Color";
                    var fgresult = await customDialog.ShowAsync();
                    if (fgresult == ContentDialogResult.Primary)
                    {
                        fg = $"#{themeCreator.SelectedColor.R:X2}{themeCreator.SelectedColor.G:X2}{themeCreator.SelectedColor.B:X2}";
                    }
                }

                if (!string.IsNullOrEmpty(bg) && !string.IsNullOrEmpty(fg))
                {
                    string themeName = "Custom";
                    int suffix = 1;
                    while (_themeManager.GetTheme(themeName) != null)
                    {
                        themeName = $"Custom {suffix}";
                        suffix++;
                    }
                    BookTheme custom = new() { Name = themeName, Background = bg, Foreground = fg };
                    _themeManager.AddOrUpdateTheme(custom);
                    await _themeManager.SaveAsync();

                    ReaderThemeName = themeName;
                    await pageOptionsDialog.ThemeManager.LoadAsync();
                    pageOptionsDialog.InitThemes();
                }

            };
        }

        private void OnPropertyChanged(string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private static async Task<string> ComputeFileHash(string filePath)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            byte[] hash = await sha.ComputeHashAsync(stream);
            return Convert.ToBase64String(hash).Replace("/", "_").Replace("+", "-");
        }

        private static string ComputeSettingsHash(string settings)
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(settings));
            return Convert.ToBase64String(hash).Replace("/", "_").Replace("+", "-");
        }
    }
}

