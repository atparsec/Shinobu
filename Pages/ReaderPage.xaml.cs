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
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Popups;

namespace Shinobu.Pages
{
    public sealed partial class ReaderPage : Page, INotifyPropertyChanged
    {
        private string _filePath = string.Empty;
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
        private readonly Dictionary<string, BookContent> _contentCache = [];
        private string? _currentFileHash;

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
                    if (_currentFileHash != null)
                    {
                        _ = RenderBook(_currentFileHash);
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
                    if (_currentFileHash != null)
                    {
                        _ = RenderBook(_currentFileHash);
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
                    if (_currentFileHash != null)
                    {
                        _ = RenderBook(_currentFileHash);
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
                    if (_currentFileHash != null)
                    {
                        _ = RenderBook(_currentFileHash);
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
                    if (_currentFileHash != null)
                    {
                        _ = RenderBook(_currentFileHash);
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
                    if (_currentFileHash != null)
                    {
                        _ = RenderBook(_currentFileHash);
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
                    if (_currentFileHash != null)
                    {
                        _ = RenderBook(_currentFileHash);
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

            if (e.Parameter is string path)
            {
                string[] parts = path.Split(';');
                _filePath = parts[0];
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
                (string? sessionFilePath, int sessionPage) = ReaderSessionManager.GetSession();
                if (sessionFilePath != null && File.Exists(sessionFilePath))
                {
                    _filePath = sessionFilePath;
                    await LoadBook();
                    await GoToPage(sessionPage);
                }

                LoadingRing.Visibility = Visibility.Collapsed;
            }

            if (App.MainWindowInstance is MainWindow mainWindow)
            {
                mainWindow.SelectReaderNavigation();
            }

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

        private async Task<bool> CheckFileExists(string path)
        {
            if (!File.Exists(path))
            {
                ReaderSessionManager.ClearSession();
                MessageDialog info = new("The file was not found. It may have been moved or deleted.", "File Not Found");
                await info.ShowAsync();
                return false;
            }

            return true;
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            if (!string.IsNullOrEmpty(_filePath))
            {
                ReaderSessionManager.SaveSession(_filePath, _currentPage);
            }
            base.OnNavigatingFrom(e);
            ReaderWebView.NavigationCompleted -= ReaderWebView_NavigationCompleted;
            ReaderWebView.WebMessageReceived -= OnWebMessageReceived;
        }

        private async Task LoadBook()
        {
            if (!await CheckFileExists(_filePath)) { return; }

            string fileHash = await ComputeFileHash(_filePath);
            _currentFileHash = fileHash;
            if (!_contentCache.TryGetValue(fileHash, out _bookContent))
            {
                _bookContent = await ContentParserFactory
                    .GetParser(Path.GetExtension(_filePath))
                    .ParseContentAsync(_filePath);
                _contentCache[fileHash] = _bookContent;

                string bookDir = Path.Combine(Path.GetTempPath(), "shinobu", "books", fileHash);
                Directory.CreateDirectory(bookDir);
                string imagesDir = Path.Combine(bookDir, "images");
                Directory.CreateDirectory(imagesDir);
                foreach (var img in _bookContent.Images)
                {
                    byte[] data = img.ImageData;
                    string imgPath = Path.Combine(imagesDir, $"{img.Id}{img.Extension}");
                    await File.WriteAllBytesAsync(imgPath, data);
                    img.ImageData = [];
                }
            }

            _pagesLoaded = new TaskCompletionSource();
            await RenderBook(fileHash);
        }

        private async Task RenderBook(string fileHash)
        {
            string htmlSettings = $"{_userJlptLevel}";
            string htmlHash = ComputeSettingsHash(htmlSettings);
            string bookDir = Path.Combine(Path.GetTempPath(), "shinobu", "books", fileHash);
            string htmlFile = Path.Combine(bookDir, $"{fileHash}_{htmlHash}.html");
            string cssFile = Path.Combine(Path.GetTempPath(), "shinobu", "shinobu_styles.css");

            string cssContent = BuildCss();
            Directory.CreateDirectory(Path.GetDirectoryName(cssFile)!);
            await File.WriteAllTextAsync(cssFile, cssContent);

            if (!File.Exists(htmlFile))
            {
                string htmlContent = await BuildHtml();
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

        private string HTMLError(string message)
        {

            return $@"
                <div style='
                position: fixed;
                top: 0; left: 0; right: 0;
                z-index: 10000;
                background: #ef4444 !important;
                color: white !important;
                padding: 16px;
                font-family: system-ui, sans-serif;
                font-size: 15px;
                font-weight: 500;
                text-align: center;
                box-shadow: 0 2px 10px rgba(0,0,0,0.2);
                border: 2px solid #dc2626;'>
                <strong>{message}</strong>
            </div><br/>";
        }

        private async Task<string> BuildHtml()
        {
            string textWithImages;
            try
            {
                textWithImages = InjectImages(_bookContent.TextContent);
            }
            catch (Exception ex)
            {
                textWithImages = HTMLError("Could not inject images " + ex.Message) + _bookContent.TextContent;
            }
            string furiganaHtml;
            try
            {
                furiganaHtml = await _furiganaGenerator.GenerateHtmlFuriganaAsync(textWithImages, _userJlptLevel);
            }
            catch (Exception ex)
            {
                furiganaHtml = HTMLError("Could not generate furigana " + ex.Message) + textWithImages;
            }

            return $@"
                    <html>
                    <head>
                    <link rel='stylesheet' href='../../shinobu_styles.css'>
                    </head>
                    <body>
                    <div id='pager'>
                    {furiganaHtml.Replace("\n", "<br/>")}
                    </div>

                    <script>
                    function paginate() {{
                        const isVertical = {_isVerticalText.ToString().ToLower()};
                        let totalPages;
                        let lengths;
                        if (isVertical) {{
                            const pageHeight = document.documentElement.clientHeight;
                            totalPages = Math.ceil(pager.scrollHeight / pageHeight);
                            lengths = new Array(totalPages).fill(0);
                        }} else {{
                            const pageWidth = document.documentElement.clientWidth;
                            totalPages = Math.ceil(pager.scrollWidth / pageWidth);
                            lengths = new Array(totalPages).fill(0);
                        }}
                        const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT, null, false);
                        let node;
                        while (node = walker.nextNode()) {{
                            const range = document.createRange();
                            range.selectNodeContents(node);
                            const rect = range.getBoundingClientRect();
                            let page;
                            if (isVertical) {{
                                const pageHeight = document.documentElement.clientHeight;
                                page = Math.floor(rect.top / pageHeight);
                            }} else {{
                                const pageWidth = document.documentElement.clientWidth;
                                page = Math.floor(rect.left / pageWidth);
                            }}
                            if (page >= 0 && page < totalPages) {{
                                lengths[page] += node.textContent.length;
                            }}
                        }}
                        window.chrome.webview.postMessage('pages:' + JSON.stringify(lengths));
                    }}

                    function goToPage(p) {{
                        if ({_isVerticalText.ToString().ToLower()}) {{
                            const pageHeight = document.documentElement.clientHeight;
                            window.scrollTo({{ top: p * (pageHeight+{ReaderMargin - 30}), behavior: 'smooth' }});
                            return;
                        }}
                        const pageWidth = document.documentElement.clientWidth;
                        window.scrollTo({{ left: p * pageWidth, behavior: 'smooth' }});
                    }}

                    document.addEventListener('mouseup', () => {{
                        const sel = window.getSelection();
                        if (!sel.rangeCount) return;

                        const range = sel.getRangeAt(0).cloneContents();
                        range.querySelectorAll('rt').forEach(e => e.remove());

                        const text = range.textContent.trim();
                        if (!text) return;

                        const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
                        let offset = 0;
                        let node;
                        while (node = walker.nextNode()) {{
                            if (node === sel.anchorNode) {{
                                offset += sel.anchorOffset;
                                break;
                            }}
                            offset += node.textContent.length;
                        }}

                        window.chrome.webview.postMessage('selected:' + offset + ':' + text);
                    }});

                    window.addEventListener('resize', paginate);
                    paginate();
                    </script>
                    </body>
                    </html>";
        }

        private string InjectImages(string text)
        {
            var sb = new StringBuilder(text);

            foreach (var img in _bookContent.Images.OrderByDescending(i => i.Offset))
            {
                sb.Insert(img.Offset,
                    $"<img src='images/{img.Id}{img.Extension}' style='max-width:100%;display:block;margin:1em auto;'/>");
            }

            return sb.ToString();
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
        }

        private async Task ShowSelectedTextPopup(string text, int start)
        {
            if (_isDialogShowing)
            {
                return;
            }

            _isDialogShowing = true;
            SelectionDialog dialog = new(start, text.Length, text, _currentPage, _filePath);
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

        private string BuildCss()
        {
            double fontSize = _fontSize;
            double lineHeight = _lineHeight;
            string fontFamily = _readerFont.Source;
            BookTheme currentTheme = CurrentTheme;
            string backgroundColor = currentTheme.Background;
            string textColor = currentTheme.Foreground;
            Color accentColor = new Windows.UI.ViewManagement.UISettings().GetColorValue(Windows.UI.ViewManagement.UIColorType.Accent);
            string accentHex = $"#{accentColor.R:X2}{accentColor.G:X2}{accentColor.B:X2}CC";

            string bodyStyle = $@"
                background-color: {backgroundColor};
                color: {textColor}; 
                font-size: {fontSize}px; 
                line-height: {lineHeight * fontSize}px; 
                font-family: {fontFamily}; 
                overflow: hidden;
                padding: 0;
                margin: 0;
                overflow-wrap: normal;
                ";

            string pagerStyle = $@"
                column-width: {ReaderWebView.ActualWidth}px;
                text-align: justify;
                padding: {ReaderMargin}px;
                text-combine-upright: digits 2;
                hanging-punctuation: allow-end;
                line-break: strict;
            ";
            if (IsVerticalText)
            {
                pagerStyle += $@"
                    
                    width: calc(100% - {ReaderMargin * 2}px);
                    column-gap: {ReaderMargin * 2 + 40}px;
                    margin-bottom: 40px;
                    writing-mode: vertical-rl;
                    text-orientation: mixed;
                ";
            }
            else
            {
                pagerStyle += $@"
                    max-height: {ReaderWebView.ActualHeight - 120}px;
                    column-gap: {ReaderMargin * 2}px;
                    box-sizing: border-box;
                    position: relative; 
                    margin: 0px;
                ";
            }

            return $@"
                    body {{ {bodyStyle} }}
                    #pager {{
                        {pagerStyle}
                    }}
                    rt {{
                        user-select: none;
                        pointer-events: none;
                    }}
                    ::selection {{ 
                            background: {accentHex};
                            box-shadow: inset 0 0 12px rgba(255, 190, 40, 0.35);
                            text-shadow: 0 0 5px rgba(255, 220, 60, 0.6);
                        }}";
        }
    }
}
