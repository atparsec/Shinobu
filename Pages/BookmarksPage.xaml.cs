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
                Frame.Navigate(typeof(ReaderPage), (bookmark.FilePath+";"+bookmark.PageNumber));
            }
        }
    }

    private void NoteView_Tapped(object sender, TappedRoutedEventArgs e)
    {
        var noteView = (FrameworkElement)sender;
        var container = noteView.Parent as FrameworkElement;
        if (container == null) return;

        var noteEditor = container.FindName("NoteEditor") as TextBox;
        if (noteEditor == null) return;

        noteView.Visibility = Visibility.Collapsed;
        noteEditor.Visibility = Visibility.Visible;
        noteEditor.Focus(FocusState.Programmatic);
    }

    private void NoteEditor_LostFocus(object sender, RoutedEventArgs e)
    {
        ExitEditMode(sender);
    }

    private void NoteEditor_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter &&
            !((TextBox)sender).AcceptsReturn)
        {
            ExitEditMode(sender);
        }
    }

    private void ExitEditMode(object sender)
    {
        var noteEditor = (FrameworkElement)sender;
        var container = noteEditor.Parent as FrameworkElement;
        if (container == null) return;

        var noteView = container.FindName("NoteView") as FrameworkElement;
        if (noteView == null) return;

        noteEditor.Visibility = Visibility.Collapsed;
        noteView.Visibility = Visibility.Visible;

        App.BookmarksManager?.UpdateBookmarkNoteAsync(
            (noteEditor.Tag as Bookmark)!,
            (noteEditor as TextBox)!.Text);

        (noteView as TextBlock)!.Text = (noteEditor as TextBox)!.Text;
    }


}
