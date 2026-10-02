using System;

namespace QuickKart.Models
{
    public sealed class CategoryAdminRecord
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; }
        public string CategoryImage { get; set; }
        public bool IsActive { get; set; }
        public int ProductCount { get; set; }
    }

    public sealed class ProductAdminRecord
    {
        public int ProductId { get; set; }
        public int CategoryId { get; set; }
        public string CategoryName { get; set; }
        public string ProductName { get; set; }
        public string Description { get; set; }
        public string Brand { get; set; }
        public decimal Price { get; set; }
        public decimal Discount { get; set; }
        public int Stock { get; set; }
        public string Unit { get; set; }
        public string ProductImage { get; set; }
        public bool IsAvailable { get; set; }
        public int ReorderLevel { get; set; }
    }

    public sealed class InventoryRecord
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public string CategoryName { get; set; }
        public int Stock { get; set; }
        public int ReorderLevel { get; set; }
        public string StockStatus { get; set; }
        public bool IsAvailable { get; set; }
    }

    public sealed class CustomerAdminRecord
    {
        public int UserId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
        public int OrderCount { get; set; }
        public decimal TotalSpent { get; set; }
    }

    public sealed class SalesSummary
    {
        public int TotalOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal AverageOrderValue { get; set; }
        public int ProductsSold { get; set; }
        public int LowStockProducts { get; set; }
    }

    public sealed class DailySalesRecord
    {
        public DateTime SalesDate { get; set; }
        public int TotalOrders { get; set; }
        public decimal TotalSales { get; set; }
    }

    public sealed class TopProductRecord
    {
        public string ProductName { get; set; }
        public int QuantitySold { get; set; }
        public decimal SalesAmount { get; set; }
    }
}
