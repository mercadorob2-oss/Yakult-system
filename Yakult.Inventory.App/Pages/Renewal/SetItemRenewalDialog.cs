using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Pages.Renewal
{
    /// <summary>
    /// Modal dialog showing all SetItems for a given Set with their individual RenewalStatus.
    /// Supports renewing individual items (partial renewal) without touching the whole Set.
    /// </summary>
    public class SetItemRenewalDialog : Form
    {
        private readonly int _setId;
        private readonly RenewalDto _setInfo;
        private readonly RenewalRepository _repository;

        private DataGridView _dgv;
        private List<SetItemRenewalDto> _items;
        private Label _lblSetInfo;
        private Label _lblItemCounts;
        private Button _btnClose;
        private Button _btnRenewAll;

        public SetItemRenewalDialog(int setId, RenewalDto setInfo)
        {
            _setId = setId;
            _setInfo = setInfo;
            _repository = new RenewalRepository();
            InitializeDialog();
            LoadItems();
        }

        private void InitializeDialog()
        {
            Text = $"Items — {_setInfo?.SetCode ?? $"Set #{_setId}"}";
            Size = new Size(900, 560);
            MinimumSize = new Size(760, 440);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = false;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9F);

            // ── Header info ────────────────────────────────────────────────────────────
            var pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 72,
                BackColor = Color.FromArgb(245, 247, 250),
                Padding = new Padding(16, 10, 16, 8)
            };

            _lblSetInfo = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 22,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 40, 40),
                Text = BuildSetInfoText()
            };

            _lblItemCounts = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 20,
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(90, 90, 90),
                Text = "Loading items..."
            };

            pnlHeader.Controls.Add(_lblItemCounts);
            pnlHeader.Controls.Add(_lblSetInfo);

            // ── Grid ───────────────────────────────────────────────────────────────────
            _dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowHeadersVisible = false,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                GridColor = Color.FromArgb(230, 230, 230),
                BackgroundColor = Color.White,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = 36,
                RowTemplate = { Height = 34 }
            };

            // Style header
            _dgv.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(52, 73, 94),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 8, 0)
            };
            _dgv.EnableHeadersVisualStyles = false;

            // Alternating row colors
            _dgv.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(248, 249, 252)
            };
            _dgv.DefaultCellStyle.Padding = new Padding(8, 0, 8, 0);

            // Columns
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colDescription",
                DataPropertyName = "Description",
                HeaderText = "Description",
                FillWeight = 28,
                MinimumWidth = 160
            });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colItemCode",
                DataPropertyName = "ItemCode",
                HeaderText = "Item Code",
                FillWeight = 12,
                MinimumWidth = 90
            });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colUoM",
                DataPropertyName = "UnitOfMeasure",
                HeaderText = "UoM",
                FillWeight = 7,
                MinimumWidth = 55
            });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colQty",
                DataPropertyName = "Quantity",
                HeaderText = "Qty",
                FillWeight = 7,
                MinimumWidth = 55,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, Format = "N0" }
            });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colUnitPrice",
                DataPropertyName = "UnitPrice",
                HeaderText = "Unit Price",
                FillWeight = 12,
                MinimumWidth = 90,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2" }
            });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colLineStart",
                DataPropertyName = "LineStartDate",
                HeaderText = "Line Start",
                FillWeight = 10,
                MinimumWidth = 90,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "MM/dd/yyyy" }
            });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colLineEnd",
                DataPropertyName = "LineEndDate",
                HeaderText = "Line End",
                FillWeight = 10,
                MinimumWidth = 90,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "MM/dd/yyyy" }
            });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colStatus",
                DataPropertyName = "DisplayStatus",
                HeaderText = "Status",
                FillWeight = 10,
                MinimumWidth = 80
            });

            var colRenewAction = new DataGridViewLinkColumn
            {
                Name = "colRenewAction",
                HeaderText = "Action",
                Text = "Renew Item",
                UseColumnTextForLinkValue = false,
                FillWeight = 8,
                MinimumWidth = 80,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                Width = 90,
                TrackVisitedState = false,
                LinkBehavior = LinkBehavior.HoverUnderline,
                LinkColor = Color.FromArgb(41, 128, 185),
                ActiveLinkColor = Color.FromArgb(21, 100, 155),
                VisitedLinkColor = Color.FromArgb(41, 128, 185)
            };
            _dgv.Columns.Add(colRenewAction);

            _dgv.CellFormatting += Dgv_CellFormatting;
            _dgv.CellContentClick += Dgv_CellContentClick;

            // ── Footer buttons ─────────────────────────────────────────────────────────
            var pnlFooter = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 52,
                BackColor = Color.FromArgb(245, 247, 250),
                Padding = new Padding(16, 10, 16, 10)
            };

            _btnClose = new Button
            {
                Text = "Close",
                DialogResult = DialogResult.Cancel,
                AutoSize = false,
                Size = new Size(100, 32),
                Dock = DockStyle.Right,
                BackColor = Color.FromArgb(189, 195, 199),
                ForeColor = Color.FromArgb(40, 40, 40),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            _btnClose.FlatAppearance.BorderSize = 0;

            _btnRenewAll = new Button
            {
                Text = "Renew All Active",
                AutoSize = false,
                Size = new Size(140, 32),
                Dock = DockStyle.Left,
                BackColor = Color.FromArgb(46, 204, 113),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            _btnRenewAll.FlatAppearance.BorderSize = 0;
            _btnRenewAll.Click += BtnRenewAll_Click;

            pnlFooter.Controls.Add(_btnClose);
            pnlFooter.Controls.Add(_btnRenewAll);

            Controls.Add(_dgv);
            Controls.Add(pnlHeader);
            Controls.Add(pnlFooter);

            CancelButton = _btnClose;
        }

        private string BuildSetInfoText()
        {
            if (_setInfo == null) return $"Set #{_setId}";
            var parts = new List<string> { _setInfo.SetCode ?? $"Set #{_setId}" };
            if (!string.IsNullOrEmpty(_setInfo.SetType)) parts.Add(_setInfo.SetType);
            if (!string.IsNullOrEmpty(_setInfo.CompanyName) && _setInfo.CompanyName != "N/A")
                parts.Add(_setInfo.CompanyName);
            return string.Join("  ·  ", parts);
        }

        private void LoadItems()
        {
            try
            {
                _items = _repository.GetSetItemsForRenewal(_setId);
                _dgv.DataSource = null;
                _dgv.DataSource = _items;
                UpdateCountsLabel();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load items:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateCountsLabel()
        {
            if (_items == null) return;
            int total    = _items.Count;
            int active   = _items.Count(x => x.CanRenew);
            int renewed  = _items.Count(x => x.RenewalStatus == "Renewed");
            int archived = _items.Count(x => x.RenewalStatus == "Archived");
            _lblItemCounts.Text =
                $"Total: {total}   |   Active: {active}   |   Renewed: {renewed}   |   Archived: {archived}";

            _btnRenewAll.Enabled = active > 0;
        }

        private void Dgv_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;

            var item = _dgv.Rows[e.RowIndex].DataBoundItem as SetItemRenewalDto;
            if (item == null) return;

            if (_dgv.Columns[e.ColumnIndex].Name == "colStatus")
            {
                switch (item.DisplayStatus)
                {
                    case "Active":
                        e.CellStyle.ForeColor = Color.FromArgb(39, 174, 96);
                        e.CellStyle.Font = new Font(_dgv.Font.FontFamily, _dgv.Font.Size, FontStyle.Bold);
                        break;
                    case "Expired":
                        e.CellStyle.ForeColor = Color.FromArgb(192, 57, 43);
                        e.CellStyle.Font = new Font(_dgv.Font.FontFamily, _dgv.Font.Size, FontStyle.Bold);
                        break;
                    case "Renewed":
                        e.CellStyle.ForeColor = Color.FromArgb(41, 128, 185);
                        break;
                    case "Archived":
                        e.CellStyle.ForeColor = Color.FromArgb(149, 165, 166);
                        break;
                }
            }

            if (_dgv.Columns[e.ColumnIndex].Name == "colRenewAction")
            {
                if (!item.CanRenew)
                {
                    e.Value = item.RenewalStatus == "Renewed" ? "Renewed" : "—";
                    e.CellStyle.ForeColor = Color.FromArgb(149, 165, 166);
                }
                else
                {
                    e.Value = "Renew Item";
                    e.CellStyle.ForeColor = Color.FromArgb(41, 128, 185);
                }
                e.CellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }
        }

        private void Dgv_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (_dgv.Columns[e.ColumnIndex].Name != "colRenewAction") return;

            var item = _dgv.Rows[e.RowIndex].DataBoundItem as SetItemRenewalDto;
            if (item == null || !item.CanRenew) return;

            PromptAndRenewItem(item);
        }

        private void BtnRenewAll_Click(object sender, EventArgs e)
        {
            if (_items == null) return;
            var renewable = _items.Where(x => x.CanRenew).ToList();
            if (renewable.Count == 0) return;

            List<ItemCatalogDto> catalog;
            try { catalog = _repository.GetRenewableItemCatalog(); }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load item catalog:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var seedItem = renewable.FirstOrDefault();
            using (var dlg = new RenewItemDialog($"Renew All Active Items ({renewable.Count})", catalog, seedItem))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                int userId = AppSession.CurrentUserId;
                int successCount = 0;
                var errors = new List<string>();

                foreach (var item in renewable)
                {
                    try
                    {
                        string description = string.IsNullOrWhiteSpace(dlg.SelectedDescription)
                            ? item.Description
                            : dlg.SelectedDescription;
                        _repository.RenewSingleItem(
                            item.SetItemId,
                            description,
                            dlg.SelectedStartDate,
                            dlg.SelectedEndDate,
                            userId,
                            dlg.SelectedItemId,
                            dlg.SelectedItemCode,
                            dlg.SelectedUnitPrice,
                            dlg.SelectedQuantity,
                            dlg.SelectedUoM);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{item.Description}: {ex.Message}");
                    }
                }

                string msg = $"{successCount} item(s) renewed successfully.";
                if (errors.Count > 0)
                    msg += $"\n\nErrors ({errors.Count}):\n" + string.Join("\n", errors);

                MessageBox.Show(msg, "Renew All Complete",
                    MessageBoxButtons.OK,
                    errors.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);

                LoadItems();
            }
        }

        private void PromptAndRenewItem(SetItemRenewalDto item)
        {
            List<ItemCatalogDto> catalog;
            try { catalog = _repository.GetRenewableItemCatalog(); }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load item catalog:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            using (var dlg = new RenewItemDialog($"Renew Item — {item.Description}", catalog, item))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                string description = string.IsNullOrWhiteSpace(dlg.SelectedDescription)
                    ? item.Description
                    : dlg.SelectedDescription;

                try
                {
                    _repository.RenewSingleItem(
                        item.SetItemId,
                        description,
                        dlg.SelectedStartDate,
                        dlg.SelectedEndDate,
                        AppSession.CurrentUserId,
                        dlg.SelectedItemId,
                        dlg.SelectedItemCode,
                        dlg.SelectedUnitPrice,
                        dlg.SelectedQuantity,
                        dlg.SelectedUoM);

                    MessageBox.Show(
                        $"Item renewed successfully.\n\nNew start: {dlg.SelectedStartDate:MM/dd/yyyy}\nNew end:   {dlg.SelectedEndDate:MM/dd/yyyy}",
                        "Renewal Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    LoadItems();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Renewal failed:\n{ex.Message}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // ── Inner dialog ────────────────────────────────────────────────────────────

        private class RenewItemDialog : Form
        {
            private readonly List<ItemCatalogDto> _allItems;
            private TextBox _txtSearch;
            private ListBox _lstItems;
            private TextBox _txtItemCode;
            private TextBox _txtDescription;
            private NumericUpDown _numQuantity;
            private TextBox _txtUoM;
            private NumericUpDown _numUnitPrice;
            private DateTimePicker _dtpStart;
            private DateTimePicker _dtpEnd;

            public int? SelectedItemId { get; private set; }
            public string SelectedItemCode { get; private set; }
            public string SelectedDescription { get; private set; }
            public decimal SelectedQuantity { get; private set; }
            public string SelectedUoM { get; private set; }
            public decimal SelectedUnitPrice { get; private set; }
            public DateTime SelectedStartDate { get; private set; }
            public DateTime SelectedEndDate { get; private set; }

            public RenewItemDialog(string title, List<ItemCatalogDto> catalog, SetItemRenewalDto oldItem)
            {
                _allItems = catalog ?? new List<ItemCatalogDto>();

                Text = title;
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;
                BackColor = Color.White;
                Font = new Font("Segoe UI", 9F);

                const int labelX  = 12;
                const int ctrlX   = 165;
                const int ctrlW   = 430;
                int y = 12;

                // ── Current item header ─────────────────────────────────────────────
                if (oldItem != null)
                {
                    var pnl = new Panel
                    {
                        Location = new Point(8, y),
                        Size = new Size(ctrlX + ctrlW - 4, 46),
                        BackColor = Color.FromArgb(240, 244, 248),
                        BorderStyle = BorderStyle.FixedSingle
                    };
                    pnl.Controls.Add(new Label
                    {
                        Text = $"Current:  {oldItem.Description}  |  {oldItem.ItemCode}  |  {oldItem.UnitPrice:N2}",
                        Location = new Point(6, 6),
                        AutoSize = false,
                        Size = new Size(pnl.Width - 14, 34),
                        ForeColor = Color.FromArgb(80, 80, 80),
                        Font = new Font("Segoe UI", 8.5F)
                    });
                    Controls.Add(pnl);
                    y += 54;
                }

                // ── Helpers ─────────────────────────────────────────────────────────
                void AddLbl(string text)
                {
                    Controls.Add(new Label { Text = text, Location = new Point(labelX, y + 3), AutoSize = true });
                }
                T AddCtrl<T>(T ctrl, int? width = null) where T : Control
                {
                    ctrl.Location = new Point(ctrlX, y);
                    ctrl.Width = width ?? ctrlW;
                    Controls.Add(ctrl);
                    return ctrl;
                }

                // ── Item search + list ───────────────────────────────────────────────
                AddLbl("Search:");
                _txtSearch = AddCtrl(new TextBox(), ctrlW);
                y += 28;

                AddLbl("Select Item:");
                _lstItems = AddCtrl(new ListBox { Height = 100, IntegralHeight = false }, ctrlW);
                y += 108;

                // ── Editable fields ──────────────────────────────────────────────────
                AddLbl("Item Code:");
                _txtItemCode = AddCtrl(new TextBox { Text = oldItem?.ItemCode ?? "" }, ctrlW);
                y += 28;

                AddLbl("Description:");
                _txtDescription = AddCtrl(new TextBox { Text = oldItem?.Description ?? "" }, ctrlW);
                y += 28;

                AddLbl("Quantity:");
                _numQuantity = AddCtrl(new NumericUpDown
                {
                    Minimum       = 0,
                    Maximum       = 99999,
                    DecimalPlaces = 2,
                    Value         = Math.Max(0m, Math.Min(oldItem?.Quantity ?? 1m, 99999m))
                }, 130);
                y += 28;

                AddLbl("Unit of Measure:");
                _txtUoM = AddCtrl(new TextBox { Text = oldItem?.UnitOfMeasure ?? "" }, 130);
                y += 28;

                AddLbl("Unit Price:");
                _numUnitPrice = AddCtrl(new NumericUpDown
                {
                    Minimum       = 0,
                    Maximum       = 9999999,
                    DecimalPlaces = 2,
                    Value         = Math.Max(0m, Math.Min(oldItem?.UnitPrice ?? 0m, 9999999m))
                }, 160);
                y += 28;

                // ── Dates ────────────────────────────────────────────────────────────
                AddLbl("New Start Date:");
                _dtpStart = AddCtrl(new DateTimePicker { Format = DateTimePickerFormat.Short, Value = DateTime.Today }, 200);
                y += 28;

                AddLbl("New End Date:");
                _dtpEnd = AddCtrl(new DateTimePicker { Format = DateTimePickerFormat.Short, Value = DateTime.Today.AddYears(1) }, 200);
                y += 38;

                // ── Buttons ──────────────────────────────────────────────────────────
                var btnConfirm = new Button
                {
                    Text        = "Confirm Renewal",
                    DialogResult = DialogResult.OK,
                    Location    = new Point(ctrlX, y),
                    Size        = new Size(150, 32),
                    BackColor   = Color.FromArgb(46, 204, 113),
                    ForeColor   = Color.White,
                    FlatStyle   = FlatStyle.Flat,
                    Font        = new Font("Segoe UI", 9F, FontStyle.Bold)
                };
                btnConfirm.FlatAppearance.BorderSize = 0;

                var btnCancel = new Button
                {
                    Text        = "Cancel",
                    DialogResult = DialogResult.Cancel,
                    Location    = new Point(ctrlX + 158, y),
                    Size        = new Size(90, 32)
                };

                Controls.Add(btnConfirm);
                Controls.Add(btnCancel);
                AcceptButton = btnConfirm;
                CancelButton = btnCancel;
                ClientSize   = new Size(ctrlX + ctrlW + 16, y + 64);

                // ── Wire up events ───────────────────────────────────────────────────
                PopulateList(_allItems);

                _txtSearch.TextChanged += (s, ev) =>
                {
                    string q = _txtSearch.Text.Trim().ToLower();
                    var filtered = string.IsNullOrEmpty(q)
                        ? _allItems
                        : _allItems.Where(x =>
                            (x.Name     ?? "").ToLower().Contains(q) ||
                            (x.ItemCode ?? "").ToLower().Contains(q)).ToList();
                    PopulateList(filtered);
                };

                _lstItems.SelectedIndexChanged += (s, ev) =>
                {
                    if (_lstItems.SelectedItem is ItemCatalogDto cat)
                    {
                        _txtItemCode.Text    = cat.ItemCode ?? "";
                        _txtDescription.Text = cat.Name    ?? "";
                        _txtUoM.Text         = cat.UnitOfMeasure ?? "";
                        decimal price = Math.Max(0m, Math.Min(cat.UnitPrice, 9999999m));
                        _numUnitPrice.Value  = price;
                    }
                };

                btnConfirm.Click += (s, ev) =>
                {
                    SelectedItemId      = (_lstItems.SelectedItem as ItemCatalogDto)?.ItemId;
                    SelectedItemCode    = _txtItemCode.Text.Trim();
                    SelectedDescription = _txtDescription.Text.Trim();
                    SelectedQuantity    = _numQuantity.Value;
                    SelectedUoM         = _txtUoM.Text.Trim();
                    SelectedUnitPrice   = _numUnitPrice.Value;
                    SelectedStartDate   = _dtpStart.Value.Date;
                    SelectedEndDate     = _dtpEnd.Value.Date;
                };
            }

            private void PopulateList(List<ItemCatalogDto> items)
            {
                _lstItems.DataSource    = null;
                _lstItems.DataSource    = items;
                _lstItems.DisplayMember = "DisplayLabel";
            }
        }
    }
}
