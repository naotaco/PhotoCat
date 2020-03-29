using PhotoCat2.Models;
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
        public ObservableCollection<ThumbnailModel> Items { get; } = new ObservableCollection<ThumbnailModel>();

        public MainViewModel()
        {
            for (int i = 1; i < 20; i++)
            {
                Items.Add(new ThumbnailModel($"Item {i}"));
            }
        }
    }
}
