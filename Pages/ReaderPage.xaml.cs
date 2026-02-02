using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Web.WebView2.Core;
using Shinobu.Dialogs;
using Shinobu.Helpers;
using System;
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
        private string _bookHash = string.Empty;
        private List<int> _pages = [];

        private int _currentPage = 0;
        private bool _isDialogShowing = false;
        public event PropertyChangedEventHandler? PropertyChanged;
        private FuriganaGenerator _furiganaGenerator = new();
        private JlptLevel _userJlptLevel;
        private bool _isVerticalText;
        private double _fontSize;
        private double _lineHeight;
        private FontFamily _readerFont;
        private double _pageMargin;
        private string _theme = "System";
        private readonly ApplicationDataContainer _settings = ApplicationData.Current.LocalSettings;
        private BookContent _bookContent = new();
        private ReaderThemeManager _themeManager = new();
        private string _currentThemeName;

        private TaskCompletionSource? _pagesLoaded;
        public bool CanGoPrev => _currentPage > 0;
        public bool CanGoNext => _currentPage < _pages.Count - 1;
        public string PageText => $"{_currentPage + 1} / {_pages.Count}";
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
                    if (!string.IsNullOrEmpty(_bookHash))
                    {
                        _ = RenderBook(_bookHash);
                    }
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
                    if (!string.IsNullOrEmpty(_bookHash))
                    {
                        _ = RenderBook(_bookHash);
                    }
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
                    if (!string.IsNullOrEmpty(_bookHash))
                    {
                        _ = RenderBook(_bookHash);
                    }
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
                    if (!string.IsNullOrEmpty(_bookHash))
                    {
                        _ = RenderBook(_bookHash);
                    }
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
                    if (!string.IsNullOrEmpty(_bookHash))
                    {
                        _ = RenderBook(_bookHash);
                    }
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
                    if (!string.IsNullOrEmpty(_bookHash))
                    {
                        _ = RenderBook(_bookHash);
                    }
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
                    if (!string.IsNullOrEmpty(_bookHash))
                    {
                        _ = RenderBook(_bookHash);
                    }
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
            await sender.ExecuteScriptAsync($@"
                if (!window.__selectionListenerAttached) {{
                    document.addEventListener('mouseup', function () {{
                        var selection = window.getSelection();

                        if (selection.rangeCount) {{
                            var range = selection.getRangeAt(0);
                            var fragment = range.cloneContents();

                            fragment.querySelectorAll('.fg').forEach(el => el.remove());

                            var selectedText = fragment.textContent;
                            if (selectedText.trim()) {{
                                var bodyText = document.body.textContent || document.body.innerText;
                                var startOffset = getTextOffset(document.body, range.startContainer, range.startOffset);
                                window.chrome.webview.postMessage('selected:' + startOffset + ':' + selectedText);
                            }}
                        }}
                    }});
                    document.addEventListener('keydown', function (event) {{
                        if (event.key === 'ArrowRight' || event.key === 'ArrowDown') {{
                            window.chrome.webview.postMessage('nav: next');
                            event.preventDefault();
                        }} else if (event.key === 'ArrowLeft' || event.key === 'ArrowUp') {{
                            window.chrome.webview.postMessage('nav: prev');
                            event.preventDefault();
                        }}
                    }});
                    document.addEventListener('wheel', function (event) {{
                        if (event.deltaY > 0) {{
                            window.chrome.webview.postMessage('nav: next');
                        }} else if (event.deltaY < 0) {{
                            window.chrome.webview.postMessage('nav: prev');
                        }}
                    }});
                    function getTextOffset(root, node, offset) {{
                        var text = '';
                        var walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, null, false);
                        var currentNode;
                        while (currentNode = walker.nextNode()) {{
                            if (currentNode === node) {{
                                return text.length + offset;
                            }}
                            text += currentNode.textContent;
                        }}
                        return text.length;
                    }}
                    window.__selectionListenerAttached = true;
                }}
            ");
        }
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await _themeManager.LoadAsync();

            ConnectedAnimation anim = ConnectedAnimationService.GetForCurrentView().GetAnimation("ForwardConnectedAnimation");

            _ = anim?.TryStart(ReaderWebView);

            await ReaderWebView.EnsureCoreWebView2Async();
            ReaderWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            ReaderWebView.CoreWebView2.Settings.AreDevToolsEnabled = true;

            LoadingRing.Visibility = Visibility.Visible;

            if (e.Parameter is string param)
            {
                string[] parts = param.Split(';');
                _bookHash = parts[0];
                await LoadBook();
                await _pagesLoaded!.Task;

                LoadingRing.Visibility = Visibility.Collapsed;

                if (parts.Length > 1 && int.TryParse(parts[1], out int pageNum))
                {
                    await GoToPage(Math.Min(pageNum, _pages.Count - 1));
                }
                if (parts.Length > 4 && int.TryParse(parts[2], out int offset) && int.TryParse(parts[3], out int endOffset) && int.TryParse(parts[4], out int pageNo))
                {
                    await GoToPage(Math.Min(pageNo, _pages.Count - 1));
                    await SelectTextAtOffsetAsync(offset, endOffset);
                }
            }
            else
            {
                (string? sessionHash, int sessionPage) = ReaderSessionManager.GetSession();
                if (sessionHash != null && BookManager.GetBookByHash(sessionHash) != null)
                {
                    _bookHash = sessionHash;
                    await LoadBook();
                    await GoToPage(sessionPage);
                }

                LoadingRing.Visibility = Visibility.Collapsed;
            }

            if (App.MainWindowInstance is MainWindow mainWindow)
            {
                mainWindow.SelectNavigation("reader");
            }
            ReaderWebView.Focus(FocusState.Programmatic);
        }

        private async Task SelectTextAtOffsetAsync(int offset, int length)
        {
            await ReaderWebView.ExecuteScriptAsync($@"
                var range = document.createRange();
                var selection = window.getSelection();
                function getTextNodeAtOffset(root, offset) {{
                    var walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, null, false);
                    var currentNode;
                    var currentOffset = 0;
                    while (currentNode = walker.nextNode()) {{
                        var nodeLength = currentNode.textContent.length;
                        if (currentOffset + nodeLength >= offset) {{
                            return {{ node: currentNode, offset: offset - currentOffset }};
                        }}
                        currentOffset += nodeLength;
                    }}
                    return null;
                }}
                var startInfo = getTextNodeAtOffset(document.body, {offset});
                var endInfo = getTextNodeAtOffset(document.body, {offset + length});
                if (startInfo && endInfo) {{
                    range.setStart(startInfo.node, startInfo.offset);
                    range.setEnd(endInfo.node, endInfo.offset);
                    selection.removeAllRanges();
                    selection.addRange(range);
                }}");
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            if (!string.IsNullOrEmpty(_bookHash))
            {
                ReaderSessionManager.SaveSession(_bookHash, _currentPage);
            }
            base.OnNavigatingFrom(e);
            ReaderWebView.NavigationCompleted -= ReaderWebView_NavigationCompleted;
            ReaderWebView.WebMessageReceived -= OnWebMessageReceived;
        }

        private async Task LoadBook()
        {
            _bookContent = await BookManager.LoadBookContentAsync(_bookHash);

            _pagesLoaded = new TaskCompletionSource();
            await RenderBook(_bookHash);
        }

        private async Task RenderBook(string bookHash)
        {
            string htmlSettings = $"{_userJlptLevel}";
            string htmlHash = ComputeSettingsHash(htmlSettings);
            string bookDir = BookManager.GetBookDirectory(bookHash);
            string htmlFile = Path.Combine(bookDir, $"{bookHash}_{htmlHash}.html");
            string cssFile = Path.Combine(Path.GetTempPath(), "shinobu", "shinobu_styles.css");

            BookTheme currentTheme = CurrentTheme;
            string cssContent = BookManager.BuildCss(_fontSize, _lineHeight, _readerFont.Source, currentTheme, _isVerticalText, _pageMargin, ReaderWebView.ActualWidth, ReaderWebView.ActualHeight);
            Directory.CreateDirectory(Path.GetDirectoryName(cssFile)!);
            await File.WriteAllTextAsync(cssFile, cssContent);

            if (!File.Exists(htmlFile))
            {
                string htmlContent = await BookManager.BuildHtml(_bookContent, _furiganaGenerator, _userJlptLevel, _isVerticalText, _pageMargin);
                await File.WriteAllTextAsync(htmlFile, htmlContent);
            }

            Uri htmlUri = new Uri(htmlFile);
            if (ReaderWebView.Source == htmlUri)
            {
                ReaderWebView.CoreWebView2.Reload();
            }
            else
            {
                ReaderWebView.CoreWebView2.Navigate(htmlFile);
            }
        }

        private void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            if (CanGoPrev)
            {
                _ = GoToPage(_currentPage - 1);
            }
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (CanGoNext)
            {
                _ = GoToPage(_currentPage + 1);
            }
        }

        private async Task GoToPage(int page)
        {
            _currentPage = page;
            OnPropertyChanged();
            await ReaderWebView.ExecuteScriptAsync($"goToPage({page});");
            ReaderWebView.Focus(FocusState.Programmatic);
        }

        private async void OnWebMessageReceived(WebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            string msg = args.TryGetWebMessageAsString();

            if (msg.StartsWith("pages:"))
            {
                string lengthsJson = msg[6..];
                var lengths = JsonSerializer.Deserialize<List<int>>(lengthsJson);
                while (lengths != null && lengths.Count > 0 && lengths[^1] == 0)
                {
                    lengths.RemoveAt(lengths.Count - 1);
                }
                _pages = lengths ?? [];
                _currentPage = Math.Min(_currentPage, Math.Max(_pages.Count - 1, 0));
                OnPropertyChanged();
                _pagesLoaded?.TrySetResult();
                await GoToPage(_currentPage);
            }
            else if (msg.StartsWith("selected:"))
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
                    await GoToPage(_currentPage + 1);
                }
                else if (direction == "prev" && CanGoPrev)
                {
                    await GoToPage(_currentPage - 1);
                }
            } else if (msg.StartsWith("image:"))
            {
                string imageId = msg["image:".Length..];
                string? imagePath = BookManager.GetBookImagePathById(_bookHash, imageId);
                if (imagePath != null)
                {
                    Frame.Navigate(typeof(ImageViewerPage), imagePath);
                }
            }
        }

        private async Task ShowSelectedTextPopup(string text, int start)
        {
            if (_isDialogShowing)
            {
                return;
            }

            _isDialogShowing = true;
            SelectionDialog dialog = new(start, text.Length, text, _currentPage, _bookHash);
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
