using CIEL.Reconciliation.Security;

namespace CIEL.Reconciliation;

public sealed class FirstRunSetupForm : Form
{
    private readonly TextBox _password = new() { UseSystemPasswordChar = true, Width = 280 };
    private readonly TextBox _confirm = new() { UseSystemPasswordChar = true, Width = 280 };

    public FirstRunSetupForm()
    {
        Text = "CIEL Reconciliation - First-Time Setup";
        Width = 520;
        Height = 410;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(245, 247, 250);
        Font = new Font("Segoe UI", 10);

        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(55, 35, 55, 35), RowCount = 8, ColumnCount = 1 };
        Controls.Add(panel);
        panel.Controls.Add(new Label { Text = "FIRST-TIME SETUP", Dock = DockStyle.Fill, Height = 55, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI Semibold", 22, FontStyle.Bold), ForeColor = Color.FromArgb(0, 76, 108) });
        panel.Controls.Add(new Label { Text = "Create the password for the protected administrator account.", Dock = DockStyle.Fill, Height = 48, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.FromArgb(100, 116, 139) });
        panel.Controls.Add(new Label { Text = "Username: walid", Dock = DockStyle.Fill, Height = 35, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI Semibold", 10, FontStyle.Bold) });
        panel.Controls.Add(new Label { Text = "Password", Dock = DockStyle.Fill, Height = 28 });
        panel.Controls.Add(_password);
        panel.Controls.Add(new Label { Text = "Confirm password", Dock = DockStyle.Fill, Height = 28 });
        panel.Controls.Add(_confirm);
        var save = new Button { Text = "CREATE ADMIN ACCOUNT", Dock = DockStyle.Fill, Height = 44, BackColor = Color.FromArgb(0, 119, 182), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        save.FlatAppearance.BorderSize = 0;
        save.Click += Save_Click;
        panel.Controls.Add(save);
        AcceptButton = save;
    }

    private void Save_Click(object? sender, EventArgs e)
    {
        var error = PasswordHasher.ValidatePassword(_password.Text);
        if (error is not null) { MessageBox.Show(error, "Password", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        if (_password.Text != _confirm.Text) { MessageBox.Show("The passwords do not match.", "Password", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        AppServices.Users.CreateInitialAdministrator(_password.Text);
        DialogResult = DialogResult.OK;
        Close();
    }
}
