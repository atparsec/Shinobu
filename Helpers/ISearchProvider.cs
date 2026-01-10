using Microsoft.UI.Xaml.Controls;

namespace Shinobu.Helpers
{
    public interface ISearchProvider
    {
        void OnQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args);
        void OnTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args);
    }
}
