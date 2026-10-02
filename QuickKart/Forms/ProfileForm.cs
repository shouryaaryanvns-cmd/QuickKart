using System;
using System.Drawing;
using System.Windows.Forms;
using QuickKart.Models;
using QuickKart.Services;

namespace QuickKart.Forms
{
    public sealed class ProfileForm : Form
    {
        private readonly AuthenticatedUser user;
        private readonly CustomerService customerService = new CustomerService();
        private readonly TextBox nameTextBox;
        private readonly TextBox emailTextBox;
        private readonly TextBox phoneTextBox;
        private readonly ComboBox genderComboBox;
        private readonly DateTimePicker birthDatePicker;
        private readonly Label memberSinceLabel;
        private readonly TextBox currentPasswordTextBox;
        private readonly TextBox newPasswordTextBox;
        private readonly TextBox confirmPasswordTextBox;

        public ProfileForm(AuthenticatedUser user)
        {
            this.user = user ?? throw new ArgumentNullException("user");
            Text = "QuickKart - My Profile";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(830, 580);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            BackColor = AppTheme.Background;
            Font = new Font("Segoe UI", 9f);

            var profileCard = CreateCard(new Rectangle(25, 25, 380, 525));
            var passwordCard = CreateCard(new Rectangle(425, 25, 380, 525));

            AddTitle(profileCard, "Profile details", "Update your customer information.");
            nameTextBox = AddField(profileCard, "Full name", 105, false);
            emailTextBox = AddField(profileCard, "Email address", 170, false);
            phoneTextBox = AddField(profileCard, "Phone number", 235, false);

            AddCaption(profileCard, "Gender", 300);
            genderComboBox = new ComboBox
            {
                Location = new Point(28, 323), Width = 324,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10f)
            };
            genderComboBox.Items.AddRange(new object[]
            {
                "Prefer not to say", "Female", "Male", "Other"
            });
            profileCard.Controls.Add(genderComboBox);

            AddCaption(profileCard, "Date of birth", 365);
            birthDatePicker = new DateTimePicker
            {
                Location = new Point(28, 388), Width = 324,
                Format = DateTimePickerFormat.Long,
                ShowCheckBox = true,
                MaxDate = DateTime.Today,
                Font = new Font("Segoe UI", 10f)
            };
            profileCard.Controls.Add(birthDatePicker);

            memberSinceLabel = new Label
            {
                Location = new Point(28, 425), Width = 324, Height = 24,
                ForeColor = AppTheme.Muted,
                Font = new Font("Segoe UI", 9f)
            };
            profileCard.Controls.Add(memberSinceLabel);

            var saveProfileButton = AppTheme.PrimaryButton("Save profile");
            saveProfileButton.Width = 150;
            saveProfileButton.Location = new Point(202, 458);
            saveProfileButton.Click += SaveProfile_Click;
            profileCard.Controls.Add(saveProfileButton);

            AddTitle(passwordCard, "Change password", "Use a strong password for your account.");
            currentPasswordTextBox = AddField(passwordCard, "Current password", 120, true);
            newPasswordTextBox = AddField(passwordCard, "New password", 200, true);
            confirmPasswordTextBox = AddField(passwordCard, "Confirm new password", 280, true);

            var hint = new Label
            {
                Location = new Point(28, 345), Width = 324, Height = 58,
                Text = "Minimum 8 characters with uppercase, lowercase, number, and special character.",
                ForeColor = AppTheme.Muted,
                Font = new Font("Segoe UI", 9f)
            };
            passwordCard.Controls.Add(hint);

            var changePasswordButton = AppTheme.PrimaryButton("Change password");
            changePasswordButton.Width = 165;
            changePasswordButton.Location = new Point(187, 413);
            changePasswordButton.Click += ChangePassword_Click;
            passwordCard.Controls.Add(changePasswordButton);

            var closeButton = AppTheme.LinkButton("Close");
            closeButton.Width = 110;
            closeButton.Location = new Point(242, 463);
            closeButton.Click += delegate { Close(); };
            passwordCard.Controls.Add(closeButton);

            Load += delegate { LoadProfile(); };
        }

        private Panel CreateCard(Rectangle bounds)
        {
            var panel = new Panel
            {
                Bounds = bounds,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(panel);
            return panel;
        }

        private static void AddTitle(Control parent, string title, string subtitle)
        {
            parent.Controls.Add(new Label
            {
                AutoSize = true, Text = title,
                Location = new Point(25, 24),
                ForeColor = AppTheme.Text,
                Font = new Font("Segoe UI Semibold", 18f, FontStyle.Bold)
            });
            parent.Controls.Add(new Label
            {
                AutoSize = true, Text = subtitle,
                Location = new Point(28, 65),
                ForeColor = AppTheme.Muted,
                Font = new Font("Segoe UI", 9.5f)
            });
        }

        private static void AddCaption(Control parent, string text, int top)
        {
            parent.Controls.Add(new Label
            {
                AutoSize = true, Text = text,
                Location = new Point(28, top),
                ForeColor = AppTheme.Muted,
                Font = new Font("Segoe UI", 9f)
            });
        }

        private static TextBox AddField(
            Control parent, string caption, int top, bool password)
        {
            AddCaption(parent, caption, top);
            var input = new TextBox
            {
                Location = new Point(28, top + 23), Width = 324,
                Font = new Font("Segoe UI", 10.5f),
                UseSystemPasswordChar = password,
                BorderStyle = BorderStyle.FixedSingle
            };
            parent.Controls.Add(input);
            return input;
        }

        private void LoadProfile()
        {
            try
            {
                CustomerProfileRecord profile = customerService.GetProfile(user.Id);
                nameTextBox.Text = profile.FullName;
                emailTextBox.Text = profile.Email;
                phoneTextBox.Text = profile.PhoneNumber;
                genderComboBox.SelectedItem = string.IsNullOrWhiteSpace(profile.Gender)
                    ? "Prefer not to say" : profile.Gender;
                if (genderComboBox.SelectedIndex < 0)
                    genderComboBox.SelectedIndex = 0;
                birthDatePicker.Checked = profile.DateOfBirth.HasValue;
                if (profile.DateOfBirth.HasValue)
                    birthDatePicker.Value = profile.DateOfBirth.Value;
                memberSinceLabel.Text = "Member since " + profile.CreatedDate.ToString("dd MMMM yyyy");
            }
            catch (Exception exception)
            {
                ShowError(exception.Message);
            }
        }

        private void SaveProfile_Click(object sender, EventArgs e)
        {
            try
            {
                string gender = Convert.ToString(genderComboBox.SelectedItem);
                customerService.UpdateProfile(new CustomerProfileRecord
                {
                    UserId = user.Id,
                    FullName = nameTextBox.Text,
                    Email = emailTextBox.Text,
                    PhoneNumber = phoneTextBox.Text,
                    Gender = gender == "Prefer not to say" ? string.Empty : gender,
                    DateOfBirth = birthDatePicker.Checked
                        ? (DateTime?)birthDatePicker.Value.Date : null
                });
                user.FullName = nameTextBox.Text.Trim();
                user.LoginName = emailTextBox.Text.Trim().ToLowerInvariant();
                MessageBox.Show(this, "Profile updated successfully.", "QuickKart",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception exception)
            {
                ShowError(exception.Message);
            }
        }

        private void ChangePassword_Click(object sender, EventArgs e)
        {
            if (!string.Equals(newPasswordTextBox.Text,
                confirmPasswordTextBox.Text, StringComparison.Ordinal))
            {
                ShowError("New passwords do not match.");
                return;
            }
            try
            {
                customerService.ChangePassword(
                    user.Id, currentPasswordTextBox.Text, newPasswordTextBox.Text);
                currentPasswordTextBox.Clear();
                newPasswordTextBox.Clear();
                confirmPasswordTextBox.Clear();
                MessageBox.Show(this, "Password changed successfully.", "QuickKart",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception exception)
            {
                ShowError(exception.Message);
            }
        }

        private void ShowError(string message)
        {
            MessageBox.Show(this, message, "QuickKart Profile",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
