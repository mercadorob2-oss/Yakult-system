using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Yakult.Inventory.App.Repositories;

namespace Yakult.Inventory.App.Pages.Set
{
    // Shown right after a batch of requests is saved (Batch Add Request / Bulk Add Request)
    // so the user can immediately group those requests into a Set instead of having to
    // find them again later from the Set management screens.
    public class AddRequestsToSetDialog : Form
    {
        private RadioButton _rbNewSet;
        private RadioButton _rbExistingSet;
        private ComboBox _cmbExistingSet;
        private Button _btnOk;
        private Button _btnCancel;
        private readonly List<SetDto> _sets = new List<SetDto>();

        public int? ResultSetId { get; private set; }

        public AddRequestsToSetDialog(int requestCount)
        {
            const int margin = 20;

            Text = "Add to Set";
            ClientSize = new Size(420, 236);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = new Font("Segoe UI", 9.5F);
            Padding = new Padding(margin);

            var lblInfo = new Label
            {
                Text = $"Add {requestCount} request(s) to a Set:",
                AutoSize = true,
                Location = new Point(margin, margin)
            };

            _rbNewSet = new RadioButton
            {
                Text = "Create a new Set",
                Checked = true,
                AutoSize = true,
                Location = new Point(margin, lblInfo.Bottom + 20)
            };
            _rbExistingSet = new RadioButton
            {
                Text = "Add to an existing Set",
                AutoSize = true,
                Location = new Point(margin, _rbNewSet.Bottom + 12)
            };
            _rbNewSet.CheckedChanged += (s, e) =>
            {
                _cmbExistingSet.Enabled = !_rbNewSet.Checked;
            };

            _cmbExistingSet = new ComboBox
            {
                Location = new Point(margin + 20, _rbExistingSet.Bottom + 8),
                Width = ClientSize.Width - (margin + 20) - margin,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Enabled = false
            };

            _btnOk = new Button
            {
                Text = "OK",
                Width = 90,
                Height = 30,
                Location = new Point(ClientSize.Width - margin - 90 - 12 - 90, ClientSize.Height - margin - 30)
            };
            _btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Width = 90,
                Height = 30,
                Location = new Point(ClientSize.Width - margin - 90, ClientSize.Height - margin - 30)
            };
            _btnOk.Click += BtnOk_Click;

            Controls.AddRange(new Control[] { lblInfo, _rbNewSet, _rbExistingSet, _cmbExistingSet, _btnOk, _btnCancel });
            AcceptButton = _btnOk;
            CancelButton = _btnCancel;

            Load += async (s, e) => await LoadSetsAsync();
        }

        private async System.Threading.Tasks.Task LoadSetsAsync()
        {
            try
            {
                var repo = new SetRepository();
                var sets = await repo.GetAllSetsAsync();

                _sets.Clear();
                // Only surface Sets that haven't been dispatched yet — attaching more
                // requests to an already-dispatched Set would be a data-integrity footgun.
                _sets.AddRange(sets
                    .Where(x => string.Equals(x.Status, "Pending", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(x => x.SetId));

                _cmbExistingSet.DataSource = null;
                _cmbExistingSet.DataSource = _sets;
                _cmbExistingSet.DisplayMember = "SetCode";

                if (_sets.Count == 0)
                {
                    _rbExistingSet.Enabled = false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load existing sets: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void BtnOk_Click(object sender, EventArgs e)
        {
            _btnOk.Enabled = false;
            try
            {
                if (_rbExistingSet.Checked)
                {
                    if (!(_cmbExistingSet.SelectedItem is SetDto set))
                    {
                        MessageBox.Show("Please select a Set.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    ResultSetId = set.SetId;
                }
                else
                {
                    // Reuse the exact same "Add Master Data → Request Set" dialog so a Set
                    // created from here looks and behaves identically either way.
                    using (var addDialog = new AddSetPage(showStatus: false))
                    {
                        if (addDialog.ShowDialog(this) != DialogResult.OK || !(addDialog.Tag is int newSetId) || newSetId <= 0)
                            return;

                        ResultSetId = newSetId;
                    }
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            finally
            {
                _btnOk.Enabled = true;
            }
        }
    }
}
