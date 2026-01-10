using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Media3D;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Web.WebView2.Core;
using Shinobu.Dialogs;
using Shinobu.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
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

        public bool CanGoPrev => _currentPage > 0;
        public bool CanGoNext => _currentPage < _pages.Count - 1;
        public string PageText => $"{_currentPage + 1} / {_pages.Count}";
        public List<JlptLevel> JlptLevels { get; } = Enum.GetValues<JlptLevel>().ToList();

        public bool IsVerticalText
        {
            get => _isVerticalText;
            set
            {
                if (_isVerticalText != value)
                {
                    _isVerticalText = value;
                    var settings = ApplicationData.Current.LocalSettings;
                    settings.Values["IsVerticalText"] = value;
                    _ = DisplayCurrentPage();
                    OnPropertyChanged(nameof(IsVerticalText));
                }
            }
        }

        public double FontSize
        {
            get => _fontSize;
            set
            {
                if (_fontSize != value)
                {
                    _fontSize = value;
                    var settings = ApplicationData.Current.LocalSettings;
                    settings.Values["FontSize"] = value;
                    _ = DisplayCurrentPage();
                    OnPropertyChanged(nameof(FontSize));
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
                    var settings = ApplicationData.Current.LocalSettings;
                    settings.Values["LineHeight"] = value;
                    _ = DisplayCurrentPage();
                    OnPropertyChanged(nameof(LineHeight));
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
                    var settings = ApplicationData.Current.LocalSettings;
                    settings.Values["JlptLevel"] = (int)value;
                    _ = DisplayCurrentPage();
                    OnPropertyChanged(nameof(UserJlptLevel));
                }
            }
        }

        public ReaderPage()
        {
            InitializeComponent();
            var settings = ApplicationData.Current.LocalSettings;
            _userJlptLevel = settings.Values.TryGetValue("JlptLevel", out var levelObj) && levelObj is int levelInt ? (JlptLevel)levelInt : JlptLevel.N5;
            _isVerticalText = settings.Values.TryGetValue("IsVerticalText", out var vt) && vt is bool b && b;
            _fontSize = settings.Values.TryGetValue("FontSize", out var fs) && fs is double fsd ? fsd : 16.0;
            _lineHeight = settings.Values.TryGetValue("LineHeight", out var lh) && lh is double lhd ? lhd : 3.0;
            ReaderWebView.WebMessageReceived += OnWebMessageReceived;
            ReaderWebView.NavigationCompleted += ReaderWebView_NavigationCompleted;
        }

        private async void ReaderWebView_NavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            await sender.ExecuteScriptAsync(@"
        if (!window.__selectionListenerAttached) {
            document.addEventListener('mouseup', function () {
                var selection = window.getSelection();

                if (selection.rangeCount) {
                    var range = selection.getRangeAt(0);
                    var fragment = range.cloneContents();

                    fragment.querySelectorAll('.furigana').forEach(el => el.remove());

                    var selectedText = fragment.textContent;
                    if (selectedText.trim()) {
                        window.chrome.webview.postMessage('selected:' + selectedText);
                    }
                }
            });
            window.__selectionListenerAttached = true;
        }
        ");
        }


        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            await ReaderWebView.EnsureCoreWebView2Async();
            if (e.Parameter is string path)
            {
                _filePath = path;
                await LoadBook();
            }
            else
            {
                var (sessionFilePath, sessionPage) = ReaderSessionManager.GetSession();
                if (sessionFilePath != null && File.Exists(sessionFilePath))
                {
                    _filePath = sessionFilePath;
                    if (!File.Exists(_filePath))
                    {
                        ReaderSessionManager.ClearSession();
                        MessageDialog info = new("The file from your last reading session could not be found.", "File Not Found");
                        await info.ShowAsync();
                        return;
                    }
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
            var content = await File.ReadAllTextAsync(_filePath);

            // calculate chars per page
            var settings = ApplicationData.Current.LocalSettings;
            var fontSize = settings.Values.TryGetValue("FontSize", out var fs) && fs is double fsd ? fsd : 16.0;
            var lineHeight = settings.Values.TryGetValue("LineHeight", out var lh) && lh is double lhd ? lhd : 3.0;

            int linesPerPage = 20;
            int charsPerLine = 30;
            int charsPerPage = linesPerPage * charsPerLine;

            _pages.Clear();
            for (int i = 0; i < content.Length; i += charsPerPage)
            {
                var page = content.Substring(i, Math.Min(charsPerPage, content.Length - i));
                _pages.Add(page);
            }
            _currentPage = 0;
            OnPropertyChanged();
            await DisplayCurrentPage();
        }

        private async Task DisplayCurrentPage()
        {
            if (_pages.Count == 0) return;

            var text = _pages[_currentPage];
            var furiganaText = await GenerateFurigana(text);

            // set to webview
            var settings = ApplicationData.Current.LocalSettings;
            var fontSize = _fontSize;
            var lineHeight = _lineHeight;
            var theme = settings.Values.TryGetValue("Theme", out var t) ? t as string : "System";
            var backgroundColor = "#FFF";
            var shadowColor = "#EEEEEEFF";
            var textColor = "#000";

            var accentColor = new Windows.UI.ViewManagement.UISettings().GetColorValue(Windows.UI.ViewManagement.UIColorType.Accent);
            var accentHex = $"#{accentColor.R:X2}{accentColor.G:X2}{accentColor.B:X2}CC";
            if (theme == "Dark" || (theme == "System" && Application.Current.RequestedTheme == ApplicationTheme.Dark))
            {
                backgroundColor = "#000";
                shadowColor = "#151515FF";
                textColor = "#fff";
            }
            var gradientFormat = $"radial-gradient(circle, {backgroundColor} 0%, {shadowColor} 100%)";

            var bodyStyle = $"background: {gradientFormat}; color: {textColor}; font-size: {fontSize}px; line-height: {lineHeight}; font-family: Arial, sans-serif; padding: 20px;";
            if (_isVerticalText)
            {
                bodyStyle += " writing-mode: vertical-rl; text-orientation: mixed; padding-bottom: 50px;";
            }

            var html = $@"
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
            return (await _furiganaGenerator.GenerateHtmlFuriganaAsync(text, UserJlptLevel)).Replace("\r\n", "<br/>").Replace("\n", "<br/>");
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
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }

        private async void OnWebMessageReceived(WebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            var message = args.TryGetWebMessageAsString();
            if (message.StartsWith("selected:"))
            {
                var selectedText = message.Substring("selected:".Length);
                await ShowSelectedTextPopup(selectedText);
            }
        }

        private async Task ShowSelectedTextPopup(string text)
        {
            if (_isDialogShowing) return;
            _isDialogShowing = true;
            var dialog = new SelectionDialog(text);
            var overlay = new Grid
            {
                Background = new SolidColorBrush(Microsoft.UI.Colors.Black) { Opacity = 0.5 },
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            Action closeDialog = () =>
            {
                (this.Content as Panel)?.Children.Remove(overlay);
                (this.Content as Panel)?.Children.Remove(dialog);
                _isDialogShowing = false;
                _ = ReaderWebView.ExecuteScriptAsync("window.getSelection().removeAllRanges();");
            };
            dialog.CloseAction = closeDialog;
            overlay.PointerPressed += (s, e) => closeDialog();
            (this.Content as Panel)?.Children.Add(overlay);
            (this.Content as Panel)?.Children.Add(dialog);
            dialog.HorizontalAlignment = HorizontalAlignment.Center;
            dialog.VerticalAlignment = VerticalAlignment.Center;
        }

        private void OnPropertyChanged(string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
