using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Yakult.Inventory.App.Services;

namespace Yakult.Inventory.App.Dialogs
{
    /// <summary>
    /// Inquiry-only Ask AI chat: questions go to the office Ollama server,
    /// answers render in the history. History lives only while open.
    /// </summary>
    public class AskAiDialog : Form
    {
        private readonly RichTextBox _history;
        private readonly TextBox _input;
        private readonly Button _sendButton;
        private readonly List<ChatMessage> _conversation = new List<ChatMessage>();
        private CancellationTokenSource _pendingCts;
        private bool _sending;

        private AskAiDialog()
        {
            string model = Core.AppConfig.OllamaModel;
            string host = Core.AppConfig.OllamaBaseUrl;

            Text = "Ask AI — " + model + " @ " + host;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(760, 560);
            MinimumSize = new Size(560, 420);
            Font = new Font("Segoe UI", 10F);
            BackColor = Color.White;
            KeyPreview = true;

            _history = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 10.5F),
                Margin = new Padding(0),
                HideSelection = false
            };

            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 96, BackColor = Color.White, Padding = new Padding(12, 8, 12, 12) };
            _input = new TextBox
            {
                Multiline = true,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10.5F),
                ScrollBars = ScrollBars.Vertical
            };
            _input.KeyDown += Input_KeyDown;
            _sendButton = new Button
            {
                Text = "Send",
                Dock = DockStyle.Right,
                Width = 96,
                Margin = new Padding(8, 0, 0, 0),
                BackColor = ColorTranslator.FromHtml("#B92335"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _sendButton.FlatAppearance.BorderSize = 0;
            _sendButton.Click += async (_, __) => await SendAsync();
            bottom.Controls.Add(_input);
            bottom.Controls.Add(_sendButton);

            var divider = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = ColorTranslator.FromHtml("#E5E6E8") };

            Controls.Add(_history);
            Controls.Add(divider);
            Controls.Add(bottom);

            AppendLine("AI", "Hi! Ask me anything. I answer from general knowledge only.", ColorTranslator.FromHtml("#677081"), italic: true);
            Shown += (_, __) => _input.Focus();
            FormClosing += (_, __) =>
            {
                try { if (_pendingCts != null) _pendingCts.Cancel(); } catch { }
            };
        }

        public static void Open(IWin32Window owner)
        {
            try
            {
                var dlg = new AskAiDialog();
                if (owner != null) dlg.Show(owner);
                else dlg.Show();
            }
            catch { }
        }

        private void Input_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
                var _ = SendAsync();
            }
        }

        private async Task SendAsync()
        {
            if (_sending) return;
            string question = (_input.Text ?? "").Trim();
            if (question.Length == 0) return;

            _sending = true;
            _sendButton.Enabled = false;
            _input.Clear();
            AppendLine("You", question, ColorTranslator.FromHtml("#202635"), bold: true);
            int thinkingAt = AppendLine("AI", "thinking…", ColorTranslator.FromHtml("#677081"), italic: true);

            _pendingCts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
            var ct = _pendingCts.Token;
            try
            {
                string reply = await Task.Run(() => OllamaChatService.AskAsync(_conversation, question, ct), ct).ConfigureAwait(true);
                ReplaceLine(thinkingAt, "AI", reply.Length == 0 ? "(No reply received.)" : reply, ColorTranslator.FromHtml("#202635"));
                _conversation.Add(new ChatMessage("user", question));
                _conversation.Add(new ChatMessage("assistant", reply));
            }
            catch (OperationCanceledException)
            {
                ReplaceLine(thinkingAt, "AI", "(Cancelled.)", ColorTranslator.FromHtml("#677081"), italic: true);
            }
            catch (Exception ex)
            {
                string hint = ex is System.Net.Http.HttpRequestException
                    ? "Couldn't reach the AI server at " + Core.AppConfig.OllamaBaseUrl + ". Check that Ollama is running and the address is correct."
                    : "Couldn't get an answer: " + ex.Message;
                ReplaceLine(thinkingAt, "AI", hint, Color.Red, italic: true);
            }
            finally
            {
                _sending = false;
                if (!IsDisposed)
                {
                    _sendButton.Enabled = true;
                    _input.Focus();
                }
            }
        }

        private int AppendLine(string who, string text, Color color, bool bold = false, bool italic = false)
        {
            int start = _history.TextLength;
            _history.SelectionStart = start;
            _history.SelectionLength = 0;
            _history.SelectionColor = color;
            _history.SelectionFont = new Font(_history.Font,
                (bold ? FontStyle.Bold : FontStyle.Regular) | (italic ? FontStyle.Italic : FontStyle.Regular));
            _history.AppendText(who + ": " + text + "\r\n\r\n");
            _history.SelectionStart = _history.TextLength;
            _history.ScrollToCaret();
            return start;
        }

        private void ReplaceLine(int start, string who, string text, Color color, bool bold = false, bool italic = true)
        {
            if (IsDisposed || _history.IsDisposed) return;
            try
            {
                _history.SelectionStart = start;
                _history.SelectionLength = _history.TextLength - start;
                _history.SelectionColor = color;
                _history.SelectionFont = new Font(_history.Font,
                    (bold ? FontStyle.Bold : FontStyle.Regular) | (italic ? FontStyle.Italic : FontStyle.Regular));
                _history.SelectedText = who + ": " + text + "\r\n\r\n";
                _history.SelectionStart = _history.TextLength;
                _history.ScrollToCaret();
            }
            catch { }
        }
    }
}
