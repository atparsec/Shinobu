using Microsoft.UI.Xaml.Controls;

namespace Shinobu.Dialogs
{
    public sealed partial class ColorSelectDialog : UserControl
    {
        private string _colorSelectText = "Select Color";
        public string ColorSelectText { 
            get => _colorSelectText;
            set
            {
                _colorSelectText = value;
                ColorSelectLabel.Text = value;

            }
        }
        public ColorSelectDialog()
        {
            InitializeComponent();
        }

        public Windows.UI.Color SelectedColor => ColorPicker.Color;
    }
}
