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

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Shinobu
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SettingsPage : Page, ISearchProvider
    {
        private readonly List<string> _settingsItems =
        [
            "General",
            "Appearance",
            "AI Features",
            "About",
            "Updates"
        ];

        public SettingsPage()
        {
            InitializeComponent();
        }

        public void OnQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            string query = args.QueryText ?? string.Empty;
            var match = _settingsItems.FirstOrDefault(s => s.Equals(query, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                var dlg = new ContentDialog()
                {
                    Title = "Found",
                    Content = $"Found setting: {match}",
                    CloseButtonText = "OK"
                };
                _ = dlg.ShowAsync();
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
                // todo
            }
            else
            {
                sender.ItemsSource = null;
            }
        }
    }
}
