using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Windows.Storage;

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

            SelectedWordText.Text = selectedText;

            ExplainTextBox.Text = "Explain: " + selectedText;

            var settings = ApplicationData.Current.LocalSettings;
            var aiProvider = settings.Values.TryGetValue("AIProvider", out var p) && p is string s ? s : "";
            AINavViewItem.Content = aiProvider;

            var aiEnabled = settings.Values.TryGetValue("AIEnabled", out var enabled) && enabled is bool b && b;
            AINavViewItem.Visibility = aiEnabled ? Visibility.Visible : Visibility.Collapsed;

            MainNavigationView.SelectedItem = MainNavigationView.MenuItems[0];

        }

        private void MainNavigationView_SelectionChanged(
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

    }
}
