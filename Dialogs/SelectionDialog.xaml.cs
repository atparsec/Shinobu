using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Shinobu.Helpers;
using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace Shinobu.Dialogs
{
    public sealed partial class SelectionDialog : UserControl
    {
        public string SelectedText { get; set; }
        public Action? CloseAction { get; set; }
        public int pageNumber { get; set; }
        public string filePath { get; set; }

        public SelectionDialog(string selectedText, int pageNumber, string filePath)
        {
            SelectedText = selectedText;
            this.pageNumber = pageNumber;
            this.filePath = filePath;
            InitializeComponent();

            string trimmed = selectedText.Length > 5 ? selectedText[..5] + "..." : selectedText;
            SelectedWordText.Text = trimmed;

            ExplainTextBox.Text = "Explain: " + selectedText;

            ApplicationDataContainer settings = ApplicationData.Current.LocalSettings;
            string aiProvider = settings.Values.TryGetValue("AIProvider", out object? p) && p is string s ? s : "";
            AINavViewItem.Content = aiProvider;

            bool aiEnabled = settings.Values.TryGetValue("AIEnabled", out object? enabled) && enabled is bool b && b;
            AINavViewItem.Visibility = aiEnabled ? Visibility.Visible : Visibility.Collapsed;

            MainNavigationView.SelectedItem = MainNavigationView.MenuItems[0];
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await ShowDictionaryPage();
        }

        private async Task LoadDictionaryDefinition(string word)
        {
            if (App.Dictionary != null)
            {
                Definition def = await App.Dictionary.GetDefinitionAsync(word);
                ReadingText.Text = def.Reading;
                // Tags
                TagsPanel.Children.Clear();
                foreach (string tag in def.Tags)
                {
                    string color = UIColorHelper.HashStringToColor(tag);
                    var border = new Border
                    {
                        Background = new SolidColorBrush(new Windows.UI.Color
                        {
                            A = 255,
                            R = Convert.ToByte(color.Substring(1, 2), 16),
                            G = Convert.ToByte(color.Substring(3, 2), 16),
                            B = Convert.ToByte(color.Substring(5, 2), 16)
                        }),
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(8, 4, 8, 4),
                        Margin = new Thickness(0, 0, 8, 8),
                        Child = new TextBlock
                        {
                            Text = tag,
                            FontSize = 14,
                            Foreground = new SolidColorBrush(UIColorHelper.DarkOrLightColor(color) ? Colors.White : Colors.Black)
                        }
                    };
                    TagsPanel.Children.Add(border);
                }
                // Meanings
                var meanings = def.Meaning.Split(';').Select((m, i) => new { Number = i + 1, Text = m.Trim(), ExtraInfo = "" });
                DefinitionsList.ItemsSource = meanings;
            }
            else
            {
                ReadingText.Text = "Dictionary not loaded";
                DefinitionsList.ItemsSource = null;
                TagsPanel.Children.Clear();
            }
        }

        private async Task ShowDictionaryPage()
        {
            DictionaryProgressRing.Visibility = Visibility.Visible;
            DictionaryContent.Visibility = Visibility.Collapsed;
            await LoadDictionaryDefinition(SelectedText);
            DictionaryProgressRing.Visibility = Visibility.Collapsed;
            DictionaryContent.Visibility = Visibility.Visible;
        }

        private async void MainNavigationView_SelectionChanged(
            NavigationView sender,
            NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem item)
            {
                // Hide all pages
                DictionaryPage.Visibility = Visibility.Collapsed;
                TranslatePage.Visibility = Visibility.Collapsed;
                AIPage.Visibility = Visibility.Collapsed;

                // Show selected page
                switch (item.Tag?.ToString())
                {
                    case "Dictionary":
                        DictionaryPage.Visibility = Visibility.Visible;
                        await ShowDictionaryPage();
                        break;

                    case "Translate":
                        TranslatePage.Visibility = Visibility.Visible;
                        break;

                    case "AI":
                        AIPage.Visibility = Visibility.Visible;
                        break;
                }
            }
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            string text = SelectedText;
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var data = new DataPackage();
            data.SetText(text);

            Clipboard.SetContent(data);

            CloseAction?.Invoke();

        }

        private async void SpeakButton_Click(object sender, RoutedEventArgs e)
        {
            if (App.SpeechSynth == null)
            {
                ContentDialog dialog = new()
                {
                    Title = "Japanese Voice Not Found",
                    Content = "No Japanese voice is installed on your system. Please install a Japanese voice for text-to-speech functionality.",
                    CloseButtonText = "OK",
                    XamlRoot = App.MainWindowInstance!.Content.XamlRoot
                };
                _ = await dialog.ShowAsync();
                CloseAction?.Invoke();
                return;
            }
            App.SpeechSynth.SetOutputToDefaultAudioDevice();
            await Task.Run(() =>
            {
                App.SpeechSynth.Speak(SelectedText);
            });
        }

        private void BookmarkButton_Click(object sender, RoutedEventArgs e)
        {
            if (App.BookmarksManager != null)
            {
                var bookmark = new Bookmark
                {
                    Text = SelectedText,
                    Note = string.Join("; ", DefinitionsList.Items.Cast<dynamic>().Select(m => m.Text)),
                    Tags = [.. TagsPanel.Children.Cast<Border>().Select(b => ((TextBlock)b.Child).Text)],
                    PageNumber = pageNumber + 1,
                    FilePath = filePath
                };
                _ = App.BookmarksManager.AddBookmarkAsync(bookmark);
            }
        }
    }
}
