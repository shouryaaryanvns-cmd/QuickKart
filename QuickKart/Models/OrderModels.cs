using System;

namespace QuickKart.Models
{
    public sealed class CategoryOption
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; }

        public override string ToString()
        {
            return CategoryName;
        }
    }

    public sealed class ProductCatalogItem
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public string CategoryName { get; set; }
        public string Brand { get; set; }
        public string Unit { get; set; }
        public decimal OriginalPrice { get; set; }
        public decimal DiscountPercent { get; set; }
        public decimal SellingPrice { get; set; }
        public int Stock { get; set; }
    }

    public sealed class CartLine
    {
        public int CartItemId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public string Unit { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public int AvailableStock { get; set; }
        public decimal LineTotal { get; set; }
    }

    public sealed class AddressOption
    {
        public int AddressId { get; set; }
        public string DisplayText { get; set; }

        public override string ToString()
        {
            return DisplayText;
        }
    }

    public sealed class AddressRequest
    {
        public string HouseNo { get; set; }
        public string Street { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Pincode { get; set; }
        public string Landmark { get; set; }
    }

    public sealed class OrderRecord
    {
        public int OrderId { get; set; }
        public DateTime OrderDate { get; set; }
        public string CustomerName { get; set; }
        public decimal TotalAmount { get; set; }
        public string OrderStatus { get; set; }
        public string PaymentStatus { get; set; }
        public string PaymentMethod { get; set; }
        public string DeliveryAddress { get; set; }
    }

    public sealed class OrderItemRecord
    {
        public string ProductName { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public decimal LineTotal { get; set; }
    }
}
