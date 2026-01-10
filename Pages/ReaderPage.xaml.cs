using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Web.WebView2.Core;
using Shinobu.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.System;

namespace Shinobu.Pages
{
    public sealed partial class ReaderPage : Page, INotifyPropertyChanged
    {
        private string _filePath = string.Empty;
        private List<string> _pages = new();
        private int _currentPage = 0;
        private bool _isDialogShowing = false;
        public event PropertyChangedEventHandler? PropertyChanged;
        private FuriganaGenerator _furiganaGenerator = new();
        private JlptLevel _userJlptLevel;

        public bool CanGoPrev => _currentPage > 0;
        public bool CanGoNext => _currentPage < _pages.Count - 1;
        public string PageText => $"{_currentPage + 1} / {_pages.Count}";
        public List<JlptLevel> JlptLevels { get; } = Enum.GetValues<JlptLevel>().ToList();

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
            ReaderWebView.WebMessageReceived += OnWebMessageReceived;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is string path)
            {
                _filePath = path;
                await LoadBook();
            }
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
            var fontSize = settings.Values.TryGetValue("FontSize", out var fs) && fs is double fsd ? fsd : 16.0;
            var lineHeight = settings.Values.TryGetValue("LineHeight", out var lh) && lh is double lhd ? lhd : 3.0;
            var theme = settings.Values.TryGetValue("Theme", out var t) ? t as string : "System";
            var backgroundColor = "#FFF";
            var textColor = "#000";
            if (theme == "Dark" || (theme == "System" && Application.Current.RequestedTheme == ApplicationTheme.Dark))
            {
                backgroundColor = "#000";
                textColor = "#fff";
            }

            var html = $@"
    <html>
    <head>
    <style>
    body {{ background-color: {backgroundColor}; color: {textColor}; font-size: {fontSize}px; line-height: {lineHeight}; font-family: Arial, sans-serif; padding: 20px; }}
    rt {{-webkit - user - select: none; /* For Safari */
    -ms-user-select: none; /* For Internet Explorer */
    user-select: none; /* Non-selectable for other browsers */
    }}
    </style>
    </head>
    <body>
    {furiganaText}
    </body>
    </html>";
            await ReaderWebView.EnsureCoreWebView2Async();
            ReaderWebView.NavigateToString(html);
            await ReaderWebView.ExecuteScriptAsync(@"
                document.addEventListener('mouseup', function() {
                    var selectedText = window.getSelection().toString();
                    if (selectedText.trim()) {
                        window.chrome.webview.postMessage('selected:' + selectedText);
                    }
                });
            ");
        }

        private async Task<string> GenerateFurigana(string text)
        {
            return (await _furiganaGenerator.GenerateHtmlFuriganaAsync(text, UserJlptLevel)).Replace("\r\n", "<br/>").Replace("\n", "<br/>");
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
                Content = new TextBlock() { Text = "Options here" },
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
            var dialog = new ContentDialog()
            {
                Title = "Selected Text",
                Content = new TextBlock() { Text = text },
                CloseButtonText = "Close",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
            _isDialogShowing = false;
        }

        private void OnPropertyChanged(string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
