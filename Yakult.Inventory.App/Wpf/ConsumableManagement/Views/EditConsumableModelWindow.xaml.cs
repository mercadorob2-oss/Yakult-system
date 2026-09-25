using System;
using System.Windows;
using System.Windows.Controls;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;

namespace Yakult.Inventory.App.WPF.ConsumableManagement.Views
{
    public partial class EditConsumableModelWindow : Window
    {
        private readonly int _consumableModelId;
        private ConsumableModelDto _model;

        public EditConsumableModelWindow(int consumableModelId)
        {
            InitializeComponent();
            _consumableModelId = consumableModelId;
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var repository = new ConsumableModelRepository();
                _model = await repository.GetByIdAsync(_consumableModelId);

                if (_model == null)
                {
                    MessageBox.Show("Consumable model not found.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    DialogResult = false;
                    return;
                }

                TxtModelNumber.Text = _model.ModelNumber;
                SelectCategory(_model.Category);
                ChkRequestable.IsChecked = _model.IsRequestable;
                ChkActive.IsChecked = _model.IsActive;

                LoadingText.Visibility = Visibility.Collapsed;
                FormPanel.Visibility = Visibility.Visible;
                TxtModelNumber.Focus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading consumable model: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                DialogResult = false;
            }
        }

        private void SelectCategory(string category)
        {
            // Canonical compare: the list shows "Print Head" while models are stored as "Printhead".
            // An exact compare fell through to index 0, so saving silently changed the category.
            var wanted = Yakult.Inventory.App.Models.ConsumableCategories.Canonicalize(category);
            foreach (ComboBoxItem item in CmbCategory.Items)
            {
                if (string.Equals(Yakult.Inventory.App.Models.ConsumableCategories.Canonicalize(item.Content?.ToString()),
                                  wanted, StringComparison.OrdinalIgnoreCase))
                {
                    CmbCategory.SelectedItem = item;
                    return;
                }
            }
            CmbCategory.SelectedIndex = 0;
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

            string category = (CmbCategory.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Ink";

            try
            {
                BtnSave.IsEnabled = false;
                BtnSave.Content = "Saving…";

                var repository = new ConsumableModelRepository();

                var existing = await repository.FindByModelNumberAsync(TxtModelNumber.Text.Trim(), category);
                if (existing != null && existing.ConsumableModelId != _consumableModelId)
                {
                    MessageBox.Show($"Model number \"{TxtModelNumber.Text.Trim()}\" already exists for {category}.",
                        "Duplicate", MessageBoxButton.OK, MessageBoxImage.Warning);
                    BtnSave.IsEnabled = true;
                    BtnSave.Content = "Save";
                    TxtModelNumber.Focus();
                    return;
                }

                _model.ModelNumber = TxtModelNumber.Text.Trim();
                _model.Category = category;
                _model.IsRequestable = ChkRequestable.IsChecked == true;
                _model.IsActive = ChkActive.IsChecked == true;

                await repository.UpdateAsync(_model);

                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                BtnSave.IsEnabled = true;
                BtnSave.Content = "Save";
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
