// Version: 0.1.0
#nullable enable
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace MediaOrganizeViewer
{
    public partial class TextInputDialog : Window, INotifyPropertyChanged
    {
        private string _dialogTitle = string.Empty;
        private string _promptMessage = string.Empty;
        private string _inputText = string.Empty;
        private string _placeholderText = string.Empty;

        public TextInputDialog(string title, string promptMessage, string defaultText = "", string placeholder = "")
        {
            InitializeComponent();
            DataContext = this;
            DialogTitle = title;
            PromptMessage = promptMessage;
            InputText = defaultText;
            PlaceholderText = placeholder;
            InputTextBox.Focus();
            InputTextBox.SelectAll();
        }

        public string DialogTitle
        {
            get => _dialogTitle;
            set
            {
                _dialogTitle = value;
                OnPropertyChanged();
            }
        }

        public string PromptMessage
        {
            get => _promptMessage;
            set
            {
                _promptMessage = value;
                OnPropertyChanged();
            }
        }

        public string InputText
        {
            get => _inputText;
            set
            {
                _inputText = value;
                OnPropertyChanged();
            }
        }

        public string PlaceholderText
        {
            get => _placeholderText;
            set
            {
                _placeholderText = value;
                OnPropertyChanged();
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}