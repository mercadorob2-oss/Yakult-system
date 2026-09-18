using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace Yakult.Inventory.App.Pages.Item
{
    /// <summary>
    /// "Enter one serial number per line" popup for a single Build Invoice line — the grid-cell
    /// equivalent of Batch Add Items' Serial Numbers box, since a DataGrid cell has no room for a
    /// multi-line textarea of its own.
    /// </summary>
    public partial class SerialNumbersDialog : Window
    {
        /// <summary>The parsed, trimmed, uppercased, de-duplicated result — populated only when
        /// the dialog closes via Save.</summary>
        public List<string> Result { get; private set; }

        public SerialNumbersDialog(string itemName, IEnumerable<string> existingSerials)
        {
            InitializeComponent();

            LblItemName.Text = string.IsNullOrWhiteSpace(itemName) ? "Serial Numbers" : $"Serial Numbers — {itemName}";
            TxtSerials.Text = string.Join(Environment.NewLine, existingSerials ?? Enumerable.Empty<string>());
            TxtSerials.TextChanged += (s, e) => UpdateCount();

            Loaded += (s, e) => { UpdateCount(); TxtSerials.Focus(); TxtSerials.CaretIndex = TxtSerials.Text.Length; };
        }

        private List<string> ParseSerials()
        {
            return TxtSerials.Text
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim().ToUpperInvariant())
                .Where(s => s.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void UpdateCount()
        {
            int count = ParseSerials().Count;
            LblCount.Text = count == 1 ? "1 serial number" : $"{count} serial number(s)";
        }

        private void OnHeaderDrag(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void OnCloseClick(object sender, MouseButtonEventArgs e) => Close();

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => Close();

        private void BtnClearAll_Click(object sender, RoutedEventArgs e)
        {
            TxtSerials.Clear();
            TxtSerials.Focus();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            Result = ParseSerials();
            DialogResult = true;
            Close();
        }
    }
}
