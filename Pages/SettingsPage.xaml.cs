using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Shinobu.Pages
{
    public sealed partial class SettingsPage : Page, ISearchProvider
    {
        private readonly List<string> _settingsItems = new()
        {
            "General",
            "Appearance",
            "AI Features",
            "About",
            "Updates"
        };

        private ApplicationDataContainer _localSettings = ApplicationData.Current.LocalSettings;

        public SettingsPage()
        {
            InitializeComponent();
            LoadSettingsToUi();
        }

        private void LoadSettingsToUi()
        {
            // Theme default: System
            var theme = _localSettings.Values.TryGetValue("Theme", out var t) ? t as string : "System";
            if (theme != null)
            {
                switch (theme)
                {
                    case "Light":
                        ThemeComboBox.SelectedIndex = 0;
                        break;
                    case "Dark":
                        ThemeComboBox.SelectedIndex = 1;
                        break;
                    default:
                        ThemeComboBox.SelectedIndex = 2;
                        break;
                }
            }

            // AI Features
            var aiEnabled = _localSettings.Values.TryGetValue("AIEnabled", out var a) && a is bool ab && ab;
            EnableAIFeaturesToggle.IsOn = aiEnabled;
            AIProviderComboBox.IsEnabled = aiEnabled;
            ApiKeyBox.IsEnabled = aiEnabled;

            var provider = _localSettings.Values.TryGetValue("AIProvider", out var p) ? p as string : "Grok";
            if (provider != null)
            {
                AIProviderComboBox.SelectedIndex = provider == "OpenAI" ? 1 : 0;
            }

            ApiKeyBox.Password = _localSettings.Values.TryGetValue("ApiKey", out var k) ? k as string ?? string.Empty : string.Empty;

            // Dictionary
            var dict = _localSettings.Values.TryGetValue("Dictionary", out var d) ? d as string : "Local";
            DictionaryComboBox.SelectedIndex = dict == "Jisho" ? 1 : 0;

            // LibraryFolder
            var libraryPath = _localSettings.Values.TryGetValue("LibraryFolder", out var lf) ? lf as string : null;
            if (string.IsNullOrEmpty(libraryPath))
            {
                libraryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            }
            LibraryFolderTextBox.Text = libraryPath;
        }

        private void SaveSettings()
        {
            var theme = ThemeComboBox.SelectedItem is ComboBoxItem ci && ci.Content is string s ? s : "System";
            _localSettings.Values["Theme"] = theme;

            _localSettings.Values["AIEnabled"] = EnableAIFeaturesToggle.IsOn;

            var provider = AIProviderComboBox.SelectedItem is ComboBoxItem pic && pic.Content is string ps ? ps : "Grok";
            _localSettings.Values["AIProvider"] = provider;

            _localSettings.Values["ApiKey"] = ApiKeyBox.Password;

            var dict = DictionaryComboBox.SelectedItem is ComboBoxItem dic && dic.Content is string ds ? ds : "Local";
            _localSettings.Values["Dictionary"] = dict;

            _localSettings.Values["LibraryFolder"] = LibraryFolderTextBox.Text;
        }

        public void OnQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            string query = args.QueryText ?? string.Empty;
            var match = _settingsItems.FirstOrDefault(s => s.Equals(query, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                ScrollToSection(match);
            }
            else
            {
                var dlg = new ContentDialog()
                {
                    Title = "Search",
                    Content = $"No exact match for: {query}",
                    CloseButtonText = "OK"
                };
                _ = dlg.ShowAsync();
            }
        }

        public void OnTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                string text = sender.Text ?? string.Empty;
                var suggestions = _settingsItems
                    .Where(s => s.Contains(text, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                sender.ItemsSource = suggestions;
            }
            else if (args.Reason == AutoSuggestionBoxTextChangeReason.SuggestionChosen)
            {
                var chosen = sender.Text ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(chosen))
                {
                    ScrollToSection(chosen);
                }
            }
            else
            {
                sender.ItemsSource = null;
            }
        }

        private void ScrollToSection(string section)
        {
            FrameworkElement? target = section switch
            {
                "General" => this.FindName("GeneralSection") as FrameworkElement,
                "Appearance" => this.FindName("AppearanceSection") as FrameworkElement,
                "AI Features" => this.FindName("AIFeaturesSection") as FrameworkElement,
                "About" => this.FindName("AboutSection") as FrameworkElement,
                "Updates" => this.FindName("UpdatesSection") as FrameworkElement,
                _ => null
            };

            if (target is not null && this.FindName("RootScrollViewer") is ScrollViewer sv)
            {
                try
                {
                    var point = target.TransformToVisual(sv).TransformPoint(new Windows.Foundation.Point(0, 0));
                    _ = sv.ChangeView(null, point.Y, null);
                }
                catch
                {
                }
            }
        }

        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ThemeComboBox.SelectedItem is ComboBoxItem ci && ci.Content is string s)
            {
                ElementTheme requestedTheme = s switch
                {
                    "Light" => ElementTheme.Light,
                    "Dark" => ElementTheme.Dark,
                    _ => ElementTheme.Default
                };

                if (App.MainWindowInstance is Window w && w.Content is FrameworkElement fe)
                {
                    fe.RequestedTheme = requestedTheme;
                }

                SaveSettings();
            }
        }

        private void EnableAIFeaturesToggle_Toggled(object sender, RoutedEventArgs e)
        {
            var enabled = EnableAIFeaturesToggle.IsOn;
            AIProviderComboBox.IsEnabled = enabled;
            ApiKeyBox.IsEnabled = enabled;

            SaveSettings();
        }

        private void AIProviderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SaveSettings();
        }

        private void ApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            SaveSettings();
        }

        private void DictionaryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SaveSettings();
        }

        private void LibraryFolderTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SaveSettings();
        }

        private async void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var folderPicker = new FolderPicker();
            var hwnd = WindowNative.GetWindowHandle(App.MainWindowInstance);
            InitializeWithWindow.Initialize(folderPicker, hwnd);
            folderPicker.SuggestedStartLocation = PickerLocationId.Downloads;
            folderPicker.FileTypeFilter.Add("*");

            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null)
            {
                LibraryFolderTextBox.Text = folder.Path;
                _localSettings.Values["LibraryFolder"] = folder.Path;
            }
        }
    }
}
