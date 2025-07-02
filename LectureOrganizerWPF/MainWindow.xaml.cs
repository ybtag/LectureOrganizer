using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

// Add reference to Windows Forms for FolderBrowserDialog
[assembly: System.Runtime.Versioning.SupportedOSPlatform("windows")]

namespace LectureOrganizerWPF
{
    public class FileItem
    {
        public string FilePath { get; set; }
        public string Name { get; set; }
        public int Number { get; set; }
        public bool IsPlaceholder { get; set; } = false;
        public FileItem(string filePath)
        {
            FilePath = filePath;
            Name = Path.GetFileName(filePath);
            var match = Regex.Match(Name, @"(\d+)\.");
            Number = match.Success ? int.Parse(match.Groups[1].Value) : -1;
        }
        public FileItem(bool isPlaceholder)
        {
            IsPlaceholder = isPlaceholder;
            Name = "";
        }
    }

    // Drop indicator adorner
    public class DropIndicatorAdorner : Adorner
    {
        private int _insertIndex;
        private ListBox _listBox;
        public DropIndicatorAdorner(ListBox adornedElement, int insertIndex) : base(adornedElement)
        {
            _listBox = adornedElement;
            _insertIndex = insertIndex;
            IsHitTestVisible = false;
        }
        public void UpdateIndex(int insertIndex)
        {
            _insertIndex = insertIndex;
            InvalidateVisual();
        }
        protected override void OnRender(DrawingContext dc)
        {
            if (_listBox.Items.Count == 0) return;
            var itemCount = _listBox.Items.Count;
            var container = _insertIndex < itemCount ? _listBox.ItemContainerGenerator.ContainerFromIndex(_insertIndex) as FrameworkElement : null;
            double y = 0;
            double height = 0;
            if (container != null)
            {
                var topLeft = container.TransformToAncestor(_listBox).Transform(new Point(0, 0));
                y = topLeft.Y;
                height = container.ActualHeight;
            }
            else // after last item
            {
                var last = _listBox.ItemContainerGenerator.ContainerFromIndex(itemCount - 1) as FrameworkElement;
                if (last == null) return;
                var bottomLeft = last.TransformToAncestor(_listBox).Transform(new Point(0, last.ActualHeight));
                y = bottomLeft.Y;
                height = last.ActualHeight;
            }
            Rect rect = new Rect(0, y, _listBox.ActualWidth, height);
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(80, 128, 128, 128)), null, rect);
        }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private ObservableCollection<FileItem> files = new ObservableCollection<FileItem>();
        private Point _dragStartPoint;
        private string _currentFolder;
        private int _minNumber = 1;
        private DropIndicatorAdorner _dropAdorner;
        private int _lastDropIndex = -1;
        private FileItem _placeholderItem = new FileItem(true);

        public MainWindow()
        {
            InitializeComponent();
            FilesListBox.ItemsSource = files;
            FilesListBox.AllowDrop = true;
            FilesListBox.DragOver += FilesListBox_DragOver;
            FilesListBox.DragLeave += FilesListBox_DragLeave;
        }

        private void SelectFolder_Click(object sender, RoutedEventArgs e)
        {
            // Use OpenFileDialog as a fallback for folder selection
            var dialog = new OpenFileDialog
            {
                CheckFileExists = false,
                CheckPathExists = true,
                FileName = "Select Folder"
            };
            if (dialog.ShowDialog() == true)
            {
                string folderPath = Path.GetDirectoryName(dialog.FileName);
                FolderPathTextBox.Text = folderPath;
                _currentFolder = folderPath;
                LoadFiles(folderPath);
            }
        }

        private void LoadFiles(string folderPath)
        {
            files.Clear();
            _minNumber = int.MaxValue;
            if (Directory.Exists(folderPath))
            {
                foreach (var file in Directory.GetFiles(folderPath))
                {
                    var item = new FileItem(file);
                    files.Add(item);
                    if (item.Number != -1 && item.Number < _minNumber)
                        _minNumber = item.Number;
                }
                if (_minNumber == int.MaxValue) _minNumber = 1;
            }
        }

        private void FilesListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
        }

        private void FilesListBox_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point position = e.GetPosition(null);
                if (Math.Abs(position.X - _dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(position.Y - _dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    if (FilesListBox.SelectedItem != null)
                    {
                        DragDrop.DoDragDrop(FilesListBox, FilesListBox.SelectedItem, DragDropEffects.Move);
                    }
                }
            }
        }

        private int GetDropIndex(Point position)
        {
            for (int i = 0; i < FilesListBox.Items.Count; i++)
            {
                var item = FilesListBox.ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement;
                if (item != null)
                {
                    var bounds = VisualTreeHelper.GetDescendantBounds(item);
                    var topLeft = item.TransformToAncestor(FilesListBox).Transform(new Point(0, 0));
                    double itemMiddle = topLeft.Y + bounds.Height / 2;
                    if (position.Y < itemMiddle)
                        return i;
                }
            }
            return FilesListBox.Items.Count;
        }

        private void FilesListBox_DragOver(object sender, DragEventArgs e)
        {
            Point pos = e.GetPosition(FilesListBox);
            int insertIndex = GetDropIndex(pos);
            // Insert placeholder
            if (!files.Contains(_placeholderItem))
            {
                files.Insert(insertIndex, _placeholderItem);
            }
            else
            {
                int currentIndex = files.IndexOf(_placeholderItem);
                if (currentIndex != insertIndex)
                {
                    // Fix: Ensure insertIndex is within valid range for Move
                    int targetIndex = insertIndex;
                    if (insertIndex >= files.Count)
                    {
                        targetIndex = files.Count - 1;
                    }
                    if (currentIndex != targetIndex)
                    {
                        files.Move(currentIndex, targetIndex);
                    }
                }
            }
            // Adorner
            if (_dropAdorner == null)
            {
                var adornerLayer = AdornerLayer.GetAdornerLayer(FilesListBox);
                _dropAdorner = new DropIndicatorAdorner(FilesListBox, insertIndex);
                adornerLayer.Add(_dropAdorner);
            }
            else
            {
                _dropAdorner.UpdateIndex(insertIndex);
            }
            _lastDropIndex = insertIndex;
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }

        private void FilesListBox_DragLeave(object sender, DragEventArgs e)
        {
            RemoveDropAdorner();
            RemovePlaceholder();
        }

        private void FilesListBox_Drop(object sender, DragEventArgs e)
        {
            RemoveDropAdorner();
            if (files.Contains(_placeholderItem))
            {
                files.Remove(_placeholderItem);
            }
            if (e.Data.GetDataPresent(typeof(FileItem)))
            {
                var droppedData = e.Data.GetData(typeof(FileItem)) as FileItem;
                int oldIndex = files.IndexOf(droppedData);
                int newIndex = _lastDropIndex;
                // Allow moving to the end of the list
                if (newIndex > files.Count)
                {
                    newIndex = files.Count;
                }
                if (oldIndex != -1 && newIndex != -1 && oldIndex != newIndex)
                {
                    // If moving to the end, just remove and add
                    if (newIndex == files.Count)
                    {
                        files.RemoveAt(oldIndex);
                        files.Add(droppedData);
                    }
                    else
                    {
                        files.Move(oldIndex, newIndex > oldIndex ? newIndex - 1 : newIndex);
                    }
                    RenumberFilesInMemory();
                }
            }
        }

        private void RemoveDropAdorner()
        {
            if (_dropAdorner != null)
            {
                var adornerLayer = AdornerLayer.GetAdornerLayer(FilesListBox);
                adornerLayer.Remove(_dropAdorner);
                _dropAdorner = null;
            }
        }

        private void RemovePlaceholder()
        {
            if (files.Contains(_placeholderItem))
            {
                files.Remove(_placeholderItem);
            }
        }

        private void RenumberFilesInMemory()
        {
            int displayIndex = 0;
            for (int i = 0; i < files.Count; i++)
            {
                var file = files[i];
                if (file.IsPlaceholder) continue;
                int newNumber = _minNumber + displayIndex;
                file.Number = newNumber;
                // Update Name in memory to reflect new number
                var match = Regex.Match(file.Name, @"^(\d+)(\.)(.*)$");
                if (match.Success)
                {
                    file.Name = $"{newNumber}{match.Groups[2].Value}{match.Groups[3].Value}";
                }
                displayIndex++;
            }
            FilesListBox.Items.Refresh();
        }

        private async void ShowToastNotification()
        {
            ToastNotification.Visibility = Visibility.Visible;
            var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(200)));
            ToastNotification.BeginAnimation(OpacityProperty, fadeIn);
            await Task.Delay(2000);
            var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(400)));
            fadeOut.Completed += (s, e) => ToastNotification.Visibility = Visibility.Collapsed;
            ToastNotification.BeginAnimation(OpacityProperty, fadeOut);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var file in files)
            {
                if (file.IsPlaceholder) continue;
                var match = Regex.Match(file.Name, @"^(\d+)(\.)(.*)$");
                if (match.Success)
                {
                    string newName = $"{file.Number}{match.Groups[2].Value}{match.Groups[3].Value}";
                    string newPath = Path.Combine(_currentFolder, newName);
                    if (!string.Equals(file.FilePath, newPath, StringComparison.OrdinalIgnoreCase))
                    {
                        File.Move(file.FilePath, newPath);
                        file.FilePath = newPath;
                        file.Name = newName;
                    }
                }
            }
            LoadFiles(_currentFolder);
            ShowToastNotification();
        }

        private void RenameMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (FilesListBox.SelectedItem is FileItem file && !file.IsPlaceholder)
            {
                // Prompt for new name (simple InputBox style)
                var inputDialog = new RenameInputDialog(file.Name);
                if (inputDialog.ShowDialog() == true)
                {
                    string newName = inputDialog.NewFileName;
                    if (!string.IsNullOrWhiteSpace(newName) && newName != file.Name)
                    {
                        string newPath = Path.Combine(_currentFolder, newName);
                        try
                        {
                            File.Move(file.FilePath, newPath);
                            file.FilePath = newPath;
                            file.Name = newName;
                            FilesListBox.Items.Refresh();
                            ShowToastNotification();
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Rename failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
        }
    }
}