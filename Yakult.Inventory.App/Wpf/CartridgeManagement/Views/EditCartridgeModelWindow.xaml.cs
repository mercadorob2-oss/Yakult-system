using System;
using System.Windows;
using System.Windows.Controls;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.WPF.CartridgeManagement.Views
{
    public partial class EditCartridgeModelWindow : Window
    {
        private readonly int _cartridgeModelId;
        private CartridgeModelDto _model;

        public EditCartridgeModelWindow(int cartridgeModelId)
        {
            InitializeComponent();
            _cartridgeModelId = cartridgeModelId;
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var repository = new CartridgeModelRepository();
                _model = await repository.GetByIdAsync(_cartridgeModelId);

                if (_model == null)
                {
                    MessageBox.Show("Cartridge model not found.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    DialogResult = false;
                    return;
                }

                TxtModelNumber.Text      = _model.ModelNumber;
                CboCategory.SelectedIndex = 0; // "Cartridge" — this model's current category
                ChkRequestable.IsChecked = _model.IsRequestable;
                ChkRefillable.IsChecked  = _model.IsRefillable;
                ChkActive.IsChecked      = _model.IsActive;

                LoadingText.Visibility = Visibility.Collapsed;
                FormPanel.Visibility   = Visibility.Visible;
                TxtModelNumber.Focus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading cartridge model: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                DialogResult = false;
            }
        }

        private void CboCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TxtCategoryWarning == null) return;
            bool isCartridge = (CboCategory.SelectedItem as ComboBoxItem)?.Content as string == "Cartridge";
            TxtCategoryWarning.Visibility = isCartridge ? Visibility.Collapsed : Visibility.Visible;
            ChkRefillable.IsEnabled = isCartridge;
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtModelNumber.Text))
            {
                MessageBox.Show("Please enter a model number.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtModelNumber.Focus();
                return;
            }

            string modelNumber = TxtModelNumber.Text.Trim();
            string selectedCategory = (CboCategory.SelectedItem as ComboBoxItem)?.Content as string ?? "Cartridge";
            bool isRequestable = ChkRequestable.IsChecked == true;
            bool isActive = ChkActive.IsChecked == true;

            try
            {
                BtnSave.IsEnabled = false;
                BtnSave.Content   = "Saving…";

                if (selectedCategory != "Cartridge")
                {
                    var consumableRepo = new ConsumableModelRepository();
                    var existingConsumable = await consumableRepo.FindByModelNumberAsync(modelNumber, selectedCategory);
                    if (existingConsumable != null)
                    {
                        MessageBox.Show($"Model number \"{modelNumber}\" already exists under {selectedCategory}.",
                            "Duplicate", MessageBoxButton.OK, MessageBoxImage.Warning);
                        BtnSave.IsEnabled = true;
                        BtnSave.Content   = "Save";
                        TxtModelNumber.Focus();
                        return;
                    }

                    var confirm = MessageBox.Show(
                        $"This will move \"{modelNumber}\" out of Cartridge Models and into the {selectedCategory} " +
                        "list, along with its current stock. Any refill/vendor history specific to cartridges " +
                        "cannot come along and will be removed. This cannot be undone. Continue?",
                        "Change Category", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (confirm != MessageBoxResult.Yes)
                    {
                        BtnSave.IsEnabled = true;
                        BtnSave.Content   = "Save";
                        return;
                    }

                    var cartridgeRepo = new CartridgeModelRepository();
                    await cartridgeRepo.ConvertToConsumableModelAsync(
                        _cartridgeModelId, modelNumber, selectedCategory,
                        isRequestable, isActive, AppSession.CurrentUserId);

                    DialogResult = true;
                    return;
                }

                var repository = new CartridgeModelRepository();

                var existing = await repository.FindByModelNumberAsync(modelNumber);
                if (existing != null && existing.CartridgeModelId != _cartridgeModelId)
                {
                    MessageBox.Show($"Model number \"{modelNumber}\" already exists.",
                        "Duplicate", MessageBoxButton.OK, MessageBoxImage.Warning);
                    BtnSave.IsEnabled = true;
                    BtnSave.Content   = "Save";
                    TxtModelNumber.Focus();
                    return;
                }

                _model.ModelNumber   = modelNumber;
                _model.IsRequestable = isRequestable;
                _model.IsRefillable  = ChkRefillable.IsChecked == true;
                _model.IsActive      = isActive;

                await repository.UpdateAsync(_model);

                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                BtnSave.IsEnabled = true;
                BtnSave.Content   = "Save";
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
