using System;
using System.Drawing;
using System.Windows.Forms;
using QuickKart.Models;

namespace QuickKart.Forms
{
    public sealed class DashboardForm : Form
    {
        public DashboardForm(AuthenticatedUser user)
        {
            Text = "QuickKart - " + user.Role + " Dashboard";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(900, 580);
            MinimumSize = new Size(760, 500);
            BackColor = AppTheme.Background;
            Font = new Font("Segoe UI", 9f);

            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 76,
                BackColor = Color.White,
                Padding = new Padding(28, 16, 28, 12)
            };
            Controls.Add(header);

            header.Controls.Add(new Label
            {
                AutoSize = true,
                Text = "QuickKart",
                ForeColor = AppTheme.Primary,
                Font = new Font("Segoe UI Semibold", 21f, FontStyle.Bold),
                Location = new Point(28, 17)
            });

            var logoutButton = new Button
            {
                Text = "Log out",
                Width = 105,
                Height = 36,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = AppTheme.Primary,
                Font = new Font("Segoe UI Semibold", 9.5f),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            logoutButton.FlatAppearance.BorderColor = AppTheme.Primary;
            logoutButton.Location = new Point(header.Width - 135, 20);
            header.Controls.Add(logoutButton);
            header.Resize += delegate
            {
                logoutButton.Left = header.ClientSize.Width - logoutButton.Width - 28;
            };
            logoutButton.Click += delegate
            {
                DialogResult = DialogResult.OK;
                Close();
            };

            var welcomeCard = new Panel
            {
                Width = 650,
                Height = 265,
                BackColor = Color.White,
                Anchor = AnchorStyles.None
            };
            Controls.Add(welcomeCard);
            Resize += delegate
            {
                welcomeCard.Left = (ClientSize.Width - welcomeCard.Width) / 2;
                welcomeCard.Top = header.Bottom + Math.Max(30, (ClientSize.Height - header.Height - welcomeCard.Height) / 2);
            };

            welcomeCard.Controls.Add(new Label
            {
                AutoSize = true,
                Text = "Login successful",
                ForeColor = AppTheme.Primary,
                Font = new Font("Segoe UI Semibold", 14f),
                Location = new Point(38, 35)
            });
            welcomeCard.Controls.Add(new Label
            {
                AutoSize = true,
                Text = "Welcome, " + user.FullName,
                ForeColor = AppTheme.Text,
                Font = new Font("Segoe UI Semibold", 24f, FontStyle.Bold),
                Location = new Point(35, 76)
            });
            welcomeCard.Controls.Add(new Label
            {
                AutoSize = true,
                Text = "Signed in as " + user.Role + "  •  " + user.LoginName,
                ForeColor = AppTheme.Muted,
                Font = new Font("Segoe UI", 10.5f),
                Location = new Point(39, 127)
            });
            welcomeCard.Controls.Add(new Label
            {
                AutoSize = false,
                Width = 565,
                Height = 55,
                Text = user.Role == "Admin"
                    ? "Authentication is working. The administrator dashboard modules can be added next."
                    : "Your customer account is ready. Product browsing and cart modules can be added next.",
                ForeColor = AppTheme.Muted,
                Font = new Font("Segoe UI", 10.5f),
                Location = new Point(39, 177)
            });
        }
    }
}
