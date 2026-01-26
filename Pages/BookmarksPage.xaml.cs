using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Shinobu.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;

namespace Shinobu.Pages;

public sealed partial class BookmarksPage : Page, ISearchProvider, INotifyPropertyChanged

{
    private bool _isBookmarksEmpty;

    public bool IsBookmarksEmpty
    {
        get => _isBookmarksEmpty;
        set
        {
            if (_isBookmarksEmpty != value)
            {
                _isBookmarksEmpty = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsBookmarksEmpty)));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public BookmarksPage()
    {
        InitializeComponent();
        Loaded += BookmarksPage_Loaded;
    }

    public void OnQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion is Bookmark bookmark)
        {
            ScrollToSection(bookmark);
        }
        else
        {
            var suggestions = App.BookmarksManager?.BookmarksSearch(sender.Text);
            sender.ItemsSource = suggestions;
        }
    }

    private void ScrollToSection(Bookmark bookmark)
    {
        foreach (var item in BookmarksListView.Items)
        {
            if (item is Bookmark bm && bm.Id == bookmark.Id)
            {
                BookmarksListView.ScrollIntoView(item);
                var container = BookmarksListView.ContainerFromItem(item) as ListViewItem;
                if (container != null)
                {
                    var expander = container.FindDescendant<Expander>();
                    if (expander != null)
                    {
                        expander.IsExpanded = true;
                    }
                }
                break;
            }
        }
    }

    public void OnTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            var suggestions = App.BookmarksManager?.BookmarksSearch(sender.Text);
            sender.ItemsSource = suggestions;
        }
    }

    private void BookmarksPage_Loaded(object sender, RoutedEventArgs e)
    {
        var bookmarks = App.BookmarksManager?.Bookmarks;
        BookmarksListView.ItemsSource = bookmarks;
        if (bookmarks is INotifyCollectionChanged notify)
        {
            notify.CollectionChanged += OnBookmarksChanged;
        }
        UpdateEmptyState();
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

    private void OnBookmarksChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateEmptyState();
    }

    private void UpdateEmptyState()
    {
        IsBookmarksEmpty = !(BookmarksListView.ItemsSource as System.Collections.IEnumerable)?.Cast<object>().Any() ?? true;
    }
}
