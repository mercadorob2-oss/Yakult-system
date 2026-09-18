using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Wpf.RequestSetSearch.ViewModels;

namespace Yakult.Inventory.App.Wpf.RequestSetSearch.Views
{
    public partial class RequestSetSearchView : UserControl
    {
        private readonly RequestSetSearchViewModel _vm;

        /// <summary>Raised when a result card is clicked, so the hosting Form can navigate.</summary>
        public event Action<RequestSetSearchResultDto> CardSelected;

        public RequestSetSearchView()
        {
            InitializeComponent();

            _vm = new RequestSetSearchViewModel();
            _vm.CardSelected += dto => CardSelected?.Invoke(dto);
            DataContext = _vm;

            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            LoadLogo();
            CenterSearchBox.Focus();
        }

        private void LoadLogo()
        {
            var logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", "yakult_Name.png");
            if (!File.Exists(logoPath)) return;
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(logoPath, UriKind.Absolute);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                LogoImage.Source = bmp;
            }
            catch { }
        }

        private void CenterSearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            _vm.ExecuteSearch();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            _vm.Reset();
            CenterSearchBox.Focus();
        }
    }
}
