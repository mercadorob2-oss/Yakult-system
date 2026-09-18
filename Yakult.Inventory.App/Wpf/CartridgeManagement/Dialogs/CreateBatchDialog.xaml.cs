using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using Yakult.Inventory.App.Core;

namespace Yakult.Inventory.App.WPF.CartridgeManagement.Dialogs
{
    public partial class CreateBatchDialog : Window
    {
        public bool   Confirmed          { get; private set; }
        public int    SelectedVendorId   { get; private set; }
        public string SelectedVendorName { get; private set; }
        public List<int>    SelectedModelIds   { get; private set; } = new List<int>();
        public List<string> SelectedModelNames { get; private set; } = new List<string>();
        public string Remarks { get; private set; }

        private readonly List<VendorItem>  _vendors = new List<VendorItem>();
        private readonly ObservableCollection<ModelItem> _models = new ObservableCollection<ModelItem>();

        public CreateBatchDialog()
        {
            InitializeComponent();
            ModelsList.ItemsSource = _models;
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            LoadData();
        }

        private void LoadData()
        {
            try
            {
                using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
                {
                    con.Open();

                    const string vendorSql = @"
                        SELECT v.VendorID, v.VendorName
                        FROM dbo.Vendor v
                        LEFT JOIN dbo.ArchiveStatus arc
                            ON arc.EntityType = 'Vendor' AND arc.EntityId = v.VendorID AND arc.IsArchived = 1
                        WHERE v.IsActive = 1 AND v.IsRefiller = 1 AND arc.ArchiveId IS NULL
                        ORDER BY v.VendorName";

                    using (var cmd = new SqlCommand(vendorSql, con))
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                            _vendors.Add(new VendorItem { VendorId = rdr.GetInt32(0), VendorName = rdr.GetString(1) });
                    }

                    const string modelSql = "SELECT CartridgeModelId, ModelNumber FROM dbo.CartridgeModel WHERE IsActive = 1 ORDER BY ModelNumber";
                    using (var cmd = new SqlCommand(modelSql, con))
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                            _models.Add(new ModelItem { CartridgeModelId = rdr.GetInt32(0), ModelNumber = rdr.GetString(1) });
                    }
                }

                CmbVendor.ItemsSource       = _vendors;
                CmbVendor.DisplayMemberPath = "VendorName";
                if (_vendors.Count > 0) CmbVendor.SelectedIndex = 0;

                if (_vendors.Count == 0)
                {
                    MessageBox.Show("No active refill vendors found.", "No Vendors", MessageBoxButton.OK, MessageBoxImage.Warning);
                    DialogResult = false;
                    return;
                }
                if (_models.Count == 0)
                {
                    MessageBox.Show("No active cartridge models found.", "No Models", MessageBoxButton.OK, MessageBoxImage.Warning);
                    DialogResult = false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading data:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.LogError("CreateBatchDialog.LoadData failed", ex);
                DialogResult = false;
            }
        }

        private void BtnCreate_Click(object sender, RoutedEventArgs e)
        {
            var vendor = CmbVendor.SelectedItem as VendorItem;
            if (vendor == null)
            {
                MessageBox.Show("Please select a vendor.", "Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selected = _models.Where(m => m.IsSelected).ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("Please select at least one cartridge model.", "Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SelectedVendorId   = vendor.VendorId;
            SelectedVendorName = vendor.VendorName;
            SelectedModelIds   = selected.Select(m => m.CartridgeModelId).ToList();
            SelectedModelNames = selected.Select(m => m.ModelNumber).ToList();
            Remarks            = TxtRemarks.Text.Trim();
            Confirmed          = true;
            DialogResult       = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private class VendorItem
        {
            public int    VendorId   { get; set; }
            public string VendorName { get; set; }
        }

        public class ModelItem : INotifyPropertyChanged
        {
            public int    CartridgeModelId { get; set; }
            public string ModelNumber      { get; set; }

            private bool _isSelected;
            public bool IsSelected
            {
                get => _isSelected;
                set
                {
                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
        }
    }
}
