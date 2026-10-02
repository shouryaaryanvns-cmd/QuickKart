using System;
using System.Data;
using System.Data.SqlClient;
using QuickKart.Models;
using QuickKart.Services;

internal static class AuthSmokeTest
{
    private const string TestEmail = "codex.auth.smoke@quickkart.local";
    private const string TestPassword = "SmokeTest@123";
    private const string ConnectionString =
        @"Data Source=.\SQLEXPRESS;Initial Catalog=QuickKartDB;Integrated Security=True;TrustServerCertificate=True;Connection Timeout=10";

    private static int Main()
    {
        var service = new AuthenticationService();

        try
        {
            CleanupTestCustomer();
            service.TestConnection();
            Assert(service.Login("admin", "QuickKart@123") != null, "Admin login");
            Assert(service.Login("admin", "wrong-password") == null, "Wrong password rejection");

            int userId = service.RegisterCustomer(new RegistrationRequest
            {
                FullName = "Authentication Smoke Test",
                Email = TestEmail,
                PhoneNumber = "9999999999",
                Gender = null,
                DateOfBirth = new DateTime(2000, 1, 1),
                Password = TestPassword
            });
            Assert(userId > 0, "Customer registration");

            AuthenticatedUser customer = service.Login(TestEmail, TestPassword);
            Assert(customer != null && customer.Role == "Customer", "New customer login");

            Console.WriteLine("PASS: connection");
            Console.WriteLine("PASS: admin login");
            Console.WriteLine("PASS: incorrect-password rejection");
            Console.WriteLine("PASS: registration transaction");
            Console.WriteLine("PASS: registered customer login");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL: " + exception);
            return 1;
        }
        finally
        {
            CleanupTestCustomer();
        }
    }

    private static void Assert(bool condition, string operation)
    {
        if (!condition)
            throw new InvalidOperationException(operation + " failed.");
    }

    private static void CleanupTestCustomer()
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            connection.Open();
            using (var transaction = connection.BeginTransaction())
            {
                int userId;
                using (var find = new SqlCommand(
                    "SELECT UserId FROM dbo.Users WHERE Email=@Email;",
                    connection,
                    transaction))
                {
                    find.Parameters.Add("@Email", SqlDbType.NVarChar, 100).Value = TestEmail;
                    object value = find.ExecuteScalar();
                    if (value == null)
                    {
                        transaction.Commit();
                        return;
                    }
                    userId = (int)value;
                }

                using (var deleteCart = new SqlCommand(
                    "DELETE dbo.Cart WHERE UserId=@UserId;",
                    connection,
                    transaction))
                {
                    deleteCart.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                    deleteCart.ExecuteNonQuery();
                }

                using (var deleteUser = new SqlCommand(
                    "DELETE dbo.Users WHERE UserId=@UserId;",
                    connection,
                    transaction))
                {
                    deleteUser.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                    deleteUser.ExecuteNonQuery();
                }

                transaction.Commit();
            }
        }
    }
}
