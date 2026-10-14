// Headless UI checks: no windows are shown, no database operations are performed.
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Data;
using Yakult.Inventory.App.Pages.User;
using Yakult.Inventory.App.Session;

class AccountLoginPreview
{
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
    static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    static void Check(bool pass, string message) { if (!pass) throw new Exception(message); Console.WriteLine("PASS: " + message); }

    [STAThread]
    static int Main(string[] args)
    {
        try
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            DatabaseConfig.Bootstrap("Data Source=(local);Initial Catalog=PreviewOnly;Integrated Security=True");
            AppSession.Clear();
            var repo = new UserRepository(DatabaseConfig.ConnectionString);
            using (var login = new LoginPage(repo))
            {
                Draw(login, Path.Combine(args[0], "login.png"));
                var name = Field<Control>(login, "NameLoginField");
                var password = Field<Control>(login, "PassLoginField");
                var toggle = Field<Button>(login, "_passwordToggle");
                Call(login, "LoginBtn_Click", login, EventArgs.Empty);
                Check(Field<Label>(login, "_loginStatus").Text.Contains("username and password"), "Empty login shows inline validation without contacting the database");
                Check(name.TabStop && password.TabStop && toggle.TabStop, "Sign-in inputs and password toggle support keyboard navigation");
                password.Text = "Preview only";
                Call(login, "lblTogglePassword_Click", toggle, EventArgs.Empty);
                Check(toggle.Text == "Hide" && (char)password.GetType().GetProperty("PasswordChar").GetValue(password) == '\0', "Show password reveals the existing password field");
                Call(login, "lblTogglePassword_Click", toggle, EventArgs.Empty);
                Check(toggle.Text == "Show" && (char)password.GetType().GetProperty("PasswordChar").GetValue(password) != '\0', "Hide password restores masking");
                password.Text = "";
                login.ClientSize = new Size(850, 560);
                Draw(login, Path.Combine(args[0], "login-small.png"));
                foreach (var field in new[] { name, password, Field<Control>(login, "LoginBtn") })
                    Check(Inside(login, field), "Sign-in control remains inside the window: " + field.Name);
            }

            foreach (float scale in new[] { 1F, 1.25F, 1.5F })
            using (var login = new LoginPage(repo))
            {
                login.PerformAutoScale();
                login.Scale(new SizeF(scale, scale));
                Draw(login, Path.Combine(args[0], "login-scale-" + scale.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + ".png"));
                foreach (string name in new[] { "NameLoginField", "PassLoginField", "LoginBtn", "lblContactAdmin", "lblBrandTitle", "lblBrandSub" })
                    Check(Unclipped(Field<Control>(login, name)), "Login content is not clipped at scale " + scale + ": " + name);
                foreach (Control control in Descendants(login))
                    if (control is LinkLabel) Check(Unclipped(control), "Support link is not clipped at scale " + scale);
            }

            using (var host = new Form { Text = "My account · Yakult Workspace", ClientSize = new Size(1000, 660) })
            using (var page = new MyAccountPage())
            {
                host.Controls.Add(page);
                Set(page, "_profileLoaded", true);
                Set(page, "_originalEmail", "russel@example.test");
                Field<TextBox>(page, "_txtUsername").Text = "Russel";
                Field<TextBox>(page, "_txtEmployee").Text = "Russel Mercado";
                Field<TextBox>(page, "_txtRoles").Text = "Developer, Admin";
                Field<TextBox>(page, "_txtStatus").Text = "Active";
                Field<Label>(page, "_profileMessage").Text = "Your email is used for account notifications.";
                Field<Button>(page, "_btnChangePassword").Enabled = true;
                var email = Field<TextBox>(page, "_txtEmail");
                email.Enabled = true;
                email.Text = "russel@example.test";
                Check(!Field<Button>(page, "_btnSave").Enabled, "Unchanged email cannot be saved");
                email.Text = "bad-email";
                Check(!Field<Button>(page, "_btnSave").Enabled && Field<Label>(page, "_lblEmailError").Text.Length > 0, "Invalid email is rejected inline");
                email.Text = "updated@example.test";
                Check(Field<Button>(page, "_btnSave").Enabled && Field<Button>(page, "_btnCancel").Enabled, "Valid email changes enable Save and Discard");
                Call(Field<Button>(page, "_btnCancel"), "OnClick", EventArgs.Empty);
                Check(email.Text == "russel@example.test" && !Field<Button>(page, "_btnSave").Enabled, "Discard restores the loaded email locally");
                Draw(host, Path.Combine(args[0], "my-account.png"));
                Check(Inside(host, email) && Inside(host, Field<Button>(page, "_btnSave")) && Inside(host, Field<Button>(page, "_btnChangePassword")), "Account edit and security actions fit the dialog");
                host.ClientSize = new Size(864, 581);
                Draw(host, Path.Combine(args[0], "my-account-small.png"));
            }

            using (var passwordDialog = new ChangePasswordDialog(repo))
            {
                Check(!Field<Button>(passwordDialog, "btnChange").Enabled, "Password update starts disabled");
                Field<TextBox>(passwordDialog, "txtCurrentPassword").Text = "Old preview 1!";
                Field<TextBox>(passwordDialog, "txtNewPassword").Text = "New preview 2!";
                Field<TextBox>(passwordDialog, "txtConfirmPassword").Text = "Mismatch";
                Check(!Field<Button>(passwordDialog, "btnChange").Enabled, "Mismatched password confirmation stays disabled");
                Field<TextBox>(passwordDialog, "txtConfirmPassword").Text = "New preview 2!";
                Check(Field<Button>(passwordDialog, "btnChange").Enabled, "Valid matching password enables the existing update action");
                Field<TextBox>(passwordDialog, "txtCurrentPassword").Clear();
                Field<TextBox>(passwordDialog, "txtNewPassword").Clear();
                Field<TextBox>(passwordDialog, "txtConfirmPassword").Clear();
                Draw(passwordDialog, Path.Combine(args[0], "change-password.png"));
            }
            Console.WriteLine("Login and account UI checks passed; no credentials authenticated or saved.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    static bool Inside(Form form, Control control)
    {
        Point origin = Point.Empty;
        for (Control current = control; current != form; current = current.Parent)
        { if (current == null) return false; origin.Offset(current.Left, current.Top); }
        return origin.X >= 0 && origin.Y >= 0 && origin.X + control.Width <= form.ClientSize.Width && origin.Y + control.Height <= form.ClientSize.Height;
    }

    static System.Collections.Generic.IEnumerable<Control> Descendants(Control control)
    {
        foreach (Control child in control.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    static bool Unclipped(Control control)
    {
        var bounds = control.ClientRectangle;
        for (Control current = control; current.Parent != null; current = current.Parent)
        {
            bounds.Offset(current.Left, current.Top);
            if (!current.Parent.ClientRectangle.Contains(bounds))
            {
                Console.WriteLine("CLIPPED: " + control.Name + " bounds " + bounds + " in " + current.Parent.GetType().Name + " " + current.Parent.Name + " " + current.Parent.ClientRectangle);
                return false;
            }
        }
        return true;
    }

    static void Draw(Form form, string path)
    {
        form.CreateControl();
        Materialize(form);
        form.PerformLayout();
        Application.DoEvents();
        using (var image = new Bitmap(form.Width, form.Height))
        {
            form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size));
            image.Save(path, ImageFormat.Png);
        }
    }

    static void Materialize(Control control)
    {
        var handle = control.Handle;
        control.PerformLayout();
        foreach (Control child in control.Controls) Materialize(child);
    }
}
