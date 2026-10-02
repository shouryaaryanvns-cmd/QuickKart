using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using QuickKart.Models;
using QuickKart.Services;

namespace QuickKart.Forms
{
    public sealed class OrderForm : Form
    {
        private readonly AuthenticatedUser user;
        private readonly OrderService orderService = new OrderService();
        private Panel headerPanel;

        private DataGridView productsGrid;
        private DataGridView cartGrid;
        private TextBox searchTextBox;
        private ComboBox categoryComboBox;
        private NumericUpDown productQuantity;
        private NumericUpDown cartQuantity;
        private ComboBox addressComboBox;
        private ComboBox paymentComboBox;
        private Label totalLabel;
        private Label cartStatusLabel;

        private DataGridView ordersGrid;
        private DataGridView orderItemsGrid;
        private ComboBox orderStatusComboBox;
        private ComboBox processStatusComboBox;
        private Label orderDetailsLabel;

        public OrderForm(AuthenticatedUser user)
        {
            if (user == null)
                throw new ArgumentNullException("user");
            this.user = user;

            Text = "QuickKart - Order Form";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1240, 760);
            MinimumSize = new Size(1040, 680);
            BackColor = AppTheme.Background;
            Font = new Font("Segoe UI", 9f);

            BuildHeader();
            if (string.Equals(user.Role, "Customer", StringComparison.OrdinalIgnoreCase))
                BuildCustomerView();
            else
                BuildAdminView();
        }

        private void BuildHeader()
        {
            headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 72,
                BackColor = Color.White,
                Padding = new Padding(24, 12, 24, 10)
            };
            Controls.Add(headerPanel);

            headerPanel.Controls.Add(new Label
            {
                AutoSize = true,
                Text = "QuickKart Orders",
                ForeColor = AppTheme.Primary,
                Font = new Font("Segoe UI Semibold", 20f, FontStyle.Bold),
                Location = new Point(24, 15)
            });
            headerPanel.Controls.Add(new Label
            {
                AutoSize = true,
                Text = string.Equals(user.Role, "Customer", StringComparison.OrdinalIgnoreCase)
                    ? "Build your cart and place an order"
                    : "Review customer orders",
                ForeColor = AppTheme.Muted,
                Font = new Font("Segoe UI", 9.5f),
                Location = new Point(255, 27)
            });

            var closeButton = AppTheme.LinkButton("Back to dashboard");
            closeButton.Width = 155;
            closeButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            closeButton.Location = new Point(headerPanel.Width - 180, 18);
            headerPanel.Controls.Add(closeButton);
            headerPanel.Resize += delegate
            {
                closeButton.Left = headerPanel.ClientSize.Width - closeButton.Width - 24;
            };
            closeButton.Click += delegate { Close(); };
        }

        private void BuildCustomerView()
        {
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                SplitterDistance = 655,
                BackColor = AppTheme.Background,
                Padding = new Padding(18)
            };
            Controls.Add(split);
            headerPanel.BringToFront();

            BuildProductsPanel(split.Panel1);
            BuildCartPanel(split.Panel2);

            Shown += delegate
            {
                LoadCategories();
                LoadProducts();
                LoadCart();
                LoadAddresses(null);
            };
        }

        private void BuildProductsPanel(Control parent)
        {
            var card = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(18)
            };
            parent.Controls.Add(card);

            var title = AppTheme.Heading("Available products", 15f);
            title.Dock = DockStyle.Top;
            card.Controls.Add(title);

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 48,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 6, 0, 4)
            };
            card.Controls.Add(toolbar);
            toolbar.BringToFront();

            searchTextBox = new TextBox
            {
                Width = 215,
                Font = new Font("Segoe UI", 10f),
                Margin = new Padding(0, 4, 8, 0)
            };
            searchTextBox.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                    LoadProducts();
            };
            toolbar.Controls.Add(searchTextBox);

            categoryComboBox = new ComboBox
            {
                Width = 180,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9.5f),
                Margin = new Padding(0, 3, 8, 0)
            };
            categoryComboBox.SelectedIndexChanged += delegate { LoadProducts(); };
            toolbar.Controls.Add(categoryComboBox);

            var searchButton = AppTheme.PrimaryButton("Search");
            searchButton.Width = 85;
            searchButton.Height = 32;
            searchButton.Margin = new Padding(0, 1, 0, 0);
            searchButton.Click += delegate { LoadProducts(); };
            toolbar.Controls.Add(searchButton);

            var addPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 58,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0, 8, 0, 4)
            };
            card.Controls.Add(addPanel);

            var addButton = AppTheme.PrimaryButton("Add to cart");
            addButton.Width = 135;
            addButton.Height = 38;
            addButton.Click += AddToCart_Click;
            addPanel.Controls.Add(addButton);

            productQuantity = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 999,
                Value = 1,
                Width = 75,
                Font = new Font("Segoe UI", 10f),
                Margin = new Padding(8, 12, 8, 0)
            };
            addPanel.Controls.Add(productQuantity);
            addPanel.Controls.Add(new Label
            {
                Text = "Quantity",
                AutoSize = true,
                ForeColor = AppTheme.Muted,
                Margin = new Padding(0, 15, 0, 0)
            });

            productsGrid = CreateGrid();
            productsGrid.Dock = DockStyle.Fill;
            AddTextColumn(productsGrid, "ProductId", "ID", 45, false);
            AddTextColumn(productsGrid, "ProductName", "Product", 175);
            AddTextColumn(productsGrid, "CategoryName", "Category", 120);
            AddTextColumn(productsGrid, "Brand", "Brand", 90);
            AddTextColumn(productsGrid, "Unit", "Unit", 70);
            AddTextColumn(productsGrid, "SellingPrice", "Price", 70, true, "C2");
            AddTextColumn(productsGrid, "Stock", "Stock", 55);
            card.Controls.Add(productsGrid);
            productsGrid.BringToFront();
        }

        private void BuildCartPanel(Control parent)
        {
            var card = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(18)
            };
            parent.Controls.Add(card);

            var title = AppTheme.Heading("Your cart", 15f);
            title.Dock = DockStyle.Top;
            card.Controls.Add(title);

            var checkoutPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 230,
                BackColor = Color.FromArgb(248, 250, 249),
                Padding = new Padding(14)
            };
            card.Controls.Add(checkoutPanel);

            cartStatusLabel = new Label
            {
                AutoSize = false,
                Height = 26,
                Dock = DockStyle.Top,
                ForeColor = AppTheme.Muted,
                Font = new Font("Segoe UI", 9f)
            };
            checkoutPanel.Controls.Add(cartStatusLabel);

            var addressLabel = AppTheme.Caption("Delivery address");
            addressLabel.Location = new Point(14, 43);
            checkoutPanel.Controls.Add(addressLabel);
            addressComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9f),
                Location = new Point(14, 65),
                Width = 330
            };
            checkoutPanel.Controls.Add(addressComboBox);

            var addAddressButton = AppTheme.LinkButton("+ Address");
            addAddressButton.Width = 90;
            addAddressButton.Height = 31;
            addAddressButton.Location = new Point(350, 63);
            addAddressButton.Click += AddAddress_Click;
            checkoutPanel.Controls.Add(addAddressButton);

            var paymentLabel = AppTheme.Caption("Payment method");
            paymentLabel.Location = new Point(14, 105);
            checkoutPanel.Controls.Add(paymentLabel);
            paymentComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9.5f),
                Location = new Point(14, 127),
                Width = 210
            };
            paymentComboBox.Items.AddRange(new object[]
            {
                "Cash on Delivery", "UPI", "Card", "Cash"
            });
            paymentComboBox.SelectedIndex = 0;
            checkoutPanel.Controls.Add(paymentComboBox);

            totalLabel = new Label
            {
                AutoSize = true,
                Text = "Total: ₹0.00",
                ForeColor = AppTheme.Text,
                Font = new Font("Segoe UI Semibold", 15f, FontStyle.Bold),
                Location = new Point(14, 176)
            };
            checkoutPanel.Controls.Add(totalLabel);

            var placeOrderButton = AppTheme.PrimaryButton("Place order");
            placeOrderButton.Width = 145;
            placeOrderButton.Location = new Point(295, 167);
            placeOrderButton.Click += PlaceOrder_Click;
            checkoutPanel.Controls.Add(placeOrderButton);

            var cartActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 52,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0, 7, 0, 3)
            };
            card.Controls.Add(cartActions);
            cartActions.BringToFront();

            var removeButton = AppTheme.LinkButton("Remove");
            removeButton.Width = 85;
            removeButton.Height = 34;
            removeButton.Click += RemoveCartItem_Click;
            cartActions.Controls.Add(removeButton);

            var updateButton = AppTheme.PrimaryButton("Update");
            updateButton.Width = 85;
            updateButton.Height = 34;
            updateButton.Click += UpdateCart_Click;
            cartActions.Controls.Add(updateButton);

            cartQuantity = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 999,
                Value = 1,
                Width = 70,
                Font = new Font("Segoe UI", 10f),
                Margin = new Padding(8, 8, 8, 0)
            };
            cartActions.Controls.Add(cartQuantity);
            cartActions.Controls.Add(new Label
            {
                Text = "Quantity",
                AutoSize = true,
                ForeColor = AppTheme.Muted,
                Margin = new Padding(0, 12, 0, 0)
            });

            cartGrid = CreateGrid();
            cartGrid.Dock = DockStyle.Fill;
            AddTextColumn(cartGrid, "CartItemId", "ID", 40, false);
            AddTextColumn(cartGrid, "ProductName", "Product", 145);
            AddTextColumn(cartGrid, "Unit", "Unit", 58);
            AddTextColumn(cartGrid, "Price", "Price", 66, true, "C2");
            AddTextColumn(cartGrid, "Quantity", "Qty", 45);
            AddTextColumn(cartGrid, "LineTotal", "Total", 72, true, "C2");
            cartGrid.SelectionChanged += delegate
            {
                CartLine selected = SelectedData<CartLine>(cartGrid);
                if (selected != null)
                    cartQuantity.Value = Math.Max(cartQuantity.Minimum,
                        Math.Min(cartQuantity.Maximum, selected.Quantity));
            };
            card.Controls.Add(cartGrid);
            cartGrid.BringToFront();
        }

        private void BuildAdminView()
        {
            var body = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = AppTheme.Background,
                Padding = new Padding(22)
            };
            Controls.Add(body);
            headerPanel.BringToFront();

            var card = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(18)
            };
            body.Controls.Add(card);

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 48,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };
            card.Controls.Add(toolbar);
            toolbar.Controls.Add(new Label
            {
                Text = "Order status",
                AutoSize = true,
                ForeColor = AppTheme.Muted,
                Margin = new Padding(0, 12, 8, 0)
            });
            orderStatusComboBox = new ComboBox
            {
                Width = 145,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9.5f),
                Margin = new Padding(0, 7, 8, 0)
            };
            orderStatusComboBox.Items.AddRange(new object[]
            {
                "All", "Pending", "Confirmed", "Packed", "Delivered", "Cancelled"
            });
            orderStatusComboBox.SelectedIndex = 0;
            toolbar.Controls.Add(orderStatusComboBox);
            var refreshButton = AppTheme.PrimaryButton("Refresh");
            refreshButton.Width = 95;
            refreshButton.Height = 33;
            refreshButton.Margin = new Padding(0, 3, 0, 0);
            refreshButton.Click += delegate { LoadAdminOrders(); };
            toolbar.Controls.Add(refreshButton);
            toolbar.Controls.Add(new Label
            {
                Text = "Move selected to",
                AutoSize = true,
                ForeColor = AppTheme.Muted,
                Margin = new Padding(22, 12, 6, 0)
            });
            processStatusComboBox = new ComboBox
            {
                Width = 125,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9.5f),
                Margin = new Padding(0, 7, 6, 0)
            };
            processStatusComboBox.Items.AddRange(new object[]
            {
                "Confirmed", "Packed", "Delivered", "Cancelled"
            });
            processStatusComboBox.SelectedIndex = 0;
            toolbar.Controls.Add(processStatusComboBox);
            var updateStatusButton = AppTheme.PrimaryButton("Update status");
            updateStatusButton.Width = 115;
            updateStatusButton.Height = 33;
            updateStatusButton.Margin = new Padding(0, 3, 6, 0);
            updateStatusButton.Click += UpdateOrderStatus_Click;
            toolbar.Controls.Add(updateStatusButton);
            var billButton = AppTheme.LinkButton("View bill");
            billButton.Width = 95;
            billButton.Height = 33;
            billButton.Margin = new Padding(0, 3, 0, 0);
            billButton.Click += ViewAdminBill_Click;
            toolbar.Controls.Add(billButton);

            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 355,
                BackColor = AppTheme.Background
            };
            card.Controls.Add(split);
            split.BringToFront();

            ordersGrid = CreateGrid();
            ordersGrid.Dock = DockStyle.Fill;
            AddTextColumn(ordersGrid, "OrderId", "Order #", 70);
            AddTextColumn(ordersGrid, "OrderDate", "Date", 120, true, "dd-MMM-yyyy HH:mm");
            AddTextColumn(ordersGrid, "CustomerName", "Customer", 155);
            AddTextColumn(ordersGrid, "TotalAmount", "Total", 80, true, "C2");
            AddTextColumn(ordersGrid, "OrderStatus", "Order status", 95);
            AddTextColumn(ordersGrid, "PaymentStatus", "Payment", 85);
            AddTextColumn(ordersGrid, "PaymentMethod", "Method", 120);
            ordersGrid.SelectionChanged += delegate { LoadSelectedOrderItems(); };
            split.Panel1.Controls.Add(ordersGrid);

            orderDetailsLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 48,
                AutoEllipsis = true,
                ForeColor = AppTheme.Muted,
                Font = new Font("Segoe UI", 9.5f),
                Padding = new Padding(5, 8, 5, 4)
            };
            split.Panel2.Controls.Add(orderDetailsLabel);

            orderItemsGrid = CreateGrid();
            orderItemsGrid.Dock = DockStyle.Fill;
            AddTextColumn(orderItemsGrid, "ProductName", "Product", 230);
            AddTextColumn(orderItemsGrid, "Price", "Price", 85, true, "C2");
            AddTextColumn(orderItemsGrid, "Quantity", "Quantity", 70);
            AddTextColumn(orderItemsGrid, "LineTotal", "Line total", 90, true, "C2");
            split.Panel2.Controls.Add(orderItemsGrid);
            orderItemsGrid.BringToFront();

            orderStatusComboBox.SelectedIndexChanged += delegate { LoadAdminOrders(); };
            Shown += delegate { LoadAdminOrders(); };
        }

        private void LoadCategories()
        {
            IList<CategoryOption> categories = orderService.GetCategories();
            categoryComboBox.DataSource = categories;
            categoryComboBox.DisplayMember = "CategoryName";
            categoryComboBox.ValueMember = "CategoryId";
        }

        private void LoadProducts()
        {
            if (productsGrid == null || categoryComboBox == null)
                return;
            int categoryId = 0;
            CategoryOption selected = categoryComboBox.SelectedItem as CategoryOption;
            if (selected != null)
                categoryId = selected.CategoryId;
            productsGrid.DataSource = orderService.GetProducts(searchTextBox.Text, categoryId);
        }

        private void LoadCart()
        {
            IList<CartLine> cart = orderService.GetCart(user.Id);
            cartGrid.DataSource = cart;
            decimal total = cart.Sum(line => line.LineTotal);
            totalLabel.Text = "Total: ₹" + total.ToString("N2");
            cartStatusLabel.Text = cart.Count == 0
                ? "Your cart is empty. Select a product to begin."
                : cart.Count + " product line(s) in your cart.";
        }

        private void LoadAddresses(int? selectAddressId)
        {
            IList<AddressOption> addresses = orderService.GetAddresses(user.Id);
            addressComboBox.DataSource = addresses;
            if (selectAddressId.HasValue)
            {
                AddressOption match = addresses.FirstOrDefault(
                    item => item.AddressId == selectAddressId.Value);
                if (match != null)
                    addressComboBox.SelectedItem = match;
            }
        }

        private void AddToCart_Click(object sender, EventArgs e)
        {
            ProductCatalogItem product = SelectedData<ProductCatalogItem>(productsGrid);
            if (product == null)
            {
                ShowWarning("Select a product first.");
                return;
            }
            try
            {
                orderService.AddToCart(user.Id, product.ProductId, (int)productQuantity.Value);
                productQuantity.Value = 1;
                LoadCart();
            }
            catch (Exception exception)
            {
                ShowWarning(exception.Message);
            }
        }

        private void UpdateCart_Click(object sender, EventArgs e)
        {
            CartLine line = SelectedData<CartLine>(cartGrid);
            if (line == null)
            {
                ShowWarning("Select a cart item first.");
                return;
            }
            try
            {
                orderService.UpdateCartQuantity(user.Id, line.CartItemId, (int)cartQuantity.Value);
                LoadCart();
            }
            catch (Exception exception)
            {
                ShowWarning(exception.Message);
            }
        }

        private void RemoveCartItem_Click(object sender, EventArgs e)
        {
            CartLine line = SelectedData<CartLine>(cartGrid);
            if (line == null)
            {
                ShowWarning("Select a cart item first.");
                return;
            }
            orderService.RemoveCartItem(user.Id, line.CartItemId);
            LoadCart();
        }

        private void AddAddress_Click(object sender, EventArgs e)
        {
            using (var form = new AddressForm())
            {
                if (form.ShowDialog(this) != DialogResult.OK)
                    return;
                try
                {
                    int addressId = orderService.AddAddress(user.Id, form.Address);
                    LoadAddresses(addressId);
                }
                catch (Exception exception)
                {
                    ShowWarning(exception.Message);
                }
            }
        }

        private void PlaceOrder_Click(object sender, EventArgs e)
        {
            AddressOption address = addressComboBox.SelectedItem as AddressOption;
            if (address == null)
            {
                ShowWarning("Add or select a delivery address.");
                return;
            }
            string paymentMethod = Convert.ToString(paymentComboBox.SelectedItem);
            if (MessageBox.Show(
                this,
                "Place this order using " + paymentMethod + "?",
                "Confirm order",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            try
            {
                int orderId = orderService.PlaceOrder(user.Id, address.AddressId, paymentMethod);
                MessageBox.Show(
                    this,
                    "Order #" + orderId + " was placed successfully.",
                    "Order confirmed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                LoadCart();
                LoadProducts();
            }
            catch (Exception exception)
            {
                ShowWarning(exception.Message);
            }
        }

        private void LoadAdminOrders()
        {
            if (ordersGrid == null || orderStatusComboBox == null)
                return;
            string status = Convert.ToString(orderStatusComboBox.SelectedItem);
            ordersGrid.DataSource = orderService.GetOrders(status);
            LoadSelectedOrderItems();
        }

        private void LoadSelectedOrderItems()
        {
            if (ordersGrid == null || orderItemsGrid == null)
                return;
            OrderRecord order = SelectedData<OrderRecord>(ordersGrid);
            if (order == null)
            {
                orderItemsGrid.DataSource = null;
                orderDetailsLabel.Text = "Select an order to see its products and delivery address.";
                return;
            }
            orderItemsGrid.DataSource = orderService.GetOrderItems(order.OrderId);
            orderDetailsLabel.Text = "Order #" + order.OrderId + "  •  " + order.DeliveryAddress;
        }

        private void UpdateOrderStatus_Click(object sender, EventArgs e)
        {
            OrderRecord order = SelectedData<OrderRecord>(ordersGrid);
            if (order == null)
            {
                ShowWarning("Select an order first.");
                return;
            }
            string newStatus = Convert.ToString(processStatusComboBox.SelectedItem);
            if (newStatus == "Cancelled" && MessageBox.Show(
                this,
                "Cancel order #" + order.OrderId + " and restore its stock?",
                "Confirm cancellation",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
                return;
            try
            {
                orderService.UpdateOrderStatus(order.OrderId, newStatus, user.Id);
                LoadAdminOrders();
                MessageBox.Show(this, "Order status updated successfully.", "QuickKart",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception exception)
            {
                ShowWarning(exception.Message);
            }
        }

        private void ViewAdminBill_Click(object sender, EventArgs e)
        {
            OrderRecord order = SelectedData<OrderRecord>(ordersGrid);
            if (order == null)
            {
                ShowWarning("Select an order first.");
                return;
            }
            using (var invoice = new InvoiceForm(
                orderService.GetOrder(order.OrderId),
                orderService.GetOrderItems(order.OrderId)))
            {
                invoice.ShowDialog(this);
            }
        }

        private static DataGridView CreateGrid()
        {
            return new DataGridView
            {
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ColumnHeadersHeight = 34,
                RowTemplate = { Height = 30 },
                EnableHeadersVisualStyles = false,
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(232, 239, 235),
                    ForeColor = AppTheme.Text,
                    Font = new Font("Segoe UI Semibold", 9f),
                    SelectionBackColor = Color.FromArgb(232, 239, 235),
                    SelectionForeColor = AppTheme.Text
                },
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Font = new Font("Segoe UI", 9f),
                    ForeColor = AppTheme.Text,
                    BackColor = Color.White,
                    SelectionBackColor = AppTheme.Primary,
                    SelectionForeColor = Color.White
                }
            };
        }

        private static void AddTextColumn(
            DataGridView grid,
            string property,
            string heading,
            int width,
            bool visible = true,
            string format = null)
        {
            var column = new DataGridViewTextBoxColumn
            {
                DataPropertyName = property,
                Name = property,
                HeaderText = heading,
                MinimumWidth = Math.Min(width, 50),
                FillWeight = width,
                Visible = visible
            };
            if (!string.IsNullOrEmpty(format))
                column.DefaultCellStyle.Format = format;
            grid.Columns.Add(column);
        }

        private static T SelectedData<T>(DataGridView grid) where T : class
        {
            if (grid == null || grid.SelectedRows.Count == 0)
                return null;
            return grid.SelectedRows[0].DataBoundItem as T;
        }

        private void ShowWarning(string message)
        {
            MessageBox.Show(
                this,
                message,
                "QuickKart Order Form",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }
}
