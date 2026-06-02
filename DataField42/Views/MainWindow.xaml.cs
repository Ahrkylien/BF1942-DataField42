using System.Windows;
using System.Windows.Input;

namespace DataField42.Views;

public partial class MainWindow : Window
{
    public MainWindow(ViewModels.MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void ControlBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
            ToggleWindowState();
        else
            DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeButton_Click(object sender, RoutedEventArgs e) => ToggleWindowState();

    private void ToggleWindowState() => WindowState = WindowState == WindowState.Normal ? WindowState.Maximized : WindowState.Normal;
}
