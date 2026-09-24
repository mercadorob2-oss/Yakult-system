using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Yakult.Inventory.App.WPF.Set.RequisitionForm.ViewModels;

namespace Yakult.Inventory.App.WPF.Set.RequisitionForm.Views
{
    public partial class RequisitionFormPrintView : UserControl
    {
        // Uniform shrink factor (1.0 = full size) applied to every font size and spacing
        // value in this form. RequisitionFormPrintService reduces it when a copy's line
        // items don't fit within its half of the page, so two copies always print on one
        // page instead of spilling the second copy onto a new page.
        public static readonly DependencyProperty FontScaleProperty =
            DependencyProperty.Register(nameof(FontScale), typeof(double), typeof(RequisitionFormPrintView),
                new PropertyMetadata(1.0));

        public double FontScale
        {
            get => (double)GetValue(FontScaleProperty);
            set => SetValue(FontScaleProperty, value);
        }

        public RequisitionFormPrintView()
        {
            InitializeComponent();
            DataContextChanged += (_, __) => BuildItemRows();
            Loaded += (_, __) => BuildItemRows();
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.Property == FontScaleProperty || e.Property == DataContextProperty)
                BuildItemRows();
        }

        private static string RemarkKey(string remarks)
        {
            return (remarks ?? "").Trim();
        }

        // Groups consecutive items sharing the same non blank REMARKS text so the
        // printed table merges them into one braced cell, as on the paper form.
        // Blank remarks are never merged; each keeps its own cell with dividers.
        private static List<List<RequisitionFormItem>> GroupItems(List<RequisitionFormItem> items)
        {
            var groups = new List<List<RequisitionFormItem>>();
            List<RequisitionFormItem> current = null;
            string currentKey = null;
            foreach (var item in items)
            {
                string key = RemarkKey(item.Remarks);
                if (!string.IsNullOrEmpty(key) && current != null && string.Equals(key, currentKey, StringComparison.Ordinal))
                {
                    current.Add(item);
                }
                else
                {
                    current = new List<RequisitionFormItem> { item };
                    groups.Add(current);
                    currentKey = key;
                }
            }
            return groups;
        }

        private Border MakeCell(string text, bool center, bool isRemarksColumn, bool bold = false)
        {
            double s = FontScale;
            var tb = new TextBlock
            {
                Text = text ?? "",
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Thickness(0),
            };
            if (bold)
                tb.FontWeight = FontWeights.Bold;
            if (center || isRemarksColumn)
            {
                tb.HorizontalAlignment = HorizontalAlignment.Center;
                tb.TextAlignment = TextAlignment.Center;
            }
            var border = new Border
            {
                BorderBrush = Brushes.Black,
                // No horizontal rules between item rows, as on the paper form:
                // only the header carries a bottom rule and the outer Border
                // draws the outline. Column separators come from the right
                // edge of the QUANTITY and DESCRIPTION cells.
                BorderThickness = isRemarksColumn
                    ? new Thickness(0, 0, 0, 0)
                    : new Thickness(0, 0, 1, 0),
                Padding = new Thickness(4 * s, 3 * s, 4 * s, 3 * s),
                Child = tb
            };
            return border;
        }

        private Border MakeMergedRemarkCell(string text, int span)
        {
            double s = FontScale;
            var inner = new Grid { VerticalAlignment = VerticalAlignment.Center };
            inner.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            inner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            inner.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Brace glyphs approximate the hand drawn brackets on the paper form.
            // Size grows with the span so a 5 row brace looks taller than a 2 row one.
            double braceSize = Math.Max(24.0, 15.0 * span) * s;
            var left = new TextBlock
            {
                Text = "{",
                FontSize = braceSize,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 2 * s, 0)
            };
            var center = new TextBlock
            {
                Text = text ?? "",
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
            var right = new TextBlock
            {
                Text = "}",
                FontSize = braceSize,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2 * s, 0, 0, 0)
            };
            Grid.SetColumn(left, 0);
            Grid.SetColumn(center, 1);
            Grid.SetColumn(right, 2);
            inner.Children.Add(left);
            inner.Children.Add(center);
            inner.Children.Add(right);

            return new Border
            {
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(0, 0, 0, 0),
                Padding = new Thickness(4 * s, 3 * s, 4 * s, 3 * s),
                VerticalAlignment = VerticalAlignment.Stretch,
                Child = inner
            };
        }

        private void BuildItemRows()
        {
            if (ItemsTable == null)
                return;
            var vm = DataContext as RequisitionFormViewModel;
            var items = vm?.Items ?? new List<RequisitionFormItem>();

            ItemsTable.RowDefinitions.Clear();
            ItemsTable.Children.Clear();

            double s = FontScale;
            int row = 0;
            foreach (var group in GroupItems(items))
            {
                bool merged = group.Count > 1 && !string.IsNullOrEmpty(RemarkKey(group[0].Remarks));
                if (merged)
                {
                    foreach (var item in group)
                    {
                        var rd = new RowDefinition { Height = GridLength.Auto, MinHeight = 26 * s };
                        ItemsTable.RowDefinitions.Add(rd);
                        var qty = MakeCell(item.Quantity, true, false);
                        var desc = MakeCell(item.Description, false, false, true);
                        Grid.SetRow(qty, row);
                        Grid.SetColumn(qty, 0);
                        Grid.SetRow(desc, row);
                        Grid.SetColumn(desc, 1);
                        ItemsTable.Children.Add(qty);
                        ItemsTable.Children.Add(desc);
                        row++;
                    }
                    var mergedCell = MakeMergedRemarkCell(RemarkKey(group[0].Remarks), group.Count);
                    Grid.SetRow(mergedCell, row - group.Count);
                    Grid.SetRowSpan(mergedCell, group.Count);
                    Grid.SetColumn(mergedCell, 2);
                    ItemsTable.Children.Add(mergedCell);
                }
                else
                {
                    foreach (var item in group)
                    {
                        var rd = new RowDefinition { Height = GridLength.Auto, MinHeight = 26 * s };
                        ItemsTable.RowDefinitions.Add(rd);
                        var qty = MakeCell(item.Quantity, true, false);
                        var desc = MakeCell(item.Description, false, false, true);
                        var rem = MakeCell(item.Remarks, false, true);
                        Grid.SetRow(qty, row);
                        Grid.SetColumn(qty, 0);
                        Grid.SetRow(desc, row);
                        Grid.SetColumn(desc, 1);
                        Grid.SetRow(rem, row);
                        Grid.SetColumn(rem, 2);
                        ItemsTable.Children.Add(qty);
                        ItemsTable.Children.Add(desc);
                        ItemsTable.Children.Add(rem);
                        row++;
                    }
                }
            }
        }
    }
}
