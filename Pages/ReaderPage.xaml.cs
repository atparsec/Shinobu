using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;

namespace Shinobu.Pages
{
    public sealed partial class ReaderPage : Page, INotifyPropertyChanged
    {
        private string _filePath = string.Empty;
        private List<string> _pages = new();
        private int _currentPage = 0;

        public event PropertyChangedEventHandler? PropertyChanged;

        public bool CanGoPrev => _currentPage > 0;
        public bool CanGoNext => _currentPage < _pages.Count - 1;
        public string PageText => $"{_currentPage + 1} / {_pages.Count}";

        public ReaderPage()
        {
            InitializeComponent();
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
            var lineHeight = settings.Values.TryGetValue("LineHeight", out var lh) && lh is double lhd ? lhd : 1.2;

            int linesPerPage = 42;
            int charsPerLine = 80;
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
            var lineHeight = settings.Values.TryGetValue("LineHeight", out var lh) && lh is double lhd ? lhd : 1.2;
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
    </style>
    </head>
    <body>
    {furiganaText}
    </body>
    </html>";
            await ReaderWebView.EnsureCoreWebView2Async();
            ReaderWebView.NavigateToString(html);
        }

        private async Task<string> GenerateFurigana(string text)
        {
            // TODO
            return text.Replace("\n", "<br>");
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

        private void OnPropertyChanged()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        }
    }
}
