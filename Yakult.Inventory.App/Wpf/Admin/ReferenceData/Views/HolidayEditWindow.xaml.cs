using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Yakult.Inventory.App.Repositories;

namespace Yakult.Inventory.App.WPF.Admin.ReferenceData.Views
{
    /// <summary>Add / Edit Holiday dialog (WPF port of the WinForms HolidayEditDialog).</summary>
    public partial class HolidayEditWindow : Window
    {
        private readonly List<HolidayDto> _existing;
        private readonly int? _editingId;

        public HolidayDto Result { get; private set; }

        public HolidayEditWindow(HolidayDto editing, DateTime defaultDate, List<HolidayDto> allHolidays)
        {
            InitializeComponent();

            _existing = allHolidays ?? new List<HolidayDto>();
            _editingId = editing?.HolidayId;

            bool isNew = editing == null;
            Title = isNew ? "Add Holiday" : "Edit Holiday";
            TxtHeader.Text = isNew ? "Add New Holiday" : "Edit Holiday";

            TxtName.Text = editing?.HolidayName ?? "";
            DpDate.SelectedDate = editing?.HolidayDate ?? defaultDate;
            CboType.SelectedIndex = editing?.HolidayType == "Special Non-Working" ? 1 : 0;
            ChkRecurring.IsChecked = editing?.IsRecurring ?? false;
            ChkActive.IsChecked = editing?.IsActive ?? true;
            TxtNotes.Text = editing?.Notes ?? "";

            Loaded += (s, e) => { TxtName.Focus(); CheckDuplicate(); };
        }

        private DateTime PickedDate => (DpDate.SelectedDate ?? DateTime.Today).Date;

        private bool IsDuplicate(DateTime picked)
        {
            bool recurring = ChkRecurring.IsChecked == true;
            return _existing.Any(h =>
                h.HolidayId != (_editingId ?? -1) &&
                h.HolidayDate.Month == picked.Month &&
                h.HolidayDate.Day   == picked.Day   &&
                (h.IsRecurring || recurring || h.HolidayDate.Year == picked.Year));
        }

        private void CheckDuplicate()
        {
            if (TxtDupeWarning == null) return;   // fires during InitializeComponent
            var picked = PickedDate;
            TxtDupeWarning.Text = IsDuplicate(picked) ? $"⚠  A holiday already exists on {picked:MMM dd}." : "";
        }

        private void Inputs_Changed(object sender, RoutedEventArgs e) => CheckDuplicate();

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtName.Text))
            {
                MessageBox.Show(this, "Holiday name is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtName.Focus();
                return;
            }

            if (DpDate.SelectedDate == null)
            {
                MessageBox.Show(this, "Please pick a date.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                DpDate.Focus();
                return;
            }

            var picked = PickedDate;
            if (IsDuplicate(picked))
            {
                MessageBox.Show(this, $"A holiday already exists on {picked:MMM dd}. Choose a different date.",
                    "Duplicate Date", MessageBoxButton.OK, MessageBoxImage.Warning);
                DpDate.Focus();
                return;
            }

            Result = new HolidayDto
            {
                HolidayName = TxtName.Text.Trim(),
                HolidayDate = picked,
                HolidayType = (CboType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Regular Holiday",
                IsRecurring = ChkRecurring.IsChecked == true,
                IsActive    = ChkActive.IsChecked == true,
                Notes       = string.IsNullOrWhiteSpace(TxtNotes.Text) ? null : TxtNotes.Text.Trim()
            };
            DialogResult = true;
        }
    }
}
