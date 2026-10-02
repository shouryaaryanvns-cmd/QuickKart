using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;
using QuickKart.Models;

namespace QuickKart.Forms
{
    public sealed class InvoiceForm : Form
    {
        private readonly OrderRecord order;
        private readonly IList<OrderItemRecord> items;
        private readonly PrintDocument printDocument = new PrintDocument();

        public InvoiceForm(OrderRecord order, IList<OrderItemRecord> items)
        {
            this.order = order ?? throw new ArgumentNullException("order");
            this.items = items ?? throw new ArgumentNullException("items");

            Text = "QuickKart - Invoice #" + order.OrderId;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(760, 650);
            BackColor = AppTheme.Background;
            Font = new Font("Segoe UI", 9f);

            var invoice = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(32)
            };
            Controls.Add(invoice);

            var actionBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 58,
                FlowDirection = FlowDirection.RightToLeft,
                BackColor = Color.White,
                Padding = new Padding(0, 8, 0, 4)
            };
            invoice.Controls.Add(actionBar);

            var closeButton = AppTheme.LinkButton("Close");
            closeButton.Width = 100;
            closeButton.Click += delegate { Close(); };
            actionBar.Controls.Add(closeButton);

            var previewButton = AppTheme.PrimaryButton("Print preview");
            previewButton.Width = 130;
            previewButton.Click += PreviewButton_Click;
            actionBar.Controls.Add(previewButton);

            var title = new Label
            {
                AutoSize = true,
                Text = "QuickKart",
                ForeColor = AppTheme.Primary,
                Font = new Font("Segoe UI Semibold", 28f, FontStyle.Bold),
                Location = new Point(32, 24)
            };
            invoice.Controls.Add(title);

            invoice.Controls.Add(new Label
            {
                AutoSize = true,
                Text = "PURCHASE INVOICE",
                ForeColor = AppTheme.Muted,
                Font = new Font("Segoe UI Semibold", 10f),
                Location = new Point(38, 78)
            });

            invoice.Controls.Add(new Label
            {
                AutoSize = true,
                Text = "Invoice #: " + order.OrderId + Environment.NewLine +
                       "Date: " + order.OrderDate.ToString("dd MMM yyyy, hh:mm tt") + Environment.NewLine +
                       "Customer: " + order.CustomerName,
                ForeColor = AppTheme.Text,
                Font = new Font("Segoe UI", 10f),
                Location = new Point(38, 115)
            });

            invoice.Controls.Add(new Label
            {
                AutoSize = false,
                Width = 390,
                Height = 80,
                Text = "Delivery address" + Environment.NewLine + order.DeliveryAddress,
                ForeColor = AppTheme.Text,
                Font = new Font("Segoe UI", 9.5f),
                Location = new Point(330, 115)
            });

            var grid = new DataGridView
            {
                Location = new Point(38, 205),
                Size = new Size(675, 280),
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "ProductName", HeaderText = "Product", FillWeight = 210
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Price", HeaderText = "Price", FillWeight = 75,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "C2" }
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Quantity", HeaderText = "Qty", FillWeight = 55
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "LineTotal", HeaderText = "Total", FillWeight = 80,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "C2" }
            });
            grid.DataSource = items;
            invoice.Controls.Add(grid);

            invoice.Controls.Add(new Label
            {
                AutoSize = true,
                Text = "Grand total: ₹" + order.TotalAmount.ToString("N2"),
                ForeColor = AppTheme.Text,
                Font = new Font("Segoe UI Semibold", 17f, FontStyle.Bold),
                Location = new Point(478, 505)
            });
            invoice.Controls.Add(new Label
            {
                AutoSize = true,
                Text = "Payment: " + order.PaymentMethod + "  |  " + order.PaymentStatus +
                       Environment.NewLine + "Order status: " + order.OrderStatus,
                ForeColor = AppTheme.Muted,
                Font = new Font("Segoe UI", 9.5f),
                Location = new Point(38, 510)
            });

            printDocument.DocumentName = "QuickKart Invoice " + order.OrderId;
            printDocument.PrintPage += PrintDocument_PrintPage;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                printDocument.Dispose();
            base.Dispose(disposing);
        }

        private void PreviewButton_Click(object sender, EventArgs e)
        {
            using (var preview = new PrintPreviewDialog())
            {
                preview.Document = printDocument;
                preview.Width = 1000;
                preview.Height = 700;
                preview.ShowDialog(this);
            }
        }

        private void PrintDocument_PrintPage(object sender, PrintPageEventArgs e)
        {
            Graphics graphics = e.Graphics;
            int left = e.MarginBounds.Left;
            int top = e.MarginBounds.Top;
            int right = e.MarginBounds.Right;
            using (var brandFont = new Font("Segoe UI", 24f, FontStyle.Bold))
            using (var headingFont = new Font("Segoe UI", 11f, FontStyle.Bold))
            using (var normalFont = new Font("Segoe UI", 9.5f))
            using (var totalFont = new Font("Segoe UI", 13f, FontStyle.Bold))
            using (var linePen = new Pen(Color.Gray))
            {
                graphics.DrawString("QuickKart", brandFont, Brushes.DarkGreen, left, top);
                graphics.DrawString("PURCHASE INVOICE", headingFont, Brushes.Gray, left, top + 50);
                graphics.DrawString(
                    "Invoice #: " + order.OrderId + Environment.NewLine +
                    "Date: " + order.OrderDate.ToString("dd MMM yyyy, hh:mm tt") + Environment.NewLine +
                    "Customer: " + order.CustomerName,
                    normalFont, Brushes.Black, left, top + 85);
                graphics.DrawString(
                    "Delivery address:" + Environment.NewLine + order.DeliveryAddress,
                    normalFont, Brushes.Black,
                    new RectangleF(left + 340, top + 85, 350, 90));

                int y = top + 190;
                graphics.DrawLine(linePen, left, y, right, y);
                graphics.DrawString("Product", headingFont, Brushes.Black, left, y + 8);
                graphics.DrawString("Price", headingFont, Brushes.Black, left + 390, y + 8);
                graphics.DrawString("Qty", headingFont, Brushes.Black, left + 490, y + 8);
                graphics.DrawString("Total", headingFont, Brushes.Black, left + 560, y + 8);
                y += 38;

                foreach (OrderItemRecord item in items)
                {
                    graphics.DrawString(item.ProductName, normalFont, Brushes.Black,
                        new RectangleF(left, y, 370, 28));
                    graphics.DrawString("₹" + item.Price.ToString("N2"), normalFont, Brushes.Black, left + 390, y);
                    graphics.DrawString(item.Quantity.ToString(), normalFont, Brushes.Black, left + 500, y);
                    graphics.DrawString("₹" + item.LineTotal.ToString("N2"), normalFont, Brushes.Black, left + 560, y);
                    y += 30;
                }

                graphics.DrawLine(linePen, left, y + 4, right, y + 4);
                y += 20;
                graphics.DrawString("Grand total: ₹" + order.TotalAmount.ToString("N2"),
                    totalFont, Brushes.Black, left + 450, y);
                graphics.DrawString(
                    "Payment: " + order.PaymentMethod + " (" + order.PaymentStatus + ")" +
                    Environment.NewLine + "Order status: " + order.OrderStatus,
                    normalFont, Brushes.Black, left, y + 4);
                graphics.DrawString("Thank you for shopping with QuickKart.",
                    headingFont, Brushes.DarkGreen, left, y + 75);
            }
            e.HasMorePages = false;
        }
    }
}
