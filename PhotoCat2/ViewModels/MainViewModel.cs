using PhotoCat2.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhotoCat2.ViewModels
{

    public class MainViewModel
    {
        public ObservableCollection<ImageModel> Items { get; } = new ObservableCollection<ImageModel>();

        public MainViewModel()
        {
            for (int i = 1; i < 20; i++)
            {
                Items.Add(new ImageModel($"Item {i}"));
            }
        }
    }
}
