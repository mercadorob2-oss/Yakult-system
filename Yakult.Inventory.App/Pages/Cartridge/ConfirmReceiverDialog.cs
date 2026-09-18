using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Yakult.Inventory.App.Forms.CartridgeManagement;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Pages.Cartridge
{
    /// <summary>
    /// Dialog that appears after a cartridge fulfillment is committed.
    /// Allows IT staff to confirm (or change) who physically receives the cartridges
    /// before the SMTP fulfillment notification is sent.
    ///
    /// BEHAVIOR:
    ///   - For PICKUP requests: shows a "Received By" ComboBox pre-filled with the
    ///     employee designated in the portal.  IT must confirm or select someone else.
    ///   - For DELIVERY requests: the receiver section is hidden; IT just confirms
    ///     that the email should be sent.
    ///   - Clicking "Confirm & Send" saves the receiver to the DB (if changed) and
    ///     fires the email-sending delegate.
    ///   - Clicking "Cancel" closes the dialog without persisting changes or sending email.
    /// </summary>
    public sealed class ConfirmReceiverDialog : Form
    {
        // ── Dependencies ─────────────────────────────────────────────────────────────
        private readonly CartridgeManagementRepository _repo;
        private readonly CartridgeRequestDto _request;
        private readonly string _fulfillmentMethod;   // "PICKUP" or "DELIVERY" (or null)
        private readonly Action _onConfirm;           // Delegate that sends the email

        // ── Employee list ─────────────────────────────────────────────────────────────
        private List<(int EmpId, string Name, string EmployeeNumber, string Position, string BranchName, string DepartmentName)> _employees;
        private int? _originalReceivedById;

        // ── Controls ─────────────────────────────────────────────────────────────────
        private ComboBox _cmbReceiver;
        private Panel    _receiverPanel;
        private Button   _btnConfirm;
        private Button   _btnCancel;

        /// <summary>
        /// Creates the dialog.
        /// </summary>
        /// <param name="request">The selected request (used to look up session/receiver info).</param>
        /// <param name="fulfillmentMethod">"PICKUP", "DELIVERY", or null.</param>
        /// <param name="onConfirm">Delegate invoked when IT clicks "Confirm &amp; Send".</param>
        public ConfirmReceiverDialog(
            CartridgeRequestDto request,
            string fulfillmentMethod,
            Action onConfirm)
        {
            _repo              = new CartridgeManagementRepository();
            _request           = request ?? throw new ArgumentNullException(nameof(request));
            _fulfillmentMethod = fulfillmentMethod;
            _onConfirm         = onConfirm ?? throw new ArgumentNullException(nameof(onConfirm));

            InitializeComponent();
            BuildUi();
            LoadData();
        }

        // ── Form init ────────────────────────────────────────────────────────────────
        private void InitializeComponent()
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode       = AutoScaleMode.Font;
            ClientSize          = new Size(520, 380);
            FormBorderStyle     = FormBorderStyle.FixedDialog;
            MaximizeBox         = false;
            MinimizeBox         = false;
            StartPosition       = FormStartPosition.CenterParent;
            Text                = "Confirm Receiver – Send Notification";
            Font                = new Font("Segoe UI", 9F);
            BackColor           = Color.White;
            ResumeLayout(false);
        }

        // ── UI construction ──────────────────────────────────────────────────────────
        private void BuildUi()
        {
            Controls.Clear();

            int lw   = 130;   // label column width
            int cw   = 330;   // control column width
            int lm   = 20;    // left margin
            int top  = 20;
            int rowH = 38;

            // Header
            var header = new Label
            {
                Text      = "Confirm Receiver / Send Notification",
                Font      = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 122, 204),
                Location  = new Point(lm, top),
                AutoSize  = true
            };
            Controls.Add(header);
            top += 40;

            // Divider
            var divider = new Panel
            {
                Location  = new Point(lm, top),
                Size      = new Size(lw + cw, 1),
                BackColor = Color.FromArgb(220, 220, 220)
            };
            Controls.Add(divider);
            top += 10;

            // Request info rows
            top = AddInfoRow("Requester:",   _request.EmployeeName  ?? "N/A", lm, top, lw, cw, rowH);
            top = AddInfoRow("Branch:",      _request.BranchName    ?? "N/A", lm, top, lw, cw, rowH);
            top = AddInfoRow("Department:",  _request.DepartmentName ?? "N/A", lm, top, lw, cw, rowH);

            bool isPickup = string.Equals(_fulfillmentMethod, "PICKUP", StringComparison.OrdinalIgnoreCase);
            string methodText = string.IsNullOrWhiteSpace(_fulfillmentMethod) ? "N/A" : _fulfillmentMethod;
            top = AddInfoRow("Fulfillment:", methodText, lm, top, lw, cw, rowH);
            top += 5;

            // Divider 2
            var divider2 = new Panel
            {
                Location  = new Point(lm, top),
                Size      = new Size(lw + cw, 1),
                BackColor = Color.FromArgb(220, 220, 220)
            };
            Controls.Add(divider2);
            top += 10;

            // Receiver panel (shown only for PICKUP)
            _receiverPanel = new Panel
            {
                Location = new Point(lm, top),
                Size     = new Size(lw + cw + 20, rowH + 30),
                Visible  = isPickup
            };

            var lblReceiver = new Label
            {
                Text     = "Received By:",
                Font     = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location = new Point(0, 8),
                Size     = new Size(lw, 20)
            };
            _receiverPanel.Controls.Add(lblReceiver);

            _cmbReceiver = new ComboBox
            {
                Location      = new Point(lw + 5, 4),
                Size          = new Size(cw, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _receiverPanel.Controls.Add(_cmbReceiver);

            var lblHint = new Label
            {
                Text      = "Select who physically collected the cartridges.",
                ForeColor = Color.Gray,
                Location  = new Point(lw + 5, 32),
                AutoSize  = true,
                Font      = new Font("Segoe UI", 8F)
            };
            _receiverPanel.Controls.Add(lblHint);

            Controls.Add(_receiverPanel);

            if (isPickup)
                top += _receiverPanel.Height + 10;
            else
            {
                // For DELIVERY, show a note
                var lblDeliveryNote = new Label
                {
                    Text      = "This is a DELIVERY request. No receiver confirmation required.",
                    ForeColor = Color.FromArgb(100, 100, 100),
                    Location  = new Point(lm, top),
                    AutoSize  = true,
                    Font      = new Font("Segoe UI", 9F, FontStyle.Italic)
                };
                Controls.Add(lblDeliveryNote);
                top += 30;
            }

            top += 10;

            // Buttons
            _btnConfirm = new Button
            {
                Text      = "✔  Confirm & Send Notification",
                Location  = new Point(lm, top),
                Size      = new Size(230, 36),
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor    = Cursors.Hand
            };
            _btnConfirm.FlatAppearance.BorderSize = 0;
            _btnConfirm.Click += BtnConfirm_Click;
            Controls.Add(_btnConfirm);

            _btnCancel = new Button
            {
                Text      = "Cancel (Don't Send)",
                Location  = new Point(lm + 240, top),
                Size      = new Size(165, 36),
                BackColor = Color.FromArgb(240, 240, 240),
                ForeColor = Color.FromArgb(80, 80, 80),
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 9.5F),
                Cursor    = Cursors.Hand
            };
            _btnCancel.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
            _btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(_btnCancel);

            // Resize form to content
            ClientSize = new Size(ClientSize.Width, top + 36 + 20);
        }

        private int AddInfoRow(string label, string value, int lm, int top, int lw, int cw, int rowH)
        {
            var lbl = new Label
            {
                Text     = label,
                Font     = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location = new Point(lm, top + 4),
                Size     = new Size(lw, 20)
            };
            var val = new Label
            {
                Text     = value,
                Location = new Point(lm + lw + 5, top + 4),
                Size     = new Size(cw, 20),
                AutoSize = false
            };
            Controls.Add(lbl);
            Controls.Add(val);
            return top + rowH;
        }

        // ── Data loading ─────────────────────────────────────────────────────────────
        private void LoadData()
        {
            bool isPickup = string.Equals(_fulfillmentMethod, "PICKUP", StringComparison.OrdinalIgnoreCase);
            if (!isPickup)
                return;

            // Load employee list
            _employees = _repo.GetActiveEmployeesForReceiver();

            _cmbReceiver.Items.Clear();
            _cmbReceiver.Items.Add(new EmployeeComboItem(0, "— Select receiver —"));

            foreach (var emp in _employees)
            {
                var empNum  = !string.IsNullOrWhiteSpace(emp.EmployeeNumber) ? $" ({emp.EmployeeNumber})" : "";
                var deptPart = !string.IsNullOrWhiteSpace(emp.DepartmentName) ? $" – {emp.DepartmentName}" : "";
                _cmbReceiver.Items.Add(new EmployeeComboItem(emp.EmpId, $"{emp.Name}{empNum}{deptPart}"));
            }

            // Pre-select the receiver stored in the portal request
            try
            {
                if (_request.SubmissionSessionId.HasValue)
                    _originalReceivedById = _repo.GetReceivedByIdForSession(_request.SubmissionSessionId.Value);

                if (!_originalReceivedById.HasValue)
                    _originalReceivedById = _repo.GetReceivedByIdForRequest(_request.ReqId);
            }
            catch
            {
                _originalReceivedById = null;
            }

            if (_originalReceivedById.HasValue)
            {
                for (int i = 0; i < _cmbReceiver.Items.Count; i++)
                {
                    if (_cmbReceiver.Items[i] is EmployeeComboItem item && item.EmpId == _originalReceivedById.Value)
                    {
                        _cmbReceiver.SelectedIndex = i;
                        break;
                    }
                }
            }

            if (_cmbReceiver.SelectedIndex < 0)
                _cmbReceiver.SelectedIndex = 0;
        }

        // ── Button handlers ──────────────────────────────────────────────────────────
        private void BtnConfirm_Click(object sender, EventArgs e)
        {
            bool isPickup = string.Equals(_fulfillmentMethod, "PICKUP", StringComparison.OrdinalIgnoreCase);

            if (isPickup)
            {
                // Validate selection
                if (!(_cmbReceiver.SelectedItem is EmployeeComboItem selected) || selected.EmpId <= 0)
                {
                    MessageBox.Show("Please select who will receive the cartridges.",
                        "Receiver Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                int confirmedId = selected.EmpId;

                // Persist to DB if the receiver changed or was not set
                bool changed = confirmedId != (_originalReceivedById ?? 0);
                if (changed)
                {
                    try
                    {
                        int userId = AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : 1;

                        if (_request.SubmissionSessionId.HasValue)
                            _repo.UpdateReceivedByForSession(_request.SubmissionSessionId.Value, confirmedId, userId);
                        else
                            _repo.UpdateReceivedByForRequest(_request.ReqId, confirmedId, userId);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Could not save receiver change:\n\n{ex.Message}",
                            "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        // Non-fatal: still send the email
                    }
                }
            }

            // Fire the email-sending delegate
            DialogResult = DialogResult.OK;
            Close();

            try
            {
                _onConfirm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Notification could not be sent:\n\n{ex.Message}",
                    "Email Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // ── Helper ───────────────────────────────────────────────────────────────────
        private sealed class EmployeeComboItem
        {
            public int    EmpId { get; }
            public string Label { get; }

            public EmployeeComboItem(int empId, string label)
            {
                EmpId = empId;
                Label = label;
            }

            public override string ToString() => Label;
        }
    }
}
