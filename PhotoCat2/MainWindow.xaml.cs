using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PhotoCat2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Grid_DragEnter(object sender, DragEventArgs e)
        {
            var f = GetFirstOrDefaultTargetFile(e);
            if (f != null)
            {
                DndGuide.Visibility = Visibility.Visible;
            }
        }

        private void Grid_DragLeave(object sender, DragEventArgs e)
        {
            if (DndGuide.Visibility == Visibility.Visible) { DndGuide.Visibility = Visibility.Collapsed; }
        }

        private void Grid_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.None;

            var f = GetFirstOrDefaultTargetFile(e);
            if (f != null)
            {
                e.Effects = DragDropEffects.Copy | DragDropEffects.Move;
                e.Handled = true;
            }
        }

        private static string GetFirstOrDefaultTargetFile(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                return files?.FirstOrDefault(s => s.EndsWith(".jpg", StringComparison.CurrentCultureIgnoreCase));

            }
            return null;
        }

        private void Grid_Drop(object sender, DragEventArgs e)
        {
            var f = GetFirstOrDefaultTargetFile(e);
            if (f != null)
            {
                Debug.WriteLine("drop: " + f);
                if (DndGuide.Visibility == Visibility.Visible) { DndGuide.Visibility = Visibility.Collapsed; }
            }
        }


    }
}
