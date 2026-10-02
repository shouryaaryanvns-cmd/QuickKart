using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Net.Mail;
using System.Text.RegularExpressions;
using QuickKart.Data;
using QuickKart.Models;
using QuickKart.Utilities;

namespace QuickKart.Services
{
    public sealed class CustomerService
    {
        public CustomerProfileRecord GetProfile(int userId)
        {
            const string sql = @"
SELECT UserId, FullName, Email, PhoneNumber, Gender,
       DateOfBirth, IsActive, CreatedDate
FROM dbo.Users
WHERE UserId = @UserId;";

            using (SqlConnection connection = DbConnectionFactory.Create())
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                        throw new InvalidOperationException("Customer profile was not found.");
                    return new CustomerProfileRecord
                    {
                        UserId = reader.GetInt32(0),
                        FullName = reader.GetString(1),
                        Email = reader.GetString(2),
                        PhoneNumber = reader.GetString(3),
                        Gender = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                        DateOfBirth = reader.IsDBNull(5)
                            ? (DateTime?)null
                            : reader.GetDateTime(5),
                        IsActive = reader.GetBoolean(6),
                        CreatedDate = reader.GetDateTime(7)
                    };
                }
            }
        }

        public void UpdateProfile(CustomerProfileRecord profile)
        {
            if (profile == null)
                throw new ArgumentNullException("profile");
            if (string.IsNullOrWhiteSpace(profile.FullName) || profile.FullName.Trim().Length < 2)
                throw new InvalidOperationException("Enter your full name.");
            if (!IsValidEmail(profile.Email))
                throw new InvalidOperationException("Enter a valid email address.");
            if (!Regex.IsMatch((profile.PhoneNumber ?? string.Empty).Trim(), @"^\d{10,15}$"))
                throw new InvalidOperationException("Phone number must contain 10 to 15 digits.");
            if (profile.DateOfBirth.HasValue && profile.DateOfBirth.Value.Date > DateTime.Today)
                throw new InvalidOperationException("Date of birth cannot be in the future.");

            const string sql = @"
UPDATE dbo.Users
SET FullName = @FullName,
    Email = @Email,
    PhoneNumber = @Phone,
    Gender = @Gender,
    DateOfBirth = @DateOfBirth
WHERE UserId = @UserId;";

            try
            {
                using (SqlConnection connection = DbConnectionFactory.Create())
                using (SqlCommand command = new SqlCommand(sql, connection))
                {
                    command.Parameters.Add("@FullName", SqlDbType.NVarChar, 100).Value = profile.FullName.Trim();
                    command.Parameters.Add("@Email", SqlDbType.NVarChar, 100).Value = profile.Email.Trim().ToLowerInvariant();
                    command.Parameters.Add("@Phone", SqlDbType.VarChar, 15).Value = profile.PhoneNumber.Trim();
                    command.Parameters.Add("@Gender", SqlDbType.NVarChar, 10).Value =
                        string.IsNullOrWhiteSpace(profile.Gender) ? (object)DBNull.Value : profile.Gender.Trim();
                    command.Parameters.Add("@DateOfBirth", SqlDbType.Date).Value =
                        profile.DateOfBirth.HasValue ? (object)profile.DateOfBirth.Value.Date : DBNull.Value;
                    command.Parameters.Add("@UserId", SqlDbType.Int).Value = profile.UserId;
                    connection.Open();
                    if (command.ExecuteNonQuery() != 1)
                        throw new InvalidOperationException("Profile could not be updated.");
                }
            }
            catch (SqlException exception)
            {
                if (exception.Number == 2601 || exception.Number == 2627)
                    throw new InvalidOperationException("This email address is already registered.");
                throw;
            }
        }

        public void ChangePassword(int userId, string currentPassword, string newPassword)
        {
            string passwordError = AuthenticationService.ValidatePasswordStrength(newPassword);
            if (passwordError != null)
                throw new InvalidOperationException(passwordError);

            using (SqlConnection connection = DbConnectionFactory.Create())
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        string storedHash;
                        using (SqlCommand findCommand = new SqlCommand(
                            "SELECT PasswordHash FROM dbo.Users WITH (UPDLOCK, ROWLOCK) WHERE UserId=@UserId AND IsActive=1;",
                            connection, transaction))
                        {
                            findCommand.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                            object value = findCommand.ExecuteScalar();
                            if (value == null)
                                throw new InvalidOperationException("Customer account is unavailable.");
                            storedHash = Convert.ToString(value);
                        }

                        if (!PasswordHasher.VerifyPassword(currentPassword, storedHash))
                            throw new InvalidOperationException("Current password is incorrect.");
                        if (PasswordHasher.VerifyPassword(newPassword, storedHash))
                            throw new InvalidOperationException("New password must be different from the current password.");

                        using (SqlCommand updateCommand = new SqlCommand(
                            "UPDATE dbo.Users SET PasswordHash=@PasswordHash WHERE UserId=@UserId;",
                            connection, transaction))
                        {
                            updateCommand.Parameters.Add("@PasswordHash", SqlDbType.NVarChar, 255).Value =
                                PasswordHasher.HashPassword(newPassword);
                            updateCommand.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                            updateCommand.ExecuteNonQuery();
                        }
                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        public IList<OrderRecord> GetOrderHistory(int userId)
        {
            var orders = new List<OrderRecord>();
            const string sql = @"
SELECT o.OrderId, o.OrderDate, u.FullName, o.TotalAmount,
       o.OrderStatus, o.PaymentStatus, o.PaymentMethod,
       o.DeliveryAddressSnapshot
FROM dbo.Orders o
JOIN dbo.Users u ON u.UserId = o.UserId
WHERE o.UserId = @UserId
ORDER BY o.OrderDate DESC, o.OrderId DESC;";

            using (SqlConnection connection = DbConnectionFactory.Create())
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        orders.Add(new OrderRecord
                        {
                            OrderId = reader.GetInt32(0),
                            OrderDate = reader.GetDateTime(1),
                            CustomerName = reader.GetString(2),
                            TotalAmount = reader.GetDecimal(3),
                            OrderStatus = reader.GetString(4),
                            PaymentStatus = reader.GetString(5),
                            PaymentMethod = reader.GetString(6),
                            DeliveryAddress = reader.GetString(7)
                        });
                    }
                }
            }
            return orders;
        }

        private static bool IsValidEmail(string email)
        {
            try
            {
                var address = new MailAddress(email ?? string.Empty);
                return string.Equals(address.Address, email.Trim(), StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }
}
