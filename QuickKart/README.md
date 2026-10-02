# QuickKart – Online Grocery Shopping System

QuickKart is a C# Windows Forms desktop application backed by Microsoft SQL
Server. It implements customer shopping and administrator store-management
workflows for a BCA final-year project.

## Implemented modules

### Customer

- Registration and secure login
- Profile update and password change
- Product search and category filtering
- Persistent shopping cart
- Saved delivery addresses
- Transactional order placement
- Cash, COD, Card and UPI simulation
- Order history and status tracking
- Purchase bill and print preview

### Administrator

- Secure administrator login
- Role-based dashboard
- Category management
- Product and pricing management
- Inventory levels and audited stock adjustments
- Customer records and account activation
- Order processing through Pending, Confirmed, Packed and Delivered
- Safe cancellation, payment refund state and stock restoration
- Purchase bill and print preview
- Date-range sales summary
- Daily sales, best-selling product and low-stock reports

## Requirements

- Windows
- Visual Studio 2019
- .NET Framework 4.7.2
- Microsoft SQL Server / SQL Server Express
- SQL Server instance: `.\SQLEXPRESS`
- Database: `QuickKartDB`

## Open and run

Open `QuickKart.sln` in Visual Studio 2019, then press `F5`.

The connection string is stored in `App.config`:

```text
Data Source=.\SQLEXPRESS;Initial Catalog=QuickKartDB;Integrated Security=True
```

## Seeded logins

Administrator:

```text
Username: admin
Password: QuickKart@123
```

Customer:

```text
Email: arjun.demo@quickkart.local
Password: Customer@123
```

Change seeded passwords before using real personal information.

## Important behavior

- Checkout recalculates prices from SQL Server and locks stock rows.
- An order is committed only if every item has sufficient stock.
- Cancelling an eligible order restores stock transactionally.
- Inventory changes are recorded in `InventoryTransactions`.
- Product names and prices are copied into `OrderItems` for historical bills.
- Card and UPI payment processing is simulated for academic demonstration.
