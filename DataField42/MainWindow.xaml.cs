using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace DataField42
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
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
}
