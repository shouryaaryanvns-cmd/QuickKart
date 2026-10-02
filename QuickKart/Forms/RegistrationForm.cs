using System;
using System.Data.SqlClient;
using System.Drawing;
using System.Windows.Forms;
using QuickKart.Models;
using QuickKart.Services;

namespace QuickKart.Forms
{
    public sealed class RegistrationForm : Form
    {
        private readonly AuthenticationService authenticationService;
        private readonly TextBox fullNameTextBox;
        private readonly TextBox emailTextBox;
        private readonly TextBox phoneTextBox;
        private readonly ComboBox genderComboBox;
        private readonly DateTimePicker dateOfBirthPicker;
        private readonly TextBox passwordTextBox;
        private readonly TextBox confirmPasswordTextBox;
        private readonly CheckBox showPasswordCheckBox;
        private readonly Label statusLabel;
        private readonly Button registerButton;

        public string RegisteredEmail { get; private set; }

        public RegistrationForm(AuthenticationService authenticationService)
        {
            this.authenticationService = authenticationService;

            Text = "QuickKart - Customer Registration";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(620, 740);
            MinimumSize = new Size(600, 700);
            BackColor = AppTheme.Background;
            Font = new Font("Segoe UI", 9f);

            var card = new Panel
            {
                Width = 450,
                Height = 650,
                BackColor = Color.White,
                Anchor = AnchorStyles.None,
                Padding = new Padding(50, 35, 50, 30)
            };
            Controls.Add(card);
            Resize += delegate
            {
                card.Left = Math.Max(20, (ClientSize.Width - card.Width) / 2);
                card.Top = Math.Max(20, (ClientSize.Height - card.Height) / 2);
            };

            var fields = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = Color.White
            };
            card.Controls.Add(fields);

            fields.Controls.Add(AppTheme.Heading("Create your account", 20f));
            fields.Controls.Add(new Label
            {
                AutoSize = true,
                Text = "Register as a QuickKart customer.",
                ForeColor = AppTheme.Muted,
                Font = new Font("Segoe UI", 10f),
                Margin = new Padding(0, 0, 0, 18)
            });

            fields.Controls.Add(AppTheme.Caption("Full name"));
            fullNameTextBox = AppTheme.Input();
            fields.Controls.Add(fullNameTextBox);

            fields.Controls.Add(AppTheme.Caption("Email address"));
            emailTextBox = AppTheme.Input();
            fields.Controls.Add(emailTextBox);

            fields.Controls.Add(AppTheme.Caption("Phone number"));
            phoneTextBox = AppTheme.Input();
            phoneTextBox.MaxLength = 15;
            fields.Controls.Add(phoneTextBox);

            fields.Controls.Add(AppTheme.Caption("Gender (optional)"));
            genderComboBox = new ComboBox
            {
                Width = 330,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10.5f),
                Margin = new Padding(0, 0, 0, 12)
            };
            genderComboBox.Items.AddRange(new object[] { "Prefer not to say", "Female", "Male", "Other" });
            genderComboBox.SelectedIndex = 0;
            fields.Controls.Add(genderComboBox);

            fields.Controls.Add(AppTheme.Caption("Date of birth (optional)"));
            dateOfBirthPicker = new DateTimePicker
            {
                Width = 330,
                Font = new Font("Segoe UI", 10.5f),
                Format = DateTimePickerFormat.Long,
                ShowCheckBox = true,
                Checked = false,
                MaxDate = DateTime.Today,
                Margin = new Padding(0, 0, 0, 12)
            };
            fields.Controls.Add(dateOfBirthPicker);

            fields.Controls.Add(AppTheme.Caption("Password"));
            passwordTextBox = AppTheme.Input(true);
            fields.Controls.Add(passwordTextBox);

            fields.Controls.Add(AppTheme.Caption("Confirm password"));
            confirmPasswordTextBox = AppTheme.Input(true);
            fields.Controls.Add(confirmPasswordTextBox);

            showPasswordCheckBox = new CheckBox
            {
                Text = "Show passwords",
                AutoSize = true,
                ForeColor = AppTheme.Muted,
                Margin = new Padding(0, -4, 0, 8)
            };
            showPasswordCheckBox.CheckedChanged += delegate
            {
                bool hide = !showPasswordCheckBox.Checked;
                passwordTextBox.UseSystemPasswordChar = hide;
                confirmPasswordTextBox.UseSystemPasswordChar = hide;
            };
            fields.Controls.Add(showPasswordCheckBox);

            statusLabel = new Label
            {
                AutoSize = false,
                Width = 330,
                Height = 42,
                ForeColor = AppTheme.Danger,
                Font = new Font("Segoe UI", 9f),
                Margin = new Padding(0, 0, 0, 2)
            };
            fields.Controls.Add(statusLabel);

            registerButton = AppTheme.PrimaryButton("Create account");
            registerButton.Click += RegisterButton_Click;
            fields.Controls.Add(registerButton);

            Button cancelButton = AppTheme.LinkButton("Back to login");
            cancelButton.Click += delegate
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };
            fields.Controls.Add(cancelButton);

            AcceptButton = registerButton;
            CancelButton = cancelButton;
        }

        private void RegisterButton_Click(object sender, EventArgs e)
        {
            statusLabel.Text = string.Empty;

            if (!string.Equals(
                passwordTextBox.Text,
                confirmPasswordTextBox.Text,
                StringComparison.Ordinal))
            {
                ShowError("Passwords do not match.");
                return;
            }

            string gender = genderComboBox.SelectedItem == null
                ? null
                : genderComboBox.SelectedItem.ToString();
            if (gender == "Prefer not to say")
                gender = null;

            var request = new RegistrationRequest
            {
                FullName = fullNameTextBox.Text,
                Email = emailTextBox.Text,
                PhoneNumber = phoneTextBox.Text,
                Gender = gender,
                DateOfBirth = dateOfBirthPicker.Checked
                    ? (DateTime?)dateOfBirthPicker.Value.Date
                    : null,
                Password = passwordTextBox.Text
            };

            SetBusy(true);
            try
            {
                authenticationService.RegisterCustomer(request);
                RegisteredEmail = request.Email.Trim().ToLowerInvariant();

                MessageBox.Show(
                    this,
                    "Your QuickKart account was created successfully.",
                    "Registration complete",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (InvalidOperationException exception)
            {
                ShowError(exception.Message);
            }
            catch (SqlException exception)
            {
                if (exception.Number == 2601 || exception.Number == 2627)
                    ShowError("An account with this email already exists.");
                else
                    ShowError("Registration could not reach the database.");
            }
            catch (Exception)
            {
                ShowError("Registration could not be completed.");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void SetBusy(bool busy)
        {
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
