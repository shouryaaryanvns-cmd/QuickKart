using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using QuickKart.Models;
using QuickKart.Services;

namespace QuickKart.Forms
{
    internal static class AdminFormUi
    {
        public static DataGridView Grid()
        {
            return new DataGridView
            {
                AutoGenerateColumns = false, AllowUserToAddRows = false,
                ReadOnly = true, MultiSelect = false, RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.White, BorderStyle = BorderStyle.FixedSingle,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ColumnHeadersHeight = 34, RowTemplate = { Height = 30 }
            };
        }

        public static void Column(DataGridView grid, string property,
            string title, int weight, string format = null, bool visible = true)
        {
            var column = new DataGridViewTextBoxColumn
            {
                DataPropertyName = property, HeaderText = title,
                FillWeight = weight, Visible = visible
            };
            if (!string.IsNullOrEmpty(format)) column.DefaultCellStyle.Format = format;
            grid.Columns.Add(column);
        }

        public static Panel Header(Form form, string title, string subtitle)
        {
            var header = new Panel { Dock = DockStyle.Top, Height = 72, BackColor = Color.White };
            form.Controls.Add(header);
            header.Controls.Add(new Label
            {
                AutoSize = true, Text = title, Location = new Point(24, 14),
                ForeColor = AppTheme.Primary,
                Font = new Font("Segoe UI Semibold", 20f, FontStyle.Bold)
            });
            header.Controls.Add(new Label
            {
                AutoSize = true, Text = subtitle, Location = new Point(27, 48),
                ForeColor = AppTheme.Muted, Font = new Font("Segoe UI", 9f)
            });
            var close = AppTheme.LinkButton("Close");
            close.Width = 100; close.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            close.Location = new Point(header.Width - 125, 18);
            close.Click += delegate { form.Close(); };
            header.Controls.Add(close);
            header.Resize += delegate { close.Left = header.ClientSize.Width - 125; };
            return header;
        }

        public static TextBox Field(Control parent, string label, int x, int y, int width)
        {
            parent.Controls.Add(new Label
            {
                AutoSize = true, Text = label, Location = new Point(x, y),
                ForeColor = AppTheme.Muted, Font = new Font("Segoe UI", 8.5f)
            });
            var textBox = new TextBox
            {
                Location = new Point(x, y + 20), Width = width,
                Font = new Font("Segoe UI", 9.5f), BorderStyle = BorderStyle.FixedSingle
            };
            parent.Controls.Add(textBox);
            return textBox;
        }
    }

    public sealed class CatalogManagementForm : Form
    {
        private readonly int adminId;
        private readonly AdminService service = new AdminService();
        private DataGridView categoriesGrid;
        private TextBox categoryNameTextBox;
        private TextBox categoryImageTextBox;
        private CheckBox categoryActiveCheckBox;
        private int selectedCategoryId;

        private DataGridView productsGrid;
        private TextBox productSearchTextBox;
        private ComboBox productCategoryComboBox;
        private TextBox productNameTextBox;
        private TextBox productBrandTextBox;
        private TextBox productDescriptionTextBox;
        private TextBox productPriceTextBox;
        private TextBox productDiscountTextBox;
        private TextBox productStockTextBox;
        private TextBox productUnitTextBox;
        private TextBox productReorderTextBox;
        private TextBox productImageTextBox;
        private CheckBox productAvailableCheckBox;
        private int selectedProductId;

        public CatalogManagementForm(int adminId)
        {
            this.adminId = adminId;
            Text = "QuickKart - Catalog Management";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1180, 740); MinimumSize = new Size(1000, 650);
            BackColor = AppTheme.Background; Font = new Font("Segoe UI", 9f);
            Panel header = AdminFormUi.Header(this, "Catalog Management",
                "Create and maintain grocery categories and products.");

            var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(14, 6) };
            Controls.Add(tabs); header.BringToFront();
            var categoriesTab = new TabPage("Categories") { BackColor = AppTheme.Background, Padding = new Padding(18) };
            var productsTab = new TabPage("Products") { BackColor = AppTheme.Background, Padding = new Padding(18) };
            tabs.TabPages.Add(categoriesTab); tabs.TabPages.Add(productsTab);
            BuildCategories(categoriesTab); BuildProducts(productsTab);
            Shown += delegate { ReloadAll(); };
        }

        private void BuildCategories(Control tab)
        {
            var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 650 };
            tab.Controls.Add(split);
            categoriesGrid = AdminFormUi.Grid(); categoriesGrid.Dock = DockStyle.Fill;
            AdminFormUi.Column(categoriesGrid, "CategoryId", "ID", 45);
            AdminFormUi.Column(categoriesGrid, "CategoryName", "Category", 180);
            AdminFormUi.Column(categoriesGrid, "ProductCount", "Products", 70);
            AdminFormUi.Column(categoriesGrid, "IsActive", "Active", 55);
            categoriesGrid.SelectionChanged += delegate { PopulateCategory(); };
            split.Panel1.Controls.Add(categoriesGrid);

            var editor = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(24) };
            split.Panel2.Controls.Add(editor);
            editor.Controls.Add(new Label
            {
                Text = "Category details", AutoSize = true, Location = new Point(25, 25),
                Font = new Font("Segoe UI Semibold", 16f), ForeColor = AppTheme.Text
            });
            categoryNameTextBox = AdminFormUi.Field(editor, "Category name *", 28, 80, 390);
            categoryImageTextBox = AdminFormUi.Field(editor, "Image path", 28, 145, 390);
            categoryActiveCheckBox = new CheckBox
            {
                Text = "Active category", AutoSize = true, Checked = true,
                Location = new Point(28, 215), BackColor = Color.White
            };
            editor.Controls.Add(categoryActiveCheckBox);
            var newButton = AppTheme.LinkButton("New"); newButton.Width = 100;
            newButton.Location = new Point(100, 270); newButton.Click += delegate { ClearCategory(); };
            editor.Controls.Add(newButton);
            var saveButton = AppTheme.PrimaryButton("Save category"); saveButton.Width = 145;
            saveButton.Location = new Point(215, 264); saveButton.Click += SaveCategory_Click;
            editor.Controls.Add(saveButton);
        }

        private void BuildProducts(Control tab)
        {
            var layout = new SplitContainer
            {
                Dock = DockStyle.Fill, Orientation = Orientation.Horizontal,
                SplitterDistance = 335
            };
            tab.Controls.Add(layout);
            var listPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(10) };
            layout.Panel1.Controls.Add(listPanel);
            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, WrapContents = false };
            listPanel.Controls.Add(toolbar);
            productSearchTextBox = new TextBox { Width = 280, Font = new Font("Segoe UI", 10f), Margin = new Padding(0, 7, 8, 0) };
            toolbar.Controls.Add(productSearchTextBox);
            var search = AppTheme.PrimaryButton("Search"); search.Width = 85; search.Height = 32;
            search.Click += delegate { LoadProducts(); }; toolbar.Controls.Add(search);
            var clear = AppTheme.LinkButton("Clear"); clear.Width = 75; clear.Height = 32;
            clear.Click += delegate { productSearchTextBox.Clear(); LoadProducts(); }; toolbar.Controls.Add(clear);
            productSearchTextBox.KeyDown += delegate(object sender, KeyEventArgs e)
            { if (e.KeyCode == Keys.Enter) LoadProducts(); };

            productsGrid = AdminFormUi.Grid(); productsGrid.Dock = DockStyle.Fill;
            AdminFormUi.Column(productsGrid, "ProductId", "ID", 45);
            AdminFormUi.Column(productsGrid, "ProductName", "Product", 170);
            AdminFormUi.Column(productsGrid, "CategoryName", "Category", 110);
            AdminFormUi.Column(productsGrid, "Brand", "Brand", 85);
            AdminFormUi.Column(productsGrid, "Price", "Price", 65, "C2");
            AdminFormUi.Column(productsGrid, "Discount", "Discount %", 65);
            AdminFormUi.Column(productsGrid, "Stock", "Stock", 55);
            AdminFormUi.Column(productsGrid, "Unit", "Unit", 65);
            AdminFormUi.Column(productsGrid, "IsAvailable", "Available", 60);
            productsGrid.SelectionChanged += delegate { PopulateProduct(); };
            listPanel.Controls.Add(productsGrid); productsGrid.BringToFront();

            var editor = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, AutoScroll = true };
            layout.Panel2.Controls.Add(editor);
            productNameTextBox = AdminFormUi.Field(editor, "Product name *", 20, 20, 205);
            productBrandTextBox = AdminFormUi.Field(editor, "Brand", 240, 20, 160);
            productUnitTextBox = AdminFormUi.Field(editor, "Unit *", 415, 20, 120);
            productPriceTextBox = AdminFormUi.Field(editor, "Price *", 550, 20, 110);
            productDiscountTextBox = AdminFormUi.Field(editor, "Discount %", 675, 20, 105);
            productStockTextBox = AdminFormUi.Field(editor, "Initial stock", 795, 20, 100);
            productReorderTextBox = AdminFormUi.Field(editor, "Reorder level", 910, 20, 105);

            editor.Controls.Add(new Label
            {
                AutoSize = true, Text = "Category *", Location = new Point(20, 87),
                ForeColor = AppTheme.Muted, Font = new Font("Segoe UI", 8.5f)
            });
            productCategoryComboBox = new ComboBox
            {
                Location = new Point(20, 107), Width = 205,
                DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9.5f)
            };
            editor.Controls.Add(productCategoryComboBox);
            productImageTextBox = AdminFormUi.Field(editor, "Image path", 240, 87, 295);
            productDescriptionTextBox = AdminFormUi.Field(editor, "Description", 550, 87, 345);
            productAvailableCheckBox = new CheckBox
            {
                Text = "Available for sale", AutoSize = true, Checked = true,
                Location = new Point(910, 112), BackColor = Color.White
            };
            editor.Controls.Add(productAvailableCheckBox);

            var newButton = AppTheme.LinkButton("New product"); newButton.Width = 120;
            newButton.Location = new Point(760, 165); newButton.Click += delegate { ClearProduct(); };
            editor.Controls.Add(newButton);
            var saveButton = AppTheme.PrimaryButton("Save product"); saveButton.Width = 135;
            saveButton.Location = new Point(890, 159); saveButton.Click += SaveProduct_Click;
            editor.Controls.Add(saveButton);
            editor.Controls.Add(new Label
            {
                Text = "Use Inventory Management to change stock after a product has been created.",
                AutoSize = true, Location = new Point(20, 175),
                ForeColor = AppTheme.Muted, Font = new Font("Segoe UI", 8.5f)
            });
        }

        private void ReloadAll()
        {
            categoriesGrid.DataSource = service.GetCategories();
            IList<CategoryAdminRecord> categories = service.GetCategories();
            productCategoryComboBox.DataSource = categories;
            productCategoryComboBox.DisplayMember = "CategoryName";
            productCategoryComboBox.ValueMember = "CategoryId";
            LoadProducts();
        }

        private void LoadProducts() { productsGrid.DataSource = service.GetProducts(productSearchTextBox.Text); }

        private void PopulateCategory()
        {
            CategoryAdminRecord row = Selected<CategoryAdminRecord>(categoriesGrid);
            if (row == null) return;
            selectedCategoryId = row.CategoryId; categoryNameTextBox.Text = row.CategoryName;
            categoryImageTextBox.Text = row.CategoryImage; categoryActiveCheckBox.Checked = row.IsActive;
        }

        private void ClearCategory()
        {
            selectedCategoryId = 0; categoryNameTextBox.Clear(); categoryImageTextBox.Clear();
            categoryActiveCheckBox.Checked = true; categoryNameTextBox.Focus();
        }

        private void SaveCategory_Click(object sender, EventArgs e)
        {
            try
            {
                if (selectedCategoryId == 0)
                    service.AddCategory(categoryNameTextBox.Text, categoryImageTextBox.Text);
                else
                    service.UpdateCategory(new CategoryAdminRecord
                    {
                        CategoryId = selectedCategoryId, CategoryName = categoryNameTextBox.Text,
                        CategoryImage = categoryImageTextBox.Text, IsActive = categoryActiveCheckBox.Checked
                    });
                ClearCategory(); ReloadAll(); ShowSaved("Category saved successfully.");
            }
            catch (Exception exception) { ShowError(exception.Message); }
        }

        private void PopulateProduct()
        {
            ProductAdminRecord row = Selected<ProductAdminRecord>(productsGrid);
            if (row == null) return;
            selectedProductId = row.ProductId; productNameTextBox.Text = row.ProductName;
            productBrandTextBox.Text = row.Brand; productUnitTextBox.Text = row.Unit;
            productPriceTextBox.Text = row.Price.ToString("0.00");
            productDiscountTextBox.Text = row.Discount.ToString("0.00");
            productStockTextBox.Text = row.Stock.ToString(); productStockTextBox.Enabled = false;
            productReorderTextBox.Text = row.ReorderLevel.ToString();
            productDescriptionTextBox.Text = row.Description; productImageTextBox.Text = row.ProductImage;
            productAvailableCheckBox.Checked = row.IsAvailable;
            productCategoryComboBox.SelectedValue = row.CategoryId;
        }

        private void ClearProduct()
        {
            selectedProductId = 0;
            foreach (TextBox box in new[] { productNameTextBox, productBrandTextBox, productUnitTextBox,
                productPriceTextBox, productDiscountTextBox, productStockTextBox,
                productReorderTextBox, productDescriptionTextBox, productImageTextBox }) box.Clear();
            productDiscountTextBox.Text = "0"; productStockTextBox.Text = "0";
            productReorderTextBox.Text = "5"; productStockTextBox.Enabled = true;
            productAvailableCheckBox.Checked = true;
            if (productCategoryComboBox.Items.Count > 0) productCategoryComboBox.SelectedIndex = 0;
            productNameTextBox.Focus();
        }

        private void SaveProduct_Click(object sender, EventArgs e)
        {
            try
            {
                CategoryAdminRecord category = productCategoryComboBox.SelectedItem as CategoryAdminRecord;
                decimal price, discount; int stock, reorder;
                if (!decimal.TryParse(productPriceTextBox.Text, out price)) throw new InvalidOperationException("Enter a valid price.");
                if (!decimal.TryParse(productDiscountTextBox.Text, out discount)) throw new InvalidOperationException("Enter a valid discount.");
                if (!int.TryParse(productStockTextBox.Text, out stock)) throw new InvalidOperationException("Enter valid stock.");
                if (!int.TryParse(productReorderTextBox.Text, out reorder)) throw new InvalidOperationException("Enter a valid reorder level.");
                var product = new ProductAdminRecord
                {
                    ProductId = selectedProductId, CategoryId = category == null ? 0 : category.CategoryId,
                    ProductName = productNameTextBox.Text, Brand = productBrandTextBox.Text,
                    Unit = productUnitTextBox.Text, Price = price, Discount = discount,
                    Stock = stock, ReorderLevel = reorder, Description = productDescriptionTextBox.Text,
                    ProductImage = productImageTextBox.Text, IsAvailable = productAvailableCheckBox.Checked
                };
                if (selectedProductId == 0) service.AddProduct(product, adminId);
                else service.UpdateProduct(product);
                ClearProduct(); LoadProducts(); ShowSaved("Product saved successfully.");
            }
            catch (Exception exception) { ShowError(exception.Message); }
        }

        private static T Selected<T>(DataGridView grid) where T : class
        { return grid.SelectedRows.Count == 0 ? null : grid.SelectedRows[0].DataBoundItem as T; }
        private void ShowError(string text) { MessageBox.Show(this, text, "Catalog", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        private void ShowSaved(string text) { MessageBox.Show(this, text, "QuickKart", MessageBoxButtons.OK, MessageBoxIcon.Information); }
    }

    public sealed class InventoryManagementForm : Form
    {
        private readonly int adminId;
        private readonly AdminService service = new AdminService();
        private readonly DataGridView grid;
        private readonly TextBox searchTextBox;
        private readonly CheckBox lowOnlyCheckBox;
        private readonly NumericUpDown adjustmentInput;
        private readonly TextBox remarksTextBox;
        private readonly Label selectedLabel;

        public InventoryManagementForm(int adminId)
        {
            this.adminId = adminId;
            Text = "QuickKart - Inventory Management"; StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1020, 650); BackColor = AppTheme.Background;
            Panel header = AdminFormUi.Header(this, "Inventory Management",
                "Monitor stock levels and record audited stock adjustments.");
            var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(22), BackColor = AppTheme.Background };
            Controls.Add(body); header.BringToFront();
            var card = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(14) };
            body.Controls.Add(card);
            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 45, WrapContents = false };
            card.Controls.Add(toolbar);
            searchTextBox = new TextBox { Width = 260, Font = new Font("Segoe UI", 10f), Margin = new Padding(0, 7, 8, 0) };
            toolbar.Controls.Add(searchTextBox);
            lowOnlyCheckBox = new CheckBox { Text = "Low stock only", AutoSize = true, Margin = new Padding(5, 10, 8, 0) };
            lowOnlyCheckBox.CheckedChanged += delegate { LoadInventory(); }; toolbar.Controls.Add(lowOnlyCheckBox);
            var search = AppTheme.PrimaryButton("Search"); search.Width = 85; search.Height = 32;
            search.Click += delegate { LoadInventory(); }; toolbar.Controls.Add(search);
            grid = AdminFormUi.Grid(); grid.Dock = DockStyle.Fill;
            AdminFormUi.Column(grid, "ProductId", "ID", 45);
            AdminFormUi.Column(grid, "ProductName", "Product", 180);
            AdminFormUi.Column(grid, "CategoryName", "Category", 120);
            AdminFormUi.Column(grid, "Stock", "Current stock", 75);
            AdminFormUi.Column(grid, "ReorderLevel", "Reorder level", 75);
            AdminFormUi.Column(grid, "StockStatus", "Status", 80);
            AdminFormUi.Column(grid, "IsAvailable", "Available", 60);
            grid.SelectionChanged += delegate { UpdateSelectedLabel(); };
            card.Controls.Add(grid); grid.BringToFront();
            var adjust = new Panel { Dock = DockStyle.Bottom, Height = 108, BackColor = Color.FromArgb(247, 250, 248) };
            card.Controls.Add(adjust); adjust.BringToFront();
            selectedLabel = new Label { Text = "Select a product", AutoSize = true, Location = new Point(16, 13), Font = new Font("Segoe UI Semibold", 10f), ForeColor = AppTheme.Text };
            adjust.Controls.Add(selectedLabel);
            adjust.Controls.Add(new Label { Text = "Change (+/-)", AutoSize = true, Location = new Point(16, 48), ForeColor = AppTheme.Muted });
            adjustmentInput = new NumericUpDown { Minimum = -100000, Maximum = 100000, Location = new Point(105, 44), Width = 95, Font = new Font("Segoe UI", 10f) };
            adjust.Controls.Add(adjustmentInput);
            remarksTextBox = new TextBox { Location = new Point(220, 44), Width = 430, Font = new Font("Segoe UI", 10f) };
            remarksTextBox.Text = "Stock adjustment"; adjust.Controls.Add(remarksTextBox);
            var apply = AppTheme.PrimaryButton("Apply adjustment"); apply.Width = 150; apply.Location = new Point(675, 36);
            apply.Click += ApplyAdjustment_Click; adjust.Controls.Add(apply);
            Shown += delegate { LoadInventory(); };
        }

        private void LoadInventory() { grid.DataSource = service.GetInventory(searchTextBox.Text, lowOnlyCheckBox.Checked); }
        private InventoryRecord Selected() { return grid.SelectedRows.Count == 0 ? null : grid.SelectedRows[0].DataBoundItem as InventoryRecord; }
        private void UpdateSelectedLabel() { InventoryRecord row = Selected(); selectedLabel.Text = row == null ? "Select a product" : row.ProductName + " — current stock: " + row.Stock; }
        private void ApplyAdjustment_Click(object sender, EventArgs e)
        {
            InventoryRecord row = Selected();
            if (row == null) { MessageBox.Show(this, "Select a product first."); return; }
            try
            {
                service.AdjustStock(adminId, row.ProductId, (int)adjustmentInput.Value, remarksTextBox.Text);
                adjustmentInput.Value = 0; LoadInventory();
                MessageBox.Show(this, "Stock updated successfully.", "QuickKart", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception exception) { MessageBox.Show(this, exception.Message, "Inventory", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }
    }

    public sealed class CustomerManagementForm : Form
    {
        private readonly AdminService service = new AdminService();
        private readonly DataGridView grid;
        private readonly TextBox searchTextBox;

        public CustomerManagementForm()
        {
            Text = "QuickKart - Customer Management"; StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1050, 650); BackColor = AppTheme.Background;
            Panel header = AdminFormUi.Header(this, "Customer Management",
                "Search customers, review account activity, and control access.");
            var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(22), BackColor = AppTheme.Background };
            Controls.Add(body); header.BringToFront();
            var card = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14), BackColor = Color.White };
            body.Controls.Add(card);
            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 46, WrapContents = false };
            card.Controls.Add(toolbar);
            searchTextBox = new TextBox { Width = 300, Font = new Font("Segoe UI", 10f), Margin = new Padding(0, 7, 8, 0) };
            toolbar.Controls.Add(searchTextBox);
            var search = AppTheme.PrimaryButton("Search"); search.Width = 85; search.Height = 32; search.Click += delegate { LoadCustomers(); };
            toolbar.Controls.Add(search);
            var toggle = AppTheme.LinkButton("Activate / deactivate"); toggle.Width = 165; toggle.Height = 32; toggle.Click += Toggle_Click;
            toolbar.Controls.Add(toggle);
            grid = AdminFormUi.Grid(); grid.Dock = DockStyle.Fill;
            AdminFormUi.Column(grid, "UserId", "ID", 45);
            AdminFormUi.Column(grid, "FullName", "Customer", 155);
            AdminFormUi.Column(grid, "Email", "Email", 190);
            AdminFormUi.Column(grid, "PhoneNumber", "Phone", 95);
            AdminFormUi.Column(grid, "IsActive", "Active", 55);
            AdminFormUi.Column(grid, "CreatedDate", "Registered", 90, "dd-MMM-yyyy");
            AdminFormUi.Column(grid, "OrderCount", "Orders", 55);
            AdminFormUi.Column(grid, "TotalSpent", "Total spent", 75, "C2");
            card.Controls.Add(grid); grid.BringToFront();
            Shown += delegate { LoadCustomers(); };
        }
        private void LoadCustomers() { grid.DataSource = service.GetCustomers(searchTextBox.Text); }
        private void Toggle_Click(object sender, EventArgs e)
        {
            CustomerAdminRecord row = grid.SelectedRows.Count == 0 ? null : grid.SelectedRows[0].DataBoundItem as CustomerAdminRecord;
            if (row == null) { MessageBox.Show(this, "Select a customer first."); return; }
            try
            {
                service.SetCustomerActive(row.UserId, !row.IsActive); LoadCustomers();
                MessageBox.Show(this, "Customer account updated.", "QuickKart", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception exception) { MessageBox.Show(this, exception.Message, "Customers", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }
    }

    public sealed class ReportsForm : Form
    {
        private readonly AdminService service = new AdminService();
        private readonly DateTimePicker fromPicker;
        private readonly DateTimePicker toPicker;
        private readonly Label ordersValue;
        private readonly Label revenueValue;
        private readonly Label averageValue;
        private readonly Label soldValue;
        private readonly Label lowStockValue;
        private readonly DataGridView dailyGrid;
        private readonly DataGridView topGrid;
        private readonly DataGridView lowGrid;

        public ReportsForm()
        {
            Text = "QuickKart - Sales Reports"; StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1120, 700); BackColor = AppTheme.Background;
            Panel header = AdminFormUi.Header(this, "Sales & Stock Reports",
                "Review revenue, order performance, best sellers, and low stock.");
            var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20), BackColor = AppTheme.Background };
            Controls.Add(body); header.BringToFront();
            var filters = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 46, WrapContents = false };
            body.Controls.Add(filters);
            filters.Controls.Add(new Label { Text = "From", AutoSize = true, Margin = new Padding(0, 12, 5, 0) });
            fromPicker = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = 120, Value = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1), Margin = new Padding(0, 7, 12, 0) };
            filters.Controls.Add(fromPicker);
            filters.Controls.Add(new Label { Text = "To", AutoSize = true, Margin = new Padding(0, 12, 5, 0) });
            toPicker = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = 120, Value = DateTime.Today, Margin = new Padding(0, 7, 12, 0) };
            filters.Controls.Add(toPicker);
            var run = AppTheme.PrimaryButton("Run report"); run.Width = 105; run.Height = 32; run.Click += delegate { LoadReport(); };
            filters.Controls.Add(run);

            var cards = new TableLayoutPanel { Dock = DockStyle.Top, Height = 92, ColumnCount = 5, RowCount = 1, BackColor = AppTheme.Background };
            body.Controls.Add(cards); cards.BringToFront();
            for (int i = 0; i < 5; i++) cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
            ordersValue = AddCard(cards, 0, "Orders"); revenueValue = AddCard(cards, 1, "Revenue");
            averageValue = AddCard(cards, 2, "Average order"); soldValue = AddCard(cards, 3, "Products sold");
            lowStockValue = AddCard(cards, 4, "Low stock");

            var tabs = new TabControl { Dock = DockStyle.Fill };
            body.Controls.Add(tabs); tabs.BringToFront();
            dailyGrid = AdminFormUi.Grid();
            AdminFormUi.Column(dailyGrid, "SalesDate", "Date", 100, "dd-MMM-yyyy");
            AdminFormUi.Column(dailyGrid, "TotalOrders", "Orders", 80);
            AdminFormUi.Column(dailyGrid, "TotalSales", "Sales", 100, "C2");
            topGrid = AdminFormUi.Grid();
            AdminFormUi.Column(topGrid, "ProductName", "Product", 180);
            AdminFormUi.Column(topGrid, "QuantitySold", "Quantity sold", 85);
            AdminFormUi.Column(topGrid, "SalesAmount", "Sales", 100, "C2");
            lowGrid = AdminFormUi.Grid();
            AdminFormUi.Column(lowGrid, "ProductName", "Product", 170);
            AdminFormUi.Column(lowGrid, "CategoryName", "Category", 110);
            AdminFormUi.Column(lowGrid, "Stock", "Stock", 60);
            AdminFormUi.Column(lowGrid, "ReorderLevel", "Reorder level", 75);
            AdminFormUi.Column(lowGrid, "StockStatus", "Status", 80);
            AddTab(tabs, "Daily sales", dailyGrid); AddTab(tabs, "Best sellers", topGrid); AddTab(tabs, "Low stock", lowGrid);
            Shown += delegate { LoadReport(); };
        }

        private static Label AddCard(TableLayoutPanel parent, int column, string title)
        {
            var card = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(4), Padding = new Padding(12) };
            var value = new Label { Text = "0", AutoSize = true, Font = new Font("Segoe UI Semibold", 17f), ForeColor = AppTheme.Text, Location = new Point(12, 9) };
            card.Controls.Add(value); card.Controls.Add(new Label { Text = title, AutoSize = true, ForeColor = AppTheme.Muted, Location = new Point(14, 48) });
            parent.Controls.Add(card, column, 0); return value;
        }
        private static void AddTab(TabControl tabs, string title, Control content)
        { var tab = new TabPage(title) { Padding = new Padding(10) }; content.Dock = DockStyle.Fill; tab.Controls.Add(content); tabs.TabPages.Add(tab); }
        private void LoadReport()
        {
            if (fromPicker.Value.Date > toPicker.Value.Date) { MessageBox.Show(this, "From date cannot be after To date."); return; }
            SalesSummary summary = service.GetSalesSummary(fromPicker.Value, toPicker.Value);
            ordersValue.Text = summary.TotalOrders.ToString(); revenueValue.Text = "₹" + summary.TotalRevenue.ToString("N2");
            averageValue.Text = "₹" + summary.AverageOrderValue.ToString("N2"); soldValue.Text = summary.ProductsSold.ToString();
            lowStockValue.Text = summary.LowStockProducts.ToString();
            dailyGrid.DataSource = service.GetDailySales(fromPicker.Value, toPicker.Value);
            topGrid.DataSource = service.GetTopProducts(fromPicker.Value, toPicker.Value);
            lowGrid.DataSource = service.GetInventory(string.Empty, true);
        }
    }
}
