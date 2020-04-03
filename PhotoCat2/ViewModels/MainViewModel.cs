using PhotoCat2.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace PhotoCat2.ViewModels
{

    public class MainViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<ImageModel> Items { get; } = new ObservableCollection<ImageModel>();

        private int _TotalImages = 0;
        public int TotalImages
        {
            get { return _TotalImages; }
            set
            {
                if (value != _TotalImages)
                {
                    _TotalImages = value;
                    Notify(nameof(TotalImages));
                    Notify(nameof(LoadingProgress));
                }
            }
        }


        private int _LoadedImagesCount = 0;
        public int LoadedImagesCount
        {
            get { return _LoadedImagesCount; }
            set
            {
                if (value != _LoadedImagesCount)
                {
                    _LoadedImagesCount = value;
                    Notify(nameof(LoadedImagesCount));
                    Notify(nameof(LoadingProgress));
                }
            }
        }

        private bool _IsLoading = false;
        public bool IsLoading
        {
            get { return _IsLoading; }
            set
            {
                if (value != _IsLoading)
                {
                    _IsLoading = value;
                    Notify(nameof(IsLoading));
                    Notify(nameof(IsLoadingInfoVisible));
                }
            }
        }

        public Visibility IsLoadingInfoVisible
        {
            get { return IsLoading ? Visibility.Visible : Visibility.Collapsed; }
        }

        public double LoadingProgress
        {
            get
            {
                if (TotalImages == 0) { return 0; }
                return (double)LoadedImagesCount / (double)TotalImages;
            }
        }

        public MainViewModel()
        {
            Items.CollectionChanged += Items_CollectionChanged;
        }

        private void Items_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
        }

        public event PropertyChangedEventHandler PropertyChanged;

        void Notify(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        void ItemLoadCompleted(ImageModel loaded)
        {
            var startIndex = Items.IndexOf(loaded) + 1;
            var num = Items.Count - startIndex + 1;

            TotalImages = num;
            LoadedImagesCount = 0;

            Task.Run(() =>
            {
                StartPrefetch(startIndex, num);
            });
        }

        void ItemPrefetchCompleted(ImageModel loaded)
        {
            LoadedImagesCount++;
        }

        public void StartPrefetch(int startIndex, int num)
        {
            // todo: Accept cancel operation using CancellationToken.
            // todo: Limit number of loaded/decoded images and dispose unnecessary images

            for (int i = 0; i < num; i++)
            {
                var item = Items[i];

                // Load sequentially,
                item.Load();

                // Decode parallelly.
                ThreadPool.QueueUserWorkItem((a) =>
                {
                    item.Decode();
                });

            }
        }


        public void AddItem(ImageModel item)
        {
            item.LoadFinished += ItemLoadCompleted;
            item.PrefetchFinished += ItemPrefetchCompleted;
            Items.Add(item);
        }
    }
}
