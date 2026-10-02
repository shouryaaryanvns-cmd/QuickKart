using System;
using System.Data.SqlClient;
using System.Drawing;
using System.Windows.Forms;
using QuickKart.Models;
using QuickKart.Services;

namespace QuickKart.Forms
{
    public sealed class LoginForm : Form
    {
        private readonly AuthenticationService authenticationService =
            new AuthenticationService();

        private readonly TextBox loginTextBox;
        private readonly TextBox passwordTextBox;
        private readonly Button loginButton;
        private readonly Button registerButton;
        private readonly Label statusLabel;
        private readonly CheckBox showPasswordCheckBox;

        public LoginForm()
        {
            Text = "QuickKart - Login";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(860, 560);
            Size = new Size(960, 620);
            BackColor = AppTheme.Background;
            Font = new Font("Segoe UI", 9f);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(0)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
            Controls.Add(root);

            root.Controls.Add(CreateBrandPanel(), 0, 0);

            var formHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };
            root.Controls.Add(formHost, 1, 0);

            var fields = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                Width = 350,
                BackColor = Color.White,
                Anchor = AnchorStyles.None
            };
            formHost.Controls.Add(fields);
            formHost.Resize += delegate
            {
                fields.Left = Math.Max(30, (formHost.ClientSize.Width - fields.Width) / 2);
                fields.Top = Math.Max(35, (formHost.ClientSize.Height - fields.Height) / 2);
            };

            fields.Controls.Add(AppTheme.Heading("Welcome back", 22f));
            fields.Controls.Add(new Label
            {
                Text = "Sign in as a customer or administrator.",
                AutoSize = true,
                ForeColor = AppTheme.Muted,
                Font = new Font("Segoe UI", 10f),
                Margin = new Padding(0, 0, 0, 24)
            });

            fields.Controls.Add(AppTheme.Caption("Email or admin username"));
            loginTextBox = AppTheme.Input();
            fields.Controls.Add(loginTextBox);

            fields.Controls.Add(AppTheme.Caption("Password"));
            passwordTextBox = AppTheme.Input(true);
            fields.Controls.Add(passwordTextBox);

            showPasswordCheckBox = new CheckBox
            {
                Text = "Show password",
                AutoSize = true,
                ForeColor = AppTheme.Muted,
                Font = new Font("Segoe UI", 9f),
                Margin = new Padding(0, -4, 0, 8)
            };
            showPasswordCheckBox.CheckedChanged += delegate
            {
                passwordTextBox.UseSystemPasswordChar = !showPasswordCheckBox.Checked;
            };
            fields.Controls.Add(showPasswordCheckBox);

            statusLabel = new Label
            {
                AutoSize = false,
                Width = 330,
                Height = 38,
                ForeColor = AppTheme.Danger,
                Font = new Font("Segoe UI", 9f),
                Margin = new Padding(0, 0, 0, 2)
            };
            fields.Controls.Add(statusLabel);

            loginButton = AppTheme.PrimaryButton("Sign in");
            loginButton.Click += LoginButton_Click;
            fields.Controls.Add(loginButton);

            registerButton = AppTheme.LinkButton("Create customer account");
            registerButton.Click += RegisterButton_Click;
            fields.Controls.Add(registerButton);

            AcceptButton = loginButton;
            Shown += LoginForm_Shown;
        }

        private static Control CreateBrandPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = AppTheme.Primary
            };

            var content = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                Width = 310,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.None
            };
            panel.Controls.Add(content);

            content.Controls.Add(new Label
            {
                AutoSize = true,
                Text = "QuickKart",
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 30f, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 12)
            });
            content.Controls.Add(new Label
            {
                AutoSize = false,
                Width = 300,
                Height = 75,
                Text = "Fresh groceries, organized shopping, and faster billing—all in one place.",
                ForeColor = Color.FromArgb(224, 243, 233),
                Font = new Font("Segoe UI", 12f),
                Margin = new Padding(0)
            });

            panel.Resize += delegate
            {
                content.Left = Math.Max(35, (panel.ClientSize.Width - content.Width) / 2);
                content.Top = Math.Max(35, (panel.ClientSize.Height - content.Height) / 2);
            };

            return panel;
        }

        private void LoginForm_Shown(object sender, EventArgs e)
        {
            try
            {
                authenticationService.TestConnection();
                statusLabel.ForeColor = AppTheme.Primary;
                statusLabel.Text = "Connected to QuickKartDB.";
                loginTextBox.Focus();
            }
            catch
            {
                statusLabel.ForeColor = AppTheme.Danger;
                statusLabel.Text = "Cannot connect to QuickKartDB. Check SQL Server.";
                loginButton.Enabled = false;
                registerButton.Enabled = false;
            }
        }

        private void LoginButton_Click(object sender, EventArgs e)
        {
            statusLabel.Text = string.Empty;

            if (string.IsNullOrWhiteSpace(loginTextBox.Text) ||
                string.IsNullOrEmpty(passwordTextBox.Text))
            {
                ShowError("Enter your email/username and password.");
                return;
            }

            SetBusy(true);
            try
            {
                AuthenticatedUser user =
                    authenticationService.Login(loginTextBox.Text, passwordTextBox.Text);

                if (user == null)
                {
                    ShowError("Invalid login details or inactive account.");
                    return;
                }

                Hide();
                using (var dashboard = new DashboardForm(user))
                {
                    dashboard.ShowDialog(this);
                }
                Show();

                passwordTextBox.Clear();
                showPasswordCheckBox.Checked = false;
                statusLabel.ForeColor = AppTheme.Primary;
                statusLabel.Text = "You have been logged out safely.";
                loginTextBox.Focus();
            }
            catch (SqlException)
            {
                ShowError("The database is unavailable. Please try again.");
            }
            catch (Exception)
            {
                ShowError("Login could not be completed.");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void RegisterButton_Click(object sender, EventArgs e)
        {
            using (var registration = new RegistrationForm(authenticationService))
            {
                if (registration.ShowDialog(this) == DialogResult.OK)
                {
                    loginTextBox.Text = registration.RegisteredEmail;
                    passwordTextBox.Clear();
                    statusLabel.ForeColor = AppTheme.Primary;
                    statusLabel.Text = "Registration complete. Sign in with your new password.";
                    passwordTextBox.Focus();
                }
            }
        }

        private void SetBusy(bool busy)
        {
            loginButton.Enabled = !busy;
            registerButton.Enabled = !busy;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        }

        private void ShowError(string message)
        {
            statusLabel.ForeColor = AppTheme.Danger;
            statusLabel.Text = message;
        }
    }
}
