using System;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.WPF.CartridgeManagement.ViewModels;
using Yakult.Inventory.App.Pages.Set;
using WinForms = System.Windows.Forms;

namespace Yakult.Inventory.App.WPF.CartridgeManagement.Views
{
    public partial class ViewCartridgeSetsView : UserControl
    {
        private readonly ViewCartridgeSetsViewModel _vm;
        private readonly SetRepository _repository = new SetRepository();

        public ViewCartridgeSetsView()
        {
            InitializeComponent();
            _vm        = new ViewCartridgeSetsViewModel();
            DataContext = _vm;
            Loaded     += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _ = _vm.LoadAsync();
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            try   { await _vm.LoadAsync(); }
            catch (Exception ex)
            {
                MessageBox.Show($"Error refreshing data:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.LogError("ViewCartridgeSetsView.BtnRefresh_Click failed", ex);
            }
        }

        private void BtnOpen_Click(object sender, RoutedEventArgs e)
        {
            var set = _vm.SelectedSet;
            if (set == null) { MessageBox.Show("Select a set first.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            OpenSetDetail(set.SetId);
        }

        private void BtnRowOpen_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int setId)
                OpenSetDetail(setId);
        }

        private void BtnArchive_Click(object sender, RoutedEventArgs e)
        {
            var set = _vm.SelectedSet;
            if (set == null) { MessageBox.Show("Select a set first.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information); return; }

            using (var dlg = new WinForms.Form())
            {
                dlg.Text = "Archive Set";
                dlg.Size = new System.Drawing.Size(480, 280);
                dlg.StartPosition = WinForms.FormStartPosition.CenterParent;
                dlg.FormBorderStyle = WinForms.FormBorderStyle.FixedDialog;
                dlg.MaximizeBox = dlg.MinimizeBox = false;

                var lblMsg = new WinForms.Label
                {
                    Text = $"Archive set \"{set.SetCode}\"?\n\nStatus: {set.SetStatus}\nItems: {set.ItemCount}",
                    AutoSize = false, Size = new System.Drawing.Size(440, 60),
                    Location = new System.Drawing.Point(16, 16)
                };

                var lblReason = new WinForms.Label { Text = "Reason:", AutoSize = true, Location = new System.Drawing.Point(16, 90) };
                var txtReason = new WinForms.TextBox { Size = new System.Drawing.Size(440, 24), Location = new System.Drawing.Point(16, 110) };

                var chkItems = new WinForms.CheckBox { Text = "Also archive all items in this set", AutoSize = true, Location = new System.Drawing.Point(16, 145), Checked = true };
                var chkDeact = new WinForms.CheckBox { Text = "Mark archived items as inactive", AutoSize = true, Location = new System.Drawing.Point(16, 168), Checked = true };

                var btnOk  = new WinForms.Button { Text = "Archive", DialogResult = WinForms.DialogResult.OK,  Location = new System.Drawing.Point(280, 200), Size = new System.Drawing.Size(80, 26) };
                var btnCxl = new WinForms.Button { Text = "Cancel",  DialogResult = WinForms.DialogResult.Cancel, Location = new System.Drawing.Point(370, 200), Size = new System.Drawing.Size(80, 26) };

                dlg.Controls.AddRange(new WinForms.Control[] { lblMsg, lblReason, txtReason, chkItems, chkDeact, btnOk, btnCxl });
                dlg.AcceptButton = btnOk; dlg.CancelButton = btnCxl;

                if (dlg.ShowDialog() == WinForms.DialogResult.OK)
                {
                    string reason = string.IsNullOrWhiteSpace(txtReason.Text) ? "No reason provided" : txtReason.Text;
                    _ = ArchiveSetAsync(set.SetId, reason, chkItems.Checked, chkDeact.Checked);
                }
            }
        }

        private async System.Threading.Tasks.Task ArchiveSetAsync(int setId, string reason, bool archiveItems, bool deactivateItems)
        {
            try
            {
                await System.Threading.Tasks.Task.Run(() =>
                {
                    using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
                    {
                        con.Open();
                        using (var tx = con.BeginTransaction())
                        {
                            try
                            {
                                const string insertSql = @"
                                    INSERT INTO ArchiveStatus (EntityType, EntityId, IsArchived, ArchivedAt, ArchivedBy, ArchiveReason)
                                    VALUES ('Set', @SetId, 1, GETDATE(), @ArchivedBy, @ArchiveReason)";

                                using (var cmd = new SqlCommand(insertSql, con, tx))
                                {
                                    cmd.Parameters.AddWithValue("@SetId",        setId);
                                    cmd.Parameters.AddWithValue("@ArchivedBy",   AppSession.CurrentUserName ?? "System");
                                    cmd.Parameters.AddWithValue("@ArchiveReason", reason);
                                    cmd.ExecuteNonQuery();
                                }

                                if (archiveItems)
                                {
                                    const string archiveItemsSql = @"
                                        INSERT INTO ArchiveStatus (EntityType, EntityId, IsArchived, ArchivedAt, ArchivedBy, ArchiveReason)
                                        SELECT 'Item', r.ItemId, 1, GETDATE(), @ArchivedBy,
                                               'Archived with Set ' + CAST(@SetId AS NVARCHAR(20))
                                        FROM dbo.Request r
                                        WHERE r.SetId = @SetId
                                          AND NOT EXISTS (
                                              SELECT 1 FROM ArchiveStatus
                                              WHERE EntityType = 'Item' AND EntityId = r.ItemId)";

                                    using (var cmd = new SqlCommand(archiveItemsSql, con, tx))
                                    {
                                        cmd.Parameters.AddWithValue("@SetId",      setId);
                                        cmd.Parameters.AddWithValue("@ArchivedBy", AppSession.CurrentUserName ?? "System");
                                        cmd.ExecuteNonQuery();
                                    }

                                    if (deactivateItems)
                                    {
                                        const string deactSql = @"
                                            UPDATE i SET i.Active = 0
                                            FROM dbo.Item i
                                            INNER JOIN dbo.Request r ON i.ItemId = r.ItemId
                                            WHERE r.SetId = @SetId";

                                        using (var cmd = new SqlCommand(deactSql, con, tx))
                                        {
                                            cmd.Parameters.AddWithValue("@SetId", setId);
                                            cmd.ExecuteNonQuery();
                                        }
                                    }
                                }

                                tx.Commit();
                            }
                            catch
                            {
                                tx.Rollback();
                                throw;
                            }
                        }
                    }
                });

                WinForms.MessageBox.Show("Set archived successfully.", "Success",
                    WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
                await _vm.LoadAsync();
            }
            catch (Exception ex)
            {
                WinForms.MessageBox.Show($"Error archiving set:\n{ex.Message}", "Error",
                    WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
            }
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            var set = _vm.SelectedSet;
            if (set == null) { MessageBox.Show("Select a set first.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information); return; }

            var first = WinForms.MessageBox.Show(
                $"⚠ PERMANENT DELETE WARNING ⚠\n\n" +
                $"Set Code: {set.SetCode}\nItems: {set.ItemCount}\n\n" +
                "All items will have their stock restored. This cannot be undone.\n\n" +
                "Use 'Archive' instead for normal records.\n\nContinue?",
                "Confirm Permanent Deletion",
                WinForms.MessageBoxButtons.YesNo, WinForms.MessageBoxIcon.Warning,
                WinForms.MessageBoxDefaultButton.Button2);

            if (first != WinForms.DialogResult.Yes) return;

            var second = WinForms.MessageBox.Show(
                "FINAL CONFIRMATION\n\nAre you absolutely certain?",
                "Final Confirmation",
                WinForms.MessageBoxButtons.YesNo, WinForms.MessageBoxIcon.Exclamation,
                WinForms.MessageBoxDefaultButton.Button2);

            if (second != WinForms.DialogResult.Yes) return;

            try
            {
                bool ok = await _repository.DeleteSetAndRestoreStock(set.SetId);
                if (ok)
                {
                    WinForms.MessageBox.Show("Set permanently deleted and stock restored.", "Deleted",
                        WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
                    await _vm.LoadAsync();
                }
            }
            catch (Exception ex)
            {
                WinForms.MessageBox.Show($"Error deleting set:\n{ex.Message}", "Error",
                    WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
            }
        }

        private void OpenSetDetail(int setId)
        {
            using (var detail = new ViewSetDetailPage(setId))
            {
                detail.ShowDialog();
                _ = _vm.LoadAsync();
            }
        }

        private void BtnPrev_Click(object sender, RoutedEventArgs e) => _vm.GoToPrevPage();
        private void BtnNext_Click(object sender, RoutedEventArgs e) => _vm.GoToNextPage();
    }
}
