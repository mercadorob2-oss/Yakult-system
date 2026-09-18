using System;
using System.Windows.Forms;
using Yakult.Inventory.App.Core;

namespace Yakult.Inventory.App.Helpers
{
    internal static class ListPageStateHelper
    {
        public static void RestoreDateRange(
            DateTimePicker fromPicker,
            DateTimePicker toPicker,
            DateTime savedFrom,
            DateTime savedTo,
            Func<DateTime> defaultFromFactory,
            Func<DateTime> defaultToFactory)
        {
            if (fromPicker == null || toPicker == null)
                return;

            var fallbackFrom = ClampToPicker(defaultFromFactory != null ? defaultFromFactory() : DateTime.Today, fromPicker);
            var fallbackTo = ClampToPicker(defaultToFactory != null ? defaultToFactory() : DateTime.Today, toPicker);

            var restoredFrom = IsValidUserDate(savedFrom, fromPicker)
                ? ClampToPicker(savedFrom.Date, fromPicker)
                : fallbackFrom;
            var restoredTo = IsValidUserDate(savedTo, toPicker)
                ? ClampToPicker(savedTo.Date, toPicker)
                : fallbackTo;

            if (restoredFrom > restoredTo)
                restoredFrom = ClampToPicker(restoredTo, fromPicker);

            fromPicker.Value = restoredFrom;
            toPicker.Value = restoredTo;
        }

        public static void RestoreDateIfValid(DateTimePicker picker, DateTime savedValue, Func<DateTime> fallbackFactory)
        {
            if (picker == null)
                return;

            var restored = IsValidUserDate(savedValue, picker)
                ? ClampToPicker(savedValue.Date, picker)
                : ClampToPicker(fallbackFactory != null ? fallbackFactory() : DateTime.Today, picker);

            picker.Value = restored;
        }

        public static void SelectComboItemByText(ComboBox combo, string value)
        {
            if (combo == null || string.IsNullOrWhiteSpace(value))
                return;

            for (var i = 0; i < combo.Items.Count; i++)
            {
                var text = combo.Items[i]?.ToString();
                if (string.Equals(text, value, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
        }

        public static void LogStateIssue(string pageName, string operation, Exception ex)
        {
            if (string.IsNullOrWhiteSpace(pageName) || string.IsNullOrWhiteSpace(operation))
                return;

            Logger.LogWarning($"{pageName}: {operation} issue. {ex?.Message ?? "No details."}");
        }

        private static bool IsValidUserDate(DateTime value, DateTimePicker picker)
        {
            return value != default(DateTime)
                   && value >= picker.MinDate
                   && value <= picker.MaxDate;
        }

        private static DateTime ClampToPicker(DateTime value, DateTimePicker picker)
        {
            if (value < picker.MinDate)
                return picker.MinDate.Date;
            if (value > picker.MaxDate)
                return picker.MaxDate.Date;
            return value.Date;
        }
    }
}
