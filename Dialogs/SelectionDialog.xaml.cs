using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using Shinobu.Helpers;
using System;
using Windows.Storage;
using System.Threading.Tasks;
using System.Linq;
using Windows.ApplicationModel.DataTransfer;

namespace Shinobu.Dialogs
{
    public sealed partial class SelectionDialog : UserControl
    {
        public string SelectedText { get; set; }
        public Action? CloseAction { get; set; }

        public SelectionDialog(string selectedText)
        {
            SelectedText = selectedText;
            InitializeComponent();

            string trimmed = selectedText.Length > 5 ? selectedText.Substring(0, 5) + "..." : selectedText;
            SelectedWordText.Text = trimmed;

            ExplainTextBox.Text = "Explain: " + selectedText;

            var settings = ApplicationData.Current.LocalSettings;
            var aiProvider = settings.Values.TryGetValue("AIProvider", out var p) && p is string s ? s : "";
            AINavViewItem.Content = aiProvider;

            var aiEnabled = settings.Values.TryGetValue("AIEnabled", out var enabled) && enabled is bool b && b;
            AINavViewItem.Visibility = aiEnabled ? Visibility.Visible : Visibility.Collapsed;

            MainNavigationView.SelectedItem = MainNavigationView.MenuItems[0];
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await LoadDictionaryDefinition(SelectedText);
        }

        private async Task LoadDictionaryDefinition(string word)
        {
            if (App.Dictionary != null)
            {
                var def = await App.Dictionary.GetDefinitionAsync(word);
                ReadingText.Text = def.Reading;
                // Tags
                TagsPanel.Children.Clear();
                foreach (var tag in def.Tags)
                {
                    var border = new Border
                    {
                        Background = new SolidColorBrush(Microsoft.UI.Colors.Black),
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(8, 4, 8, 4),
                        Margin = new Thickness(0, 0, 8, 0)
                    };
                    border.Child = new TextBlock { Text = tag, FontSize = 14 };
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
                        await LoadDictionaryDefinition(SelectedText);
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
            if (string.IsNullOrEmpty(text)) return;

            var data = new DataPackage();
            data.SetText(text);

            Clipboard.SetContent(data);

            CloseAction?.Invoke();

        }
    }
}
