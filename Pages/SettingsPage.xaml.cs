using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Popups;
using WinRT.Interop;

namespace Shinobu.Pages
{
    public sealed partial class SettingsPage : Page, ISearchProvider
    {
        private readonly List<string> _settingSections =
        [
            "General",
            "Appearance",
            "AI Features",
            "About",
            "Updates"
        ];

        private static class SettingKeys
        {
            public const string Theme = "Theme";
            public const string AIEnabled = "AIEnabled";
            public const string AIProvider = "AIProvider";
            public const string ApiKey = "ApiKey";
            public const string Dictionary = "Dictionary";
            public const string LibraryFolder = "LibraryFolder";
        }

        private readonly ApplicationDataContainer _localSettings = ApplicationData.Current.LocalSettings;
        private bool _isLoading = false;

        public SettingsPage()
        {
            InitializeComponent();
            
            Loaded += (s, e) => LoadSettingsToUi();

        }

        private void LoadSettingsToUi()
        {
            _isLoading = true;
           
            // Theme
            var theme = _localSettings.Values.TryGetValue(SettingKeys.Theme, out var t) ? t as string : null;
            if (theme != null)
            {
                ThemeComboBox.SelectedIndex = theme switch
                {
                    "Light" => 0,
                    "Dark" => 1,
                    _ => 2
                };
            }

            // AI Features
            if (_localSettings.Values.TryGetValue(SettingKeys.AIEnabled, out var a) && a is bool ab)
            {
                EnableAIFeaturesToggle.IsOn = ab;
            }
            else
            {
                EnableAIFeaturesToggle.IsOn = false;
            }
            AIProviderComboBox.IsEnabled = EnableAIFeaturesToggle.IsOn;
            ApiKeyBox.IsEnabled = EnableAIFeaturesToggle.IsOn;

            // AI Provider
            if (_localSettings.Values.TryGetValue(SettingKeys.AIProvider, out var p) && p is string ps)
            {
                AIProviderComboBox.SelectedIndex = ps switch
                {
                    "Grok" => 0,
                    "OpenAI" => 1,
                    _ => 0
                };
            }
            else
            {
                AIProviderComboBox.SelectedIndex = 0;
            }

            ApiKeyBox.Password = _localSettings.Values.TryGetValue(SettingKeys.ApiKey, out var k) ? k as string ?? string.Empty : string.Empty;

            // Dictionary
            if (_localSettings.Values.TryGetValue(SettingKeys.Dictionary, out var d) && d is string dict)
            {
                DictionaryComboBox.SelectedIndex = dict == "Jisho" ? 1 : 0;
            }
            else
            {
                DictionaryComboBox.SelectedIndex = 0;
            }

            // LibraryFolder
            if (_localSettings.Values.TryGetValue(SettingKeys.LibraryFolder, out var lf) && lf is string libraryPath)
            {
                LibraryFolderTextBox.Text = libraryPath;
            }
            else
            {
                var defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                LibraryFolderTextBox.Text = defaultPath;
            }

            _isLoading = false;
        }

        private void SaveSettings()
        {
            if (_isLoading) return;

            var theme = ThemeComboBox.SelectedItem is ComboBoxItem ci && ci.Content is string s ? s : "System";
            _localSettings.Values[SettingKeys.Theme] = theme;

            _localSettings.Values[SettingKeys.AIEnabled] = EnableAIFeaturesToggle.IsOn;

            var provider = AIProviderComboBox.SelectedItem is ComboBoxItem pic && pic.Content is string ps ? ps : "Grok";
            _localSettings.Values[SettingKeys.AIProvider] = provider;

            _localSettings.Values[SettingKeys.ApiKey] = ApiKeyBox.Password;

            var dict = DictionaryComboBox.SelectedItem is ComboBoxItem dic && dic.Content is string ds ? ds : "Local";
            _localSettings.Values[SettingKeys.Dictionary] = dict;

            _localSettings.Values[SettingKeys.LibraryFolder] = LibraryFolderTextBox.Text;
        }

        public void OnQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            string query = args.QueryText ?? string.Empty;
            var match = _settingSections.FirstOrDefault(s => s.Equals(query, StringComparison.OrdinalIgnoreCase));
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
                var suggestions = _settingSections
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
            if (_isLoading) return;

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
            if (_isLoading) return;

            var enabled = EnableAIFeaturesToggle.IsOn;
            AIProviderComboBox.IsEnabled = enabled;
            ApiKeyBox.IsEnabled = enabled;

            SaveSettings();
        }

        private void AIProviderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;

            SaveSettings();
        }

        private void ApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;

            SaveSettings();
        }

        private void DictionaryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;

            SaveSettings();
        }

        private void LibraryFolderTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isLoading) return;

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
                SaveSettings();
            }
        }
    }
}
