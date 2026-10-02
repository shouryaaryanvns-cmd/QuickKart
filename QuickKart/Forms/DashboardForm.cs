using System;
using System.Drawing;
using System.Windows.Forms;
using QuickKart.Models;

namespace QuickKart.Forms
{
    public sealed class DashboardForm : Form
    {
        private readonly AuthenticatedUser user;
        private Label welcomeLabel;
        private Label identityLabel;

        public DashboardForm(AuthenticatedUser user)
        {
            this.user = user ?? throw new ArgumentNullException("user");
            Text = "QuickKart - " + user.Role + " Dashboard";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1050, 680);
            MinimumSize = new Size(900, 600);
            BackColor = AppTheme.Background;
            Font = new Font("Segoe UI", 9f);
            BuildHeader();
            BuildDashboard();
        }

        private void BuildHeader()
        {
            var header = new Panel
            {
                Dock = DockStyle.Top, Height = 78, BackColor = Color.White,
                Padding = new Padding(28, 14, 28, 12)
            };
            Controls.Add(header);
            header.Controls.Add(new Label
            {
                AutoSize = true, Text = "QuickKart", ForeColor = AppTheme.Primary,
                Font = new Font("Segoe UI Semibold", 22f, FontStyle.Bold),
                Location = new Point(28, 16)
            });
            identityLabel = new Label
            {
                AutoSize = true, Text = user.FullName + "  |  " + user.Role,
                ForeColor = AppTheme.Muted, Font = new Font("Segoe UI", 9.5f),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(header.Width - 380, 29)
            };
            header.Controls.Add(identityLabel);
            var logoutButton = AppTheme.LinkButton("Log out");
            logoutButton.Width = 100; logoutButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            logoutButton.Location = new Point(header.Width - 128, 21);
            header.Controls.Add(logoutButton);
            header.Resize += delegate
            {
                logoutButton.Left = header.ClientSize.Width - logoutButton.Width - 28;
                identityLabel.Left = logoutButton.Left - identityLabel.Width - 24;
            };
            logoutButton.Click += delegate { DialogResult = DialogResult.OK; Close(); };
        }

        private void BuildDashboard()
        {
            var body = new Panel
            {
                Dock = DockStyle.Fill, BackColor = AppTheme.Background,
                Padding = new Padding(38, 32, 38, 32)
            };
            Controls.Add(body);
            Controls[1].BringToFront();
            welcomeLabel = new Label
            {
                AutoSize = true, Text = "Welcome, " + user.FullName,
                ForeColor = AppTheme.Text,
                Font = new Font("Segoe UI Semibold", 24f, FontStyle.Bold),
                Location = new Point(40, 28)
            };
            body.Controls.Add(welcomeLabel);
            body.Controls.Add(new Label
            {
                AutoSize = true,
                Text = user.Role == "Admin"
                    ? "Manage the complete QuickKart store from this dashboard."
                    : "Shop groceries and manage your QuickKart account.",
                ForeColor = AppTheme.Muted, Font = new Font("Segoe UI", 10.5f),
                Location = new Point(43, 75)
            });
            var modules = new FlowLayoutPanel
            {
                Location = new Point(38, 120), Size = new Size(940, 450),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                AutoScroll = true, WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = AppTheme.Background, Padding = new Padding(2)
            };
            body.Controls.Add(modules);

            if (user.Role == "Admin")
            {
                modules.Controls.Add(CreateModuleCard("Catalog",
                    "Categories, products, pricing, discounts and availability.",
                    "Manage catalog", delegate { Open(new CatalogManagementForm(user.Id)); }));
                modules.Controls.Add(CreateModuleCard("Inventory",
                    "Current stock, reorder levels and audited adjustments.",
                    "Manage inventory", delegate { Open(new InventoryManagementForm(user.Id)); }));
                modules.Controls.Add(CreateModuleCard("Customers",
                    "Registered customers, activity and account access.",
                    "Manage customers", delegate { Open(new CustomerManagementForm()); }));
                modules.Controls.Add(CreateModuleCard("Orders & Billing",
                    "Process orders, cancel safely, and print purchase bills.",
                    "Manage orders", delegate { Open(new OrderForm(user)); }));
                modules.Controls.Add(CreateModuleCard("Reports",
                    "Sales, revenue, best-selling products and low stock.",
                    "View reports", delegate { Open(new ReportsForm()); }));
            }
            else
            {
                modules.Controls.Add(CreateModuleCard("Shop groceries",
                    "Browse products, build your cart and place an order.",
                    "Create an order", delegate { Open(new OrderForm(user)); }));
                modules.Controls.Add(CreateModuleCard("My orders",
                    "Track order and payment status, details, and purchase bills.",
                    "Order history", delegate { Open(new OrderHistoryForm(user)); }));
                modules.Controls.Add(CreateModuleCard("My profile",
                    "Update personal details and change your password securely.",
                    "Manage profile", delegate
                    {
                        Open(new ProfileForm(user));
                        RefreshIdentity();
                    }));
            }
        }

        private static Control CreateModuleCard(
            string title, string description, string buttonText, EventHandler click)
        {
            var card = new Panel
            {
                Width = 285, Height = 190, BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(8)
            };
            card.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 6, BackColor = AppTheme.Primary });
            card.Controls.Add(new Label
            {
                AutoSize = true, Text = title, Location = new Point(22, 28),
                ForeColor = AppTheme.Text,
                Font = new Font("Segoe UI Semibold", 16f, FontStyle.Bold)
            });
            card.Controls.Add(new Label
            {
                AutoSize = false, Text = description, Location = new Point(24, 70),
                Size = new Size(235, 55), ForeColor = AppTheme.Muted,
                Font = new Font("Segoe UI", 9.5f)
            });
            Button button = AppTheme.PrimaryButton(buttonText);
            button.Width = 165; button.Height = 37; button.Location = new Point(23, 136);
            button.Click += click; card.Controls.Add(button);
            return card;
        }

        private void Open(Form form)
        {
            using (form) form.ShowDialog(this);
        }

        private void RefreshIdentity()
        {
            welcomeLabel.Text = "Welcome, " + user.FullName;
            identityLabel.Text = user.FullName + "  |  " + user.Role;
        }
    }
}
