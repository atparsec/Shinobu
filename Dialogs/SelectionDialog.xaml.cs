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
using static System.Runtime.InteropServices.JavaScript.JSType;

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

        private static uint Djb2(string str)
        {
            var hash = 5381;
            for (var i = 0; i < str.Length; i++)
            {
                hash = ((hash << 5) + hash) + str[i];
            }
            return (uint)hash;
        }

        private static string HashStringToColor(string str)
        {
            var hash = Djb2(str);
            var r = (hash & 0xFF0000) >> 16;
            var g = (hash & 0x00FF00) >> 8;
            var b = hash & 0x0000FF;
            string rHex = r.ToString("X2");
            string gHex = g.ToString("X2");
            string bHex = b.ToString("X2");
            return "#" + rHex + gHex + bHex;
        }

        private static bool DarkOrLightColor(string hexColor)
        {
            var r = Convert.ToByte(hexColor.Substring(1, 2), 16);
            var g = Convert.ToByte(hexColor.Substring(3, 2), 16);
            var b = Convert.ToByte(hexColor.Substring(5, 2), 16);
            var brightness = (r * 299 + g * 587 + b * 114) / 1000;
            return brightness < 128;
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
                    var color = HashStringToColor(tag);
                    var border = new Border
                    {
                        Background = new SolidColorBrush(new Windows.UI.Color
                        {
                            A = 255,
                            R = (byte)Convert.ToByte(color.Substring(1, 2), 16),
                            G = (byte)Convert.ToByte(color.Substring(3, 2), 16),
                            B = (byte)Convert.ToByte(color.Substring(5, 2), 16)
                        }),
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(8, 4, 8, 4),
                        Margin = new Thickness(0, 0, 8, 8),
                        Child = new TextBlock { 
                            Text = tag, FontSize = 14, 
                            Foreground = new SolidColorBrush(DarkOrLightColor(color) ? Colors.White : Colors.Black)
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
