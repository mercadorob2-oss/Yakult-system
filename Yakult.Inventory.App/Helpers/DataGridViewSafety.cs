using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace Yakult.Inventory.App.Helpers
{
    internal static class DataGridViewSafety
    {
        private sealed class AttachedState { }
        private static readonly ConditionalWeakTable<DataGridView, AttachedState> Attached = new ConditionalWeakTable<DataGridView, AttachedState>();

        internal static void Attach(DataGridView grid)
        {
            if (grid == null)
                return;

            if (Attached.TryGetValue(grid, out _))
                return;

            Attached.Add(grid, new AttachedState());

            grid.DataError += Grid_DataError;
            grid.DataBindingComplete += Grid_DataBindingComplete;
            grid.CellParsing += Grid_CellParsing;
            grid.CellFormatting += Grid_CellFormatting;

            // If we attach after binding already happened, still try to normalize checkbox columns once.
            try
            {
                ConfigureBoundCheckBoxColumns(grid);
            }
            catch
            {
            }
        }

        internal static void AttachAll(Control root)
        {
            if (root == null)
                return;

            if (root is DataGridView grid)
            {
                Attach(grid);
            }

            foreach (Control child in root.Controls)
            {
                AttachAll(child);
            }
        }

        private static void Grid_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            var grid = sender as DataGridView;
            if (grid == null)
                return;

            try
            {
                ConfigureBoundCheckBoxColumns(grid);
            }
            catch
            {
                // Never break UI because of a "safety" helper.
            }
        }

        private static void Grid_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            // Prevent WinForms from showing the default modal dialog.
            // Common trigger: checkbox column bound to non-bool (e.g., 0/1, "0"/"1") or paste operations.
            if (e?.Exception is null)
                return;

            if (e.Exception is FormatException || e.Exception is ArgumentException || e.Exception is InvalidCastException)
            {
                e.ThrowException = false;
                e.Cancel = true;
            }
        }

        private static void Grid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            var grid = sender as DataGridView;
            if (grid == null || e.RowIndex < 0 || e.ColumnIndex < 0)
                return;

            if (!(grid.Columns[e.ColumnIndex] is DataGridViewCheckBoxColumn))
                return;

            if (e.Value == null || e.Value == DBNull.Value)
                return;

            if (e.Value is bool)
                return;

            if (TryCoerceToBool(e.Value, out var b))
            {
                e.Value = b;
                e.FormattingApplied = true;
            }
        }

        private static void Grid_CellParsing(object sender, DataGridViewCellParsingEventArgs e)
        {
            var grid = sender as DataGridView;
            if (grid == null || e.RowIndex < 0 || e.ColumnIndex < 0)
                return;

            if (!(grid.Columns[e.ColumnIndex] is DataGridViewCheckBoxColumn checkCol))
                return;

            if (e.Value == null || e.Value == DBNull.Value)
                return;

            var targetType = checkCol.ValueType ?? typeof(bool);

            // Typical cases:
            // - checkbox is bound to BIT/bool: allow "1/0", "yes/no", etc.
            // - checkbox is bound to numeric 0/1: convert bool -> numeric
            // - checkbox is unbound: still parse pasted strings into bool
            if (targetType == typeof(bool))
            {
                if (e.Value is bool)
                    return;

                if (TryCoerceToBool(e.Value, out var b))
                {
                    e.Value = b;
                    e.ParsingApplied = true;
                }

                return;
            }

            if (IsNumericType(targetType))
            {
                if (e.Value is bool b)
                {
                    e.Value = Convert.ChangeType(b ? 1 : 0, targetType, CultureInfo.InvariantCulture);
                    e.ParsingApplied = true;
                    return;
                }

                if (TryCoerceToBool(e.Value, out var b2))
                {
                    e.Value = Convert.ChangeType(b2 ? 1 : 0, targetType, CultureInfo.InvariantCulture);
                    e.ParsingApplied = true;
                    return;
                }

                if (e.Value is string s && long.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
                {
                    e.Value = Convert.ChangeType(n, targetType, CultureInfo.InvariantCulture);
                    e.ParsingApplied = true;
                }

                return;
            }

            if (targetType == typeof(string))
            {
                if (e.Value is bool b)
                {
                    e.Value = b ? "1" : "0";
                    e.ParsingApplied = true;
                    return;
                }

                if (TryCoerceToBool(e.Value, out var b2))
                {
                    e.Value = b2 ? "1" : "0";
                    e.ParsingApplied = true;
                }
            }
        }

        private static void ConfigureBoundCheckBoxColumns(DataGridView grid)
        {
            var dataSource = UnwrapDataSource(grid.DataSource);
            if (dataSource == null)
                return;

            foreach (DataGridViewColumn col in grid.Columns)
            {
                if (!(col is DataGridViewCheckBoxColumn cb))
                    continue;

                if (string.IsNullOrWhiteSpace(cb.DataPropertyName))
                    continue;

                if (!TryGetBoundColumnInfo(dataSource, cb.DataPropertyName, out var boundType, out var allowNull))
                    continue;

                // Normalize nullable types
                if (boundType != null && boundType.IsGenericType && boundType.GetGenericTypeDefinition() == typeof(Nullable<>))
                    boundType = Nullable.GetUnderlyingType(boundType) ?? boundType;

                if (boundType == typeof(bool))
                {
                    cb.ValueType = typeof(bool);
                    cb.TrueValue = true;
                    cb.FalseValue = false;
                    cb.ThreeState = allowNull;
                    cb.IndeterminateValue = allowNull ? (object)DBNull.Value : null;
                    continue;
                }

                if (boundType == typeof(string))
                {
                    cb.ValueType = typeof(string);
                    cb.TrueValue = "1";
                    cb.FalseValue = "0";
                    cb.ThreeState = allowNull;
                    cb.IndeterminateValue = allowNull ? (object)DBNull.Value : null;
                    continue;
                }

                if (IsNumericType(boundType))
                {
                    cb.ValueType = boundType;
                    cb.TrueValue = Convert.ChangeType(1, boundType, CultureInfo.InvariantCulture);
                    cb.FalseValue = Convert.ChangeType(0, boundType, CultureInfo.InvariantCulture);
                    cb.ThreeState = allowNull;
                    cb.IndeterminateValue = allowNull ? (object)DBNull.Value : null;
                }
            }
        }

        private static object UnwrapDataSource(object dataSource)
        {
            if (dataSource == null)
                return null;

            // BindingSource is common in WinForms.
            if (dataSource is BindingSource bs)
            {
                if (bs.DataSource != null)
                    return UnwrapDataSource(bs.DataSource);
                if (bs.List != null)
                    return bs.List;
            }

            return dataSource;
        }

        private static bool TryGetBoundColumnInfo(object dataSource, string dataPropertyName, out Type type, out bool allowNull)
        {
            type = null;
            allowNull = false;

            if (string.IsNullOrWhiteSpace(dataPropertyName) || dataSource == null)
                return false;

            // DataTable/DataView bindings
            if (dataSource is DataTable dt)
            {
                if (!dt.Columns.Contains(dataPropertyName))
                    return false;

                var dc = dt.Columns[dataPropertyName];
                type = dc.DataType;
                allowNull = dc.AllowDBNull;
                return type != null;
            }

            if (dataSource is DataView dv)
            {
                var table = dv.Table;
                if (table == null || !table.Columns.Contains(dataPropertyName))
                    return false;

                var dc = table.Columns[dataPropertyName];
                type = dc.DataType;
                allowNull = dc.AllowDBNull;
                return type != null;
            }

            // IList bindings (List<T>, BindingList<T>, etc)
            if (dataSource is IList list)
            {
                Type elementType = null;

                if (list.Count > 0 && list[0] != null)
                    elementType = list[0].GetType();
                else
                    elementType = TryGetElementTypeFromEnumerableType(list.GetType());

                if (elementType == null)
                    return false;

                var props = TypeDescriptor.GetProperties(elementType);
                var prop = props?.Find(dataPropertyName, ignoreCase: false);
                if (prop == null)
                    return false;

                type = prop.PropertyType;
                allowNull = !type.IsValueType || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>));
                return true;
            }

            return false;
        }

        private static Type TryGetElementTypeFromEnumerableType(Type t)
        {
            if (t == null)
                return null;

            if (t.IsArray)
                return t.GetElementType();

            var enumerableIface = t.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

            return enumerableIface?.GetGenericArguments().FirstOrDefault();
        }

        private static bool IsNumericType(Type t)
        {
            if (t == null)
                return false;

            t = Nullable.GetUnderlyingType(t) ?? t;

            return t == typeof(byte) ||
                   t == typeof(sbyte) ||
                   t == typeof(short) ||
                   t == typeof(ushort) ||
                   t == typeof(int) ||
                   t == typeof(uint) ||
                   t == typeof(long) ||
                   t == typeof(ulong) ||
                   t == typeof(decimal);
        }

        private static bool TryCoerceToBool(object value, out bool result)
        {
            result = false;
            if (value == null || value == DBNull.Value)
                return false;

            if (value is bool b)
            {
                result = b;
                return true;
            }

            if (value is byte by)
            {
                result = by != 0;
                return true;
            }

            if (value is sbyte sb)
            {
                result = sb != 0;
                return true;
            }

            if (value is short sh)
            {
                result = sh != 0;
                return true;
            }

            if (value is ushort ush)
            {
                result = ush != 0;
                return true;
            }

            if (value is int i)
            {
                result = i != 0;
                return true;
            }

            if (value is uint ui)
            {
                result = ui != 0;
                return true;
            }

            if (value is long l)
            {
                result = l != 0;
                return true;
            }

            if (value is ulong ul)
            {
                result = ul != 0;
                return true;
            }

            if (value is decimal dec)
            {
                result = dec != 0m;
                return true;
            }

            if (value is string s)
            {
                var text = (s ?? string.Empty).Trim();
                if (text.Length == 0)
                    return false;

                if (text.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                    text.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                    text.Equals("t", StringComparison.OrdinalIgnoreCase) ||
                    text.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                    text.Equals("y", StringComparison.OrdinalIgnoreCase) ||
                    text.Equals("checked", StringComparison.OrdinalIgnoreCase))
                {
                    result = true;
                    return true;
                }

                if (text.Equals("0", StringComparison.OrdinalIgnoreCase) ||
                    text.Equals("false", StringComparison.OrdinalIgnoreCase) ||
                    text.Equals("f", StringComparison.OrdinalIgnoreCase) ||
                    text.Equals("no", StringComparison.OrdinalIgnoreCase) ||
                    text.Equals("n", StringComparison.OrdinalIgnoreCase) ||
                    text.Equals("unchecked", StringComparison.OrdinalIgnoreCase))
                {
                    result = false;
                    return true;
                }

                // Last chance: numeric strings
                if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
                {
                    result = n != 0;
                    return true;
                }
            }

            return false;
        }
    }
}
