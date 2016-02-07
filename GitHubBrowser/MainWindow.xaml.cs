using MahApps.Metro.Controls;
using Octokit;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace GitUserBrowser
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow, IPasswordProvider
    {
        private MainWindowModel model;
        private MainWindowController controller;

        private const int VisibleUsersInGrid = 11;

        public MainWindow()
        {
            model = new MainWindowModel();
            controller = new MainWindowController(model, this, VisibleUsersInGrid);

            DataContext = model;
            InitializeComponent();
        }

        private async void srchButton_Click(object sender, RoutedEventArgs e)
        {
            await controller.SearchAsync();
            await controller.LoadVisibleUserDataAsync(0);
        }

        public string GetPassword()
        {
            return tbUserPass.Password;
        }

        private void listView_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            UpdateSelectedItems();
        }

        private void listView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateSelectedItems();
        }

        private void listView_PreviewTouchUp(object sender, TouchEventArgs e)
        {
            UpdateSelectedItems();
        }

        private void listView_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            UpdateSelectedItems();
        }

        private async void UpdateSelectedItems()
        {
            List<ScrollViewer> svList = GetVisualChildCollection<ScrollViewer>(listView);
            await controller.LoadVisibleUserDataAsync((int)svList[0].VerticalOffset);
        }
        private static List<T> GetVisualChildCollection<T>(object parent) where T : Visual
        {
            List<T> visualCollection = new List<T>();
            GetVisualChildCollection(parent as DependencyObject, visualCollection);
            return visualCollection;
        }
        private static void GetVisualChildCollection<T>(DependencyObject parent, List<T> visualCollection) where T : Visual
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child is T)
                {
                    visualCollection.Add(child as T);
                }
                else if (child != null)
                {
                    GetVisualChildCollection(child, visualCollection);
                }
            }
        }

        private async void export_To_CSV_Click(object sender, RoutedEventArgs e)
        {
            Button b = (Button)sender;
            b.IsEnabled = false;

            try
            {
                await controller.ExportResultsToCSVAsync();
            }
            finally
            {
                b.IsEnabled = true;
            }
            
        }
    }
}
