using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Shinobu.Helpers;
using System;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Shinobu.Pages;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class BookmarksPage : Page
{
    public BookmarksPage()
    {
        InitializeComponent();
        Loaded += BookmarksPage_Loaded;
    }

    private void BookmarksPage_Loaded(object sender, RoutedEventArgs e)
    {
        BookmarksListView.ItemsSource = App.BookmarksManager?.Bookmarks;
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is Bookmark bookmark)
        {
            if (bookmark == null || App.BookmarksManager == null)
                return;
            await App.BookmarksManager!.RemoveBookmarkAsync(bookmark);
        }
    }

    private async void GoToButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is Bookmark bookmark)
        {
            if (App.MainWindowInstance is MainWindow)
            {
                Frame.Navigate(typeof(ReaderPage), (bookmark.FilePath+";"+bookmark.PageNumber));
            }
        }
    }
}
