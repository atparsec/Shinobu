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
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Popups;

namespace Shinobu.Pages
{
    public sealed partial class ReaderPage : Page, INotifyPropertyChanged
    {
        private string _filePath = string.Empty;
        private List<string> _pages = [];
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
                    _ = LoadBook();
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
                    _ = LoadBook();
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
                    _ = LoadBook();
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
                    _ = LoadBook();
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
                    _ = LoadBook();
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
                    _ = DisplayCurrentPage();
                    OnPropertyChanged(nameof(UserJlptLevel));
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
            _pageMargin = _settings.Values.TryGetValue("PageMargin", out object? pm) && pm is double pmd ? pmd : 20.0;
            _theme = _settings.Values.TryGetValue("Theme", out object? t) && t is string themeStr ? themeStr : "Dark";
            ReaderWebView.WebMessageReceived += OnWebMessageReceived;
            ReaderWebView.NavigationCompleted += ReaderWebView_NavigationCompleted;
        }

        private async void ReaderWebView_NavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            _ = await sender.ExecuteScriptAsync(@"
                if (!window.__selectionListenerAttached) {
                    document.addEventListener('mouseup', function () {
                        var selection = window.getSelection();

                        if (selection.rangeCount) {
                            var range = selection.getRangeAt(0);
                            var fragment = range.cloneContents();

                            fragment.querySelectorAll('.furigana').forEach(el => el.remove());

                            var selectedText = fragment.textContent;
                            if (selectedText.trim()) {
                                var bodyText = document.body.textContent || document.body.innerText;
                                var startOffset = getTextOffset(document.body, range.startContainer, range.startOffset);
                                window.chrome.webview.postMessage('selected:' + startOffset + ':' + selectedText);
                            }
                        }
                    });
                    function getTextOffset(root, node, offset) {
                        var text = '';
                        var walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, null, false);
                        var currentNode;
                        while (currentNode = walker.nextNode()) {
                            if (currentNode === node) {
                                return text.length + offset;
                            }
                            text += currentNode.textContent;
                        }
                        return text.length;
                    }
                    window.__selectionListenerAttached = true;
                }
        ");
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            ConnectedAnimation anim = ConnectedAnimationService.GetForCurrentView().GetAnimation("ForwardConnectedAnimation");

            _ = anim?.TryStart(ReaderWebView);

            await ReaderWebView.EnsureCoreWebView2Async();
            if (e.Parameter is string path)
            {
                string[] parts = path.Split(';');
                _filePath = parts[0];
                await LoadBook();

                if (parts.Length > 1 && int.TryParse(parts[1], out int pageNum))
                {
                    _currentPage = Math.Min(pageNum - 1, _pages.Count - 1);
                    OnPropertyChanged();
                    await DisplayCurrentPage();
                }
                if (parts.Length > 3 && int.TryParse(parts[2], out int offset) && int.TryParse(parts[3], out int endOffset))
                {
                    int cumulativeLength = 0;
                    for (int i = 0; i < _pages.Count; i++)
                    {
                        int pageLength = _pages[i].Length;
                        if (cumulativeLength + pageLength >= offset)
                        {
                            _currentPage = i;
                            OnPropertyChanged();
                            await DisplayCurrentPage();
                            int offsetInPage = offset - cumulativeLength;
                            int lengthInPage = Math.Min(endOffset, pageLength - offsetInPage);
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
                                var startInfo = getTextNodeAtOffset(document.body, {offsetInPage});
                                var endInfo = getTextNodeAtOffset(document.body, {offsetInPage + lengthInPage});
                                if (startInfo && endInfo) {{
                                    range.setStart(startInfo.node, startInfo.offset);
                                    range.setEnd(endInfo.node, endInfo.offset);
                                    selection.removeAllRanges();
                                    selection.addRange(range);
                                }}
                            ");
                            break;
                        }
                        cumulativeLength += pageLength;
                    }
                }
            }
            else
            {
                (string? sessionFilePath, int sessionPage) = ReaderSessionManager.GetSession();
                if (sessionFilePath != null && File.Exists(sessionFilePath))
                {
                    _filePath = sessionFilePath;
                    await LoadBook();
                    _currentPage = Math.Min(sessionPage, _pages.Count - 1);
                    OnPropertyChanged();
                    await DisplayCurrentPage();
                }
            }

            if (App.MainWindowInstance is MainWindow mainWindow)
            {
                mainWindow.SelectReaderNavigation();
            }

        }

        private async Task<bool> CheckFileExists(string path)
        {
            if (!File.Exists(path))
            {
                ReaderSessionManager.ClearSession();
                MessageDialog info = new("The file was not found. It may have been moved or deleted.", "File Not Found");
                _ = await info.ShowAsync();
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
            string content = await ContentParserFactory.GetParser(Path.GetExtension(_filePath)).ParseContentAsync(_filePath);

            double fontSize = ReaderFontSize;
            double lineHeight = LineHeight;
            double pageMargin = ReaderMargin;
            double webViewWidth = ReaderWebView.ActualWidth - (2 * pageMargin);
            double webViewHeight = ReaderWebView.ActualHeight - (2 * pageMargin);

            double avgCharWidth = fontSize * 0.76;
            double avgLineHeight = (fontSize * lineHeight) + fontSize;
            int navHeight = 65;

            int charsPerLineApprox = (int)((webViewWidth - (IsVerticalText ? navHeight : 0)) / avgCharWidth);
            int linesPerPageApprox = (int)((webViewHeight-navHeight) / avgLineHeight * 0.9);

            int targetCharsPerPage = charsPerLineApprox * linesPerPageApprox;

            _pages.Clear();
            for (int i = 0; i < content.Length; i += targetCharsPerPage)
            {
                string page = content.Substring(i, Math.Min(targetCharsPerPage, content.Length - i));
                _pages.Add(page);
            }
            _currentPage = 0;
            OnPropertyChanged();
            await DisplayCurrentPage();
        }

        private async Task DisplayCurrentPage()
        {
            if (_pages.Count == 0) return;

            string text = _pages[_currentPage];
            string furiganaText = await GenerateFurigana(text);

            double fontSize = _fontSize;
            double lineHeight = _lineHeight;
            string fontFamily = _readerFont.Source;
            string backgroundColor = "#FFF";
            string shadowColor = "#EEEEEEFF";
            string textColor = "#000";

            Color accentColor = new Windows.UI.ViewManagement.UISettings().GetColorValue(Windows.UI.ViewManagement.UIColorType.Accent);
            string accentHex = $"#{accentColor.R:X2}{accentColor.G:X2}{accentColor.B:X2}CC";
            if (_theme == "Dark" || (_theme == "System" && Application.Current.RequestedTheme == ApplicationTheme.Dark))
            {
                backgroundColor = "#000";
                shadowColor = "#151515FF";
                textColor = "#fff";
            }
            string gradientFormat = $"radial-gradient(circle, {backgroundColor} 0%, {shadowColor} 100%)";

            string bodyStyle = $"background: {gradientFormat}; color: {textColor}; font-size: {fontSize}px; line-height: {lineHeight * fontSize}px; font-family: {fontFamily}; padding: {_pageMargin}px; overflow: hidden;";
            if (_isVerticalText)
            {
                bodyStyle += " writing-mode: vertical-rl; text-orientation: mixed; padding-bottom: 50px;";
            }

            string html = $@"
                <html>
                <head>
                    <style>
                        body {{ {bodyStyle} }}
                        rt {{user-select: none; pointer-events: none;}}
                        ::selection {{ 
                            background: {accentHex};
                            box-shadow: inset 0 0 12px rgba(255, 190, 40, 0.35);
                            text-shadow: 0 0 5px rgba(255, 220, 60, 0.6);
                        }}
                    </style>
                </head>
                <body>
                    {furiganaText}
                </body>
                </html>";
            ReaderWebView.NavigateToString(html);
        }

        private async Task<string> GenerateFurigana(string text)
        {
            return (await _furiganaGenerator.GenerateHtmlFuriganaAsync(text, UserJlptLevel));
        }

        public async Task UpdateDisplayAsync()
        {
            await DisplayCurrentPage();
        }

        private void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 0)
            {
                _currentPage--;
                OnPropertyChanged();
                _ = DisplayCurrentPage();
            }
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage < _pages.Count - 1)
            {
                _currentPage++;
                OnPropertyChanged();
                _ = DisplayCurrentPage();
            }
        }

        private async void PageOptionsButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog()
            {
                Title = "Page Options",
                Content = new PageOptionsDialog(this),
                CloseButtonText = "Close",
                XamlRoot = XamlRoot
            };
            _ = await dialog.ShowAsync();
        }

        private async void OnWebMessageReceived(WebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            string message = args.TryGetWebMessageAsString();
            if (message.StartsWith("selected:"))
            {
                string[] parts = message["selected:".Length..].Split(':', 2);
                if (parts.Length == 2 && int.TryParse(parts[0], out int start))
                {
                    string selectedText = parts[1];
                    await ShowSelectedTextPopup(selectedText, start);
                }
            }
        }

        private async Task ShowSelectedTextPopup(string text, int start)
        {
            if (_isDialogShowing) return;
            _isDialogShowing = true;
            for (int i = 0; i < _currentPage; i++)
            {
                start += _pages[i].Length;
            }
            var dialog = new SelectionDialog(start, text.Length, text, _currentPage, _filePath);
            var overlay = new Grid
            {
                Background = new SolidColorBrush(Microsoft.UI.Colors.Black) { Opacity = 0.5 },
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            void closeDialog()
            {
                (this.Content as Panel)?.Children.Remove(overlay);
                (this.Content as Panel)?.Children.Remove(dialog);
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

        private void OnPropertyChanged(string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
