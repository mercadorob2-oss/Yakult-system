using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Pages.Cartridge;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.WPF.CartridgeManagement.ViewModels;
using WinForms = System.Windows.Forms;

namespace Yakult.Inventory.App.WPF.CartridgeManagement.Views
{
    public partial class ViewCartridgeModelsView : UserControl
    {
        private readonly ViewCartridgeModelsViewModel _vm;
        private readonly CartridgeModelRepository     _repository;

        public ViewCartridgeModelsView()
        {
            InitializeComponent();
            _vm          = new ViewCartridgeModelsViewModel();
            _repository  = new CartridgeModelRepository();
            DataContext   = _vm;
            Loaded       += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _ = _vm.LoadAsync();
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _vm.LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error refreshing data:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.LogError("ViewCartridgeModelsView.BtnRefresh_Click failed", ex);
            }
        }

        private void BtnPrev_Click(object sender, RoutedEventArgs e) => _vm.GoToPrevPage();
        private void BtnNext_Click(object sender, RoutedEventArgs e) => _vm.GoToNextPage();

        // ─── Edit ────────────────────────────────────────────────────────────

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int modelId)
            {
                var win = new EditCartridgeModelWindow(modelId) { Owner = Window.GetWindow(this) };
                if (win.ShowDialog() == true)
                    _ = _vm.LoadAsync();
            }
        }

        // ─── Add ─────────────────────────────────────────────────────────────

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = BuildAddDialog())
            {
                if (dialog.ShowDialog() == WinForms.DialogResult.OK)
                    _ = _vm.LoadAsync();
            }
        }

        // ─── Archive ─────────────────────────────────────────────────────────

        private void BtnArchive_Click(object sender, RoutedEventArgs e)
        {
            var selected = _vm.GetCheckedRows().Select(r => r.Model).ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("Please select at least one model to archive.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string message = selected.Count == 1
                ? $"Are you sure you want to archive \"{selected[0].ModelNumber}\"?"
                : $"Are you sure you want to archive the {selected.Count} selected cartridge models?";

            using (var archiveDialog = new WinForms.Form())
            {
                archiveDialog.Text            = "Archive Cartridge Model";
                archiveDialog.Size            = new System.Drawing.Size(500, 280);
                archiveDialog.StartPosition   = WinForms.FormStartPosition.CenterScreen;
                archiveDialog.FormBorderStyle = WinForms.FormBorderStyle.FixedDialog;
                archiveDialog.MaximizeBox     = false;
                archiveDialog.MinimizeBox     = false;

                var lblMessage = new WinForms.Label
                {
                    Text      = message + "\n\nThe model(s) will be moved to the archive and will no longer appear in active lists.",
                    AutoSize  = false,
                    Size      = new System.Drawing.Size(460, 80),
                    Location  = new System.Drawing.Point(10, 10)
                };

                var lblReason = new WinForms.Label { Text = "Reason for archiving:", AutoSize = true, Location = new System.Drawing.Point(10, 100) };

                var txtReason = new WinForms.TextBox { Size = new System.Drawing.Size(460, 20), Location = new System.Drawing.Point(10, 120) };

                var chkDeactivate = new WinForms.CheckBox
                {
                    Text     = "Also mark as inactive (hide from dropdowns and requests)",
                    AutoSize = true,
                    Location = new System.Drawing.Point(10, 150),
                    Checked  = true
                };

                var btnConfirm = new WinForms.Button { Text = "Archive", DialogResult = WinForms.DialogResult.OK, Location = new System.Drawing.Point(290, 185), Size = new System.Drawing.Size(90, 28) };
                var btnCancel  = new WinForms.Button { Text = "Cancel",  DialogResult = WinForms.DialogResult.Cancel, Location = new System.Drawing.Point(390, 185), Size = new System.Drawing.Size(90, 28) };

                archiveDialog.Controls.AddRange(new WinForms.Control[] { lblMessage, lblReason, txtReason, chkDeactivate, btnConfirm, btnCancel });
                archiveDialog.AcceptButton = btnConfirm;
                archiveDialog.CancelButton = btnCancel;

                if (archiveDialog.ShowDialog() == WinForms.DialogResult.OK)
                {
                    string reason    = string.IsNullOrWhiteSpace(txtReason.Text) ? "No reason provided" : txtReason.Text.Trim();
                    bool   deactivate = chkDeactivate.Checked;
                    _ = ArchiveModelsAsync(selected, reason, deactivate);
                }
            }
        }

        private async Task ArchiveModelsAsync(List<CartridgeModelDto> models, string reason, bool deactivate)
        {
            try
            {
                string archivedBy = AppSession.CurrentUserName ?? "System";
                foreach (var model in models)
                    await _repository.ArchiveAsync(model.CartridgeModelId, reason, deactivate, archivedBy);

                string msg = models.Count == 1
                    ? "Cartridge model archived successfully!"
                    : $"{models.Count} cartridge models archived successfully!";

                MessageBox.Show(msg + "\n\nYou can view archived records in the Archive page.",
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                await _vm.LoadAsync();
            }
            catch (System.Data.SqlClient.SqlException ex)
            {
                MessageBox.Show($"Database error while archiving:\n\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error archiving cartridge model:\n\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ─── Delete ──────────────────────────────────────────────────────────

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            var selected = _vm.GetCheckedRows().Select(r => r.Model).ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("Please select at least one model to delete.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var withDeps    = new List<(CartridgeModelDto model, int itemCount)>();
            var withoutDeps = new List<CartridgeModelDto>();
            var hardBlocked = new List<(CartridgeModelDto model, int emptyCartridgeCount, int batchLineCount)>();

            foreach (var model in selected)
            {
                var (hasItems, itemCount, emptyCartridgeCount, batchLineCount) = await _repository.CheckDependenciesAsync(model.CartridgeModelId);
                if (emptyCartridgeCount > 0 || batchLineCount > 0)
                    hardBlocked.Add((model, emptyCartridgeCount, batchLineCount));
                else if (hasItems)
                    withDeps.Add((model, itemCount));
                else
                    withoutDeps.Add(model);
            }

            if (hardBlocked.Count > 0)
            {
                string blockedList = string.Join("\n", hardBlocked.Select(x =>
                    $"  \"{x.model.ModelNumber}\" — {x.emptyCartridgeCount} EmptyCartridge record(s), {x.batchLineCount} vendor batch line record(s)"));

                MessageBox.Show(
                    $"{hardBlocked.Count} of the selected model(s) cannot be permanently deleted:\n\n{blockedList}\n\n" +
                    "These represent physical inventory / audit history and cannot be auto-unlinked. " +
                    "Resolve those records first (dispose, sell, or return them), or mark the model as Inactive instead.\n\n" +
                    (withDeps.Count + withoutDeps.Count > 0
                        ? $"The remaining {withDeps.Count + withoutDeps.Count} model(s) you selected are not affected — re-run delete with just those selected if you want to proceed with them."
                        : "None of the other selected models are eligible for deletion either."),
                    "Cannot Delete — Linked Inventory Records", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (withDeps.Count > 0)
            {
                int totalItems = withDeps.Sum(x => x.itemCount);

                using (var choiceDialog = new WinForms.Form())
                {
                    choiceDialog.Text            = "Models Have Linked Items";
                    choiceDialog.Size            = new System.Drawing.Size(700, 510);
                    choiceDialog.StartPosition   = WinForms.FormStartPosition.CenterScreen;
                    choiceDialog.FormBorderStyle = WinForms.FormBorderStyle.FixedDialog;
                    choiceDialog.MaximizeBox     = false;
                    choiceDialog.MinimizeBox     = false;

                    var lblWarning = new WinForms.Label
                    {
                        Text     = $"⚠️ WARNING: {withDeps.Count} selected model(s) have linked item records ⚠️\n\n" +
                                   $"Selected models: {selected.Count} total\n" +
                                   $"  {withoutDeps.Count} model(s) with NO linked items\n" +
                                   $"  {withDeps.Count} model(s) WITH linked items ({totalItems} item record(s) total)\n\n" +
                                   $"Deleting a model that has linked items will unlink those items\n" +
                                   $"(their CartridgeModel field will be cleared, but items will NOT be deleted).\n\n" +
                                   $"How do you want to proceed?",
                        AutoSize = false,
                        Size     = new System.Drawing.Size(660, 215),
                        Location = new System.Drawing.Point(15, 10),
                        Font     = new System.Drawing.Font("Segoe UI", 9F)
                    };

                    var btnInactive = new WinForms.Button
                    {
                        Text      = "Option A: Mark as Inactive (Safe)",
                        Location  = new System.Drawing.Point(15, 230),
                        Size      = new System.Drawing.Size(660, 50),
                        BackColor = System.Drawing.Color.FromArgb(52, 152, 219),
                        ForeColor = System.Drawing.Color.White,
                        Font      = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold),
                        TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                        Padding   = new WinForms.Padding(10, 0, 0, 0)
                    };
                    btnInactive.Click += (s, ev) => { choiceDialog.Tag = "INACTIVE"; choiceDialog.DialogResult = WinForms.DialogResult.OK; };

                    var lblInactiveDesc = new WinForms.Label
                    {
                        Text      = "✓ Keeps all data intact  ✓ Maintains audit trail  ✓ Reversible via Edit",
                        Location  = new System.Drawing.Point(25, 285),
                        Size      = new System.Drawing.Size(640, 20),
                        ForeColor = System.Drawing.Color.Gray,
                        Font      = new System.Drawing.Font("Segoe UI", 8F)
                    };

                    var btnForceDelete = new WinForms.Button
                    {
                        Text      = "Option B: Delete Permanently (Unlinks Linked Items)",
                        Location  = new System.Drawing.Point(15, 315),
                        Size      = new System.Drawing.Size(660, 50),
                        BackColor = System.Drawing.Color.FromArgb(231, 76, 60),
                        ForeColor = System.Drawing.Color.White,
                        Font      = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold),
                        TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                        Padding   = new WinForms.Padding(10, 0, 0, 0)
                    };
                    btnForceDelete.Click += (s, ev) => { choiceDialog.Tag = "FORCE_DELETE"; choiceDialog.DialogResult = WinForms.DialogResult.OK; };

                    var lblForceDesc = new WinForms.Label
                    {
                        Text      = "⚠️ Permanently removes the model  ⚠️ Clears CartridgeModel on linked items  ⚠️ Cannot be undone",
                        Location  = new System.Drawing.Point(25, 370),
                        Size      = new System.Drawing.Size(640, 20),
                        ForeColor = System.Drawing.Color.DarkRed,
                        Font      = new System.Drawing.Font("Segoe UI", 8F)
                    };

                    var btnCancelChoice = new WinForms.Button
                    {
                        Text         = "Cancel",
                        DialogResult = WinForms.DialogResult.Cancel,
                        Location     = new System.Drawing.Point(590, 405),
                        Size         = new System.Drawing.Size(90, 28)
                    };

                    choiceDialog.Controls.AddRange(new WinForms.Control[]
                        { lblWarning, btnInactive, lblInactiveDesc, btnForceDelete, lblForceDesc, btnCancelChoice });
                    choiceDialog.CancelButton = btnCancelChoice;

                    if (choiceDialog.ShowDialog() != WinForms.DialogResult.OK) return;

                    string choice = choiceDialog.Tag as string;

                    if (choice == "INACTIVE")
                    {
                        try
                        {
                            foreach (var m in selected)
                            {
                                var dto = await _repository.GetByIdAsync(m.CartridgeModelId);
                                if (dto == null) continue;
                                dto.IsActive = false;
                                await _repository.UpdateAsync(dto);
                            }
                            MessageBox.Show($"{selected.Count} model(s) marked as inactive.",
                                "Done", MessageBoxButton.OK, MessageBoxImage.Information);
                            await _vm.LoadAsync();
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error marking models inactive:\n\n{ex.Message}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        return;
                    }

                    if (choice == "FORCE_DELETE")
                    {
                        string finalMsg = selected.Count == 1
                            ? $"FINAL WARNING\n\nYou are about to permanently delete:\n  \"{selected[0].ModelNumber}\"\n\n" +
                              $"This will also unlink {totalItems} item record(s).\n\nThis CANNOT be undone. Proceed?"
                            : $"FINAL WARNING\n\nYou are about to permanently delete {selected.Count} cartridge models.\n\n" +
                              $"This will unlink {totalItems} item record(s). This CANNOT be undone. Proceed?";

                        var confirm = MessageBox.Show(finalMsg, "Final Warning — Cannot Be Undone",
                            MessageBoxButton.YesNo, MessageBoxImage.Stop);

                        if (confirm != MessageBoxResult.Yes) return;

                        await DeleteModelsAsync(selected);
                    }
                }
                return;
            }

            // No dependencies — simple confirmation
            string simpleMsg = selected.Count == 1
                ? $"Permanently delete \"{selected[0].ModelNumber}\"?\n\nThis cannot be undone."
                : $"Permanently delete {selected.Count} cartridge models?\n\nThis cannot be undone.";

            if (MessageBox.Show(simpleMsg, "Confirm Delete",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                await DeleteModelsAsync(selected);
            }
        }

        private async Task DeleteModelsAsync(List<CartridgeModelDto> models)
        {
            try
            {
                foreach (var model in models)
                    await _repository.DeleteAsync(model.CartridgeModelId);

                string msg = models.Count == 1 ? "Cartridge model deleted." : $"{models.Count} cartridge models deleted.";
                MessageBox.Show(msg, "Deleted", MessageBoxButton.OK, MessageBoxImage.Information);
                await _vm.LoadAsync();
            }
            catch (System.Data.SqlClient.SqlException ex)
            {
                MessageBox.Show($"Database error while deleting:\n\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting cartridge model:\n\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ─── Add model dialog (inline, same logic as original WinForms page) ──

        private static WinForms.Form BuildAddDialog()
        {
            var dialog = new WinForms.Form
            {
                Text            = "Add Cartridge Model",
                Size            = new System.Drawing.Size(420, 270),
                StartPosition   = WinForms.FormStartPosition.CenterScreen,
                FormBorderStyle = WinForms.FormBorderStyle.FixedDialog,
                MaximizeBox     = false,
                MinimizeBox     = false
            };

            var mainPanel = new WinForms.TableLayoutPanel
            {
                Dock        = WinForms.DockStyle.Fill,
                ColumnCount = 2,
                RowCount    = 4,
                Padding     = new WinForms.Padding(20)
            };
            mainPanel.ColumnStyles.Add(new WinForms.ColumnStyle(WinForms.SizeType.Absolute, 120F));
            mainPanel.ColumnStyles.Add(new WinForms.ColumnStyle(WinForms.SizeType.Percent, 100F));
            dialog.Controls.Add(mainPanel);

            var lblModelNumber = new WinForms.Label { Text = "Model Number *", Dock = WinForms.DockStyle.Fill };
            var txtModelNumber = new WinForms.TextBox { Dock = WinForms.DockStyle.Fill };
            mainPanel.Controls.Add(lblModelNumber, 0, 0);
            mainPanel.Controls.Add(txtModelNumber, 1, 0);

            var chkRequestable = new WinForms.CheckBox { Text = "Is Requestable", Checked = true, Dock = WinForms.DockStyle.Fill };
            mainPanel.Controls.Add(new WinForms.Label(), 0, 1);
            mainPanel.Controls.Add(chkRequestable, 1, 1);

            var chkRefillable = new WinForms.CheckBox { Text = "Is Refillable", Checked = true, Dock = WinForms.DockStyle.Fill };
            mainPanel.Controls.Add(new WinForms.Label(), 0, 2);
            mainPanel.Controls.Add(chkRefillable, 1, 2);

            var buttonPanel = new WinForms.FlowLayoutPanel
            {
                FlowDirection = WinForms.FlowDirection.RightToLeft,
                Dock          = WinForms.DockStyle.Fill,
                AutoSize      = true
            };

            var btnSave   = new WinForms.Button { Text = "Save",   Width = 80, DialogResult = WinForms.DialogResult.None };
            var btnCancel = new WinForms.Button { Text = "Cancel", Width = 80, DialogResult = WinForms.DialogResult.Cancel };

            btnSave.Click += async (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtModelNumber.Text))
                {
                    WinForms.MessageBox.Show("Please enter a model number.", "Validation Error",
                        WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Warning);
                    return;
                }

                try
                {
                    btnSave.Enabled = false;
                    btnSave.Text    = "Saving...";

                    var repo     = new CartridgeModelRepository();
                    var existing = await repo.FindByModelNumberAsync(txtModelNumber.Text.Trim());
                    if (existing != null)
                    {
                        WinForms.MessageBox.Show($"Model number '{txtModelNumber.Text.Trim()}' already exists.",
                            "Duplicate", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Warning);
                        btnSave.Enabled = true;
                        btnSave.Text    = "Save";
                        return;
                    }

                    var model = new CartridgeModelDto
                    {
                        ModelNumber   = txtModelNumber.Text.Trim(),
                        IsRequestable = chkRequestable.Checked,
                        IsRefillable  = chkRefillable.Checked,
                        IsActive      = true,
                        CreatedBy     = AppSession.CurrentUserId,
                        CreatedAt     = DateTime.Now
                    };

                    await repo.CreateAsync(model);
                    WinForms.MessageBox.Show("Cartridge model added successfully!", "Success",
                        WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);

                    dialog.DialogResult = WinForms.DialogResult.OK;
                    dialog.Close();
                }
                catch (Exception ex)
                {
                    WinForms.MessageBox.Show($"Error: {ex.Message}", "Error",
                        WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
                    btnSave.Enabled = true;
                    btnSave.Text    = "Save";
                }
            };

            buttonPanel.Controls.Add(btnSave);
            buttonPanel.Controls.Add(btnCancel);
            mainPanel.Controls.Add(buttonPanel, 0, 3);
            mainPanel.SetColumnSpan(buttonPanel, 2);

            dialog.AcceptButton = btnSave;
            dialog.CancelButton = btnCancel;

            return dialog;
        }
    }
}
