using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Shinobu.Helpers;
using System;

namespace Shinobu.Pages;

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
                Frame.Navigate(typeof(ReaderPage), ($"{bookmark.FilePath};{bookmark.PageNumber};{bookmark.Offset.Start};{bookmark.Offset.End}"));
            }
        }
    }


    private void NoteEditor_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox && textBox.Tag is Bookmark bookmark)
        {
            if (bookmark == null || App.BookmarksManager == null)
                return;
            _ = App.BookmarksManager!.UpdateBookmarkNoteAsync(bookmark, textBox.Text);
        }

    }
}
