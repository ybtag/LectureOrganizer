using System.Windows;

namespace LectureOrganizerWPF
{
    public partial class RenameInputDialog : Window
    {
        public string NewFileName { get; private set; }

        public RenameInputDialog(string currentName)
        {
            InitializeComponent();
            FileNameTextBox.Text = currentName;
            FileNameTextBox.SelectAll();
            FileNameTextBox.Focus();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            NewFileName = FileNameTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(NewFileName))
            {
                MessageBox.Show("File name cannot be empty.", "Invalid Name", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
