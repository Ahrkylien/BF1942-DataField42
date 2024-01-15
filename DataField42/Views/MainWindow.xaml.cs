using System.Windows;
using System.Windows.Input;

namespace DataField42.Views;
public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    //[DllImport("user32.dll")]
    //public static extern IntPtr SendMessage(IntPtr hWnd, int wMsg, int wParam, int lParam);

    private void ControlBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
            ToggleWindowState();
        else
        {
            DragMove();
            //var helper = new WindowInteropHelper(this);
            //SendMessage(helper.Handle, 161, 2, 0);
        }
    }

    private void ControlBar_MouseEnter(object sender, MouseEventArgs e)
    {
        //MaxHeight = SystemParameters.MaximizedPrimaryScreenHeight;
    }
        
    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeButton_Click(object sender, RoutedEventArgs e) => ToggleWindowState();

    private void ToggleWindowState() => WindowState = WindowState == WindowState.Normal ? WindowState.Maximized : WindowState.Normal;
}