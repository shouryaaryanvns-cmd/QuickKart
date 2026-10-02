using System;
using System.Drawing;
using System.Windows.Forms;
using QuickKart.Models;
using QuickKart.Services;

namespace QuickKart.Forms
{
    public sealed class OrderHistoryForm : Form
    {
        private readonly AuthenticatedUser user;
        private readonly CustomerService customerService = new CustomerService();
        private readonly OrderService orderService = new OrderService();
        private readonly DataGridView ordersGrid;
        private readonly DataGridView itemsGrid;
        private readonly Label addressLabel;

        public OrderHistoryForm(AuthenticatedUser user)
        {
            this.user = user ?? throw new ArgumentNullException("user");
            Text = "QuickKart - My Orders";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1050, 680);
            MinimumSize = new Size(900, 600);
            BackColor = AppTheme.Background;
            Font = new Font("Segoe UI", 9f);

            var header = new Panel
            {
                Dock = DockStyle.Top, Height = 72,
                BackColor = Color.White, Padding = new Padding(24, 14, 24, 10)
            };
            Controls.Add(header);
            header.Controls.Add(new Label
            {
                AutoSize = true, Text = "My Orders",
                ForeColor = AppTheme.Primary,
                Font = new Font("Segoe UI Semibold", 20f, FontStyle.Bold),
                Location = new Point(24, 16)
            });
            var invoiceButton = AppTheme.PrimaryButton("View bill");
            invoiceButton.Width = 115;
            invoiceButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            invoiceButton.Location = new Point(header.Width - 270, 18);
            invoiceButton.Click += InvoiceButton_Click;
            header.Controls.Add(invoiceButton);
            var closeButton = AppTheme.LinkButton("Close");
            closeButton.Width = 100;
            closeButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            closeButton.Location = new Point(header.Width - 125, 18);
            closeButton.Click += delegate { Close(); };
            header.Controls.Add(closeButton);
            header.Resize += delegate
            {
                invoiceButton.Left = header.ClientSize.Width - 270;
                closeButton.Left = header.ClientSize.Width - 125;
            };

            var body = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = AppTheme.Background,
                Padding = new Padding(22)
            };
            Controls.Add(body);
            header.BringToFront();

            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 320,
                BackColor = AppTheme.Background
            };
            body.Controls.Add(split);

            ordersGrid = CreateGrid();
            ordersGrid.Dock = DockStyle.Fill;
            AddColumn(ordersGrid, "OrderId", "Order #", 70);
            AddColumn(ordersGrid, "OrderDate", "Date", 130, "dd-MMM-yyyy HH:mm");
            AddColumn(ordersGrid, "TotalAmount", "Total", 80, "C2");
            AddColumn(ordersGrid, "OrderStatus", "Order status", 95);
            AddColumn(ordersGrid, "PaymentStatus", "Payment", 85);
            AddColumn(ordersGrid, "PaymentMethod", "Method", 120);
            ordersGrid.SelectionChanged += delegate { LoadItems(); };
            split.Panel1.Controls.Add(ordersGrid);

            addressLabel = new Label
            {
                Dock = DockStyle.Top, Height = 54,
                ForeColor = AppTheme.Muted,
                Font = new Font("Segoe UI", 9.5f),
                Padding = new Padding(8, 10, 8, 4),
                AutoEllipsis = true
            };
            split.Panel2.Controls.Add(addressLabel);

            itemsGrid = CreateGrid();
            itemsGrid.Dock = DockStyle.Fill;
            AddColumn(itemsGrid, "ProductName", "Product", 220);
            AddColumn(itemsGrid, "Price", "Price", 75, "C2");
            AddColumn(itemsGrid, "Quantity", "Quantity", 65);
            AddColumn(itemsGrid, "LineTotal", "Line total", 80, "C2");
            split.Panel2.Controls.Add(itemsGrid);
            itemsGrid.BringToFront();

            Shown += delegate { LoadOrders(); };
        }

        private void LoadOrders()
        {
            ordersGrid.DataSource = customerService.GetOrderHistory(user.Id);
            LoadItems();
        }

        private void LoadItems()
        {
            OrderRecord order = SelectedOrder();
            if (order == null)
            {
                itemsGrid.DataSource = null;
                addressLabel.Text = "Select an order to see details.";
                return;
            }
            itemsGrid.DataSource = orderService.GetOrderItems(order.OrderId);
            addressLabel.Text = "Delivery: " + order.DeliveryAddress;
        }

        private void InvoiceButton_Click(object sender, EventArgs e)
        {
            OrderRecord order = SelectedOrder();
            if (order == null)
            {
                MessageBox.Show(this, "Select an order first.", "QuickKart",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (var invoice = new InvoiceForm(
                order, orderService.GetOrderItems(order.OrderId)))
            {
                invoice.ShowDialog(this);
            }
        }

        private OrderRecord SelectedOrder()
        {
            if (ordersGrid.SelectedRows.Count == 0)
                return null;
            return ordersGrid.SelectedRows[0].DataBoundItem as OrderRecord;
        }

        private static DataGridView CreateGrid()
        {
            return new DataGridView
            {
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                ReadOnly = true,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ColumnHeadersHeight = 34,
                RowTemplate = { Height = 30 }
            };
        }

        private static void AddColumn(
            DataGridView grid, string property, string title, int weight, string format = null)
        {
            var column = new DataGridViewTextBoxColumn
            {
                DataPropertyName = property,
                HeaderText = title,
                FillWeight = weight
            };
            if (!string.IsNullOrEmpty(format))
                column.DefaultCellStyle.Format = format;
            grid.Columns.Add(column);
        }
    }
}
