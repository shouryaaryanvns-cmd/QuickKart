using System;
using System.Data;
using System.Data.SqlClient;
using System.Net.Mail;
using System.Text.RegularExpressions;
using QuickKart.Data;
using QuickKart.Models;
using QuickKart.Utilities;

namespace QuickKart.Services
{
    public sealed class AuthenticationService
    {
        public void TestConnection()
        {
            using (SqlConnection connection = DbConnectionFactory.Create())
            {
                connection.Open();
            }
        }

        public AuthenticatedUser Login(string loginName, string password)
        {
            loginName = (loginName ?? string.Empty).Trim();

            if (loginName.Length == 0 || string.IsNullOrEmpty(password))
                return null;

            using (SqlConnection connection = DbConnectionFactory.Create())
            {
                connection.Open();

                AuthenticatedUser admin = TryLoginAdmin(connection, loginName, password);
                if (admin != null)
                    return admin;

                return TryLoginCustomer(connection, loginName, password);
            }
        }

        public int RegisterCustomer(RegistrationRequest request)
        {
            ValidateRegistration(request);

            string normalizedEmail = request.Email.Trim().ToLowerInvariant();
            string passwordHash = PasswordHasher.HashPassword(request.Password);

            using (SqlConnection connection = DbConnectionFactory.Create())
            {
                connection.Open();

                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        if (EmailExists(connection, transaction, normalizedEmail))
                            throw new InvalidOperationException("An account with this email already exists.");

                        const string insertUserSql = @"
INSERT dbo.Users
    (FullName, Email, PasswordHash, PhoneNumber, Gender, DateOfBirth, IsActive)
VALUES
    (@FullName, @Email, @PasswordHash, @PhoneNumber, @Gender, @DateOfBirth, 1);

SELECT CONVERT(int, SCOPE_IDENTITY());";

                        int userId;
                        using (SqlCommand command = new SqlCommand(insertUserSql, connection, transaction))
                        {
                            command.Parameters.Add("@FullName", SqlDbType.NVarChar, 100).Value =
                                request.FullName.Trim();
                            command.Parameters.Add("@Email", SqlDbType.NVarChar, 100).Value =
                                normalizedEmail;
                            command.Parameters.Add("@PasswordHash", SqlDbType.NVarChar, 255).Value =
                                passwordHash;
                            command.Parameters.Add("@PhoneNumber", SqlDbType.VarChar, 15).Value =
                                request.PhoneNumber.Trim();
                            command.Parameters.Add("@Gender", SqlDbType.NVarChar, 10).Value =
                                string.IsNullOrWhiteSpace(request.Gender)
                                    ? (object)DBNull.Value
                                    : request.Gender;
                            command.Parameters.Add("@DateOfBirth", SqlDbType.Date).Value =
                                request.DateOfBirth.HasValue
                                    ? (object)request.DateOfBirth.Value.Date
                                    : DBNull.Value;

                            userId = (int)command.ExecuteScalar();
                        }

                        using (SqlCommand cartCommand = new SqlCommand(
                            "INSERT dbo.Cart (UserId) VALUES (@UserId);",
                            connection,
                            transaction))
                        {
                            cartCommand.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                            cartCommand.ExecuteNonQuery();
                        }

                        transaction.Commit();
                        return userId;
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        public static string ValidatePasswordStrength(string password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < 8)
                return "Password must contain at least 8 characters.";
            if (!Regex.IsMatch(password, "[A-Z]"))
                return "Password must contain an uppercase letter.";
            if (!Regex.IsMatch(password, "[a-z]"))
                return "Password must contain a lowercase letter.";
            if (!Regex.IsMatch(password, "[0-9]"))
                return "Password must contain a number.";
            if (!Regex.IsMatch(password, "[^A-Za-z0-9]"))
                return "Password must contain a special character.";

            return null;
        }

        private static AuthenticatedUser TryLoginAdmin(
            SqlConnection connection,
            string loginName,
            string password)
        {
            const string sql = @"
SELECT TOP (1) AdminId, FullName, Username, PasswordHash
FROM dbo.Admin
WHERE IsActive = 1
  AND (Username = @LoginName OR Email = @LoginName);";

            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@LoginName", SqlDbType.NVarChar, 100).Value = loginName;

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                        return null;

                    string storedHash = reader.GetString(reader.GetOrdinal("PasswordHash"));
                    if (!PasswordHasher.VerifyPassword(password, storedHash))
                        return null;

                    return new AuthenticatedUser
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("AdminId")),
                        FullName = reader.GetString(reader.GetOrdinal("FullName")),
                        LoginName = reader.GetString(reader.GetOrdinal("Username")),
                        Role = "Admin"
                    };
                }
            }
        }

        private static AuthenticatedUser TryLoginCustomer(
            SqlConnection connection,
            string loginName,
            string password)
        {
            const string sql = @"
SELECT TOP (1) UserId, FullName, Email, PasswordHash
FROM dbo.Users
WHERE IsActive = 1
  AND Email = @Email;";

            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@Email", SqlDbType.NVarChar, 100).Value =
                    loginName.ToLowerInvariant();

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                        return null;

                    string storedHash = reader.GetString(reader.GetOrdinal("PasswordHash"));
                    if (!PasswordHasher.VerifyPassword(password, storedHash))
                        return null;

                    return new AuthenticatedUser
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("UserId")),
                        FullName = reader.GetString(reader.GetOrdinal("FullName")),
                        LoginName = reader.GetString(reader.GetOrdinal("Email")),
                        Role = "Customer"
                    };
                }
            }
        }

        private static bool EmailExists(
            SqlConnection connection,
            SqlTransaction transaction,
            string email)
        {
            using (SqlCommand command = new SqlCommand(
                "SELECT COUNT(1) FROM dbo.Users WHERE Email = @Email;",
                connection,
                transaction))
            {
                command.Parameters.Add("@Email", SqlDbType.NVarChar, 100).Value = email;
                return (int)command.ExecuteScalar() > 0;
            }
        }

        private static void ValidateRegistration(RegistrationRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Trim().Length < 2)
                throw new InvalidOperationException("Please enter your full name.");
            if (request.FullName.Trim().Length > 100)
                throw new InvalidOperationException("Full name cannot exceed 100 characters.");

            try
            {
                var address = new MailAddress(request.Email ?? string.Empty);
                if (!string.Equals(address.Address, request.Email.Trim(), StringComparison.OrdinalIgnoreCase))
                    throw new FormatException();
            }
            catch
            {
                throw new InvalidOperationException("Please enter a valid email address.");
            }

            if (request.Email.Trim().Length > 100)
                throw new InvalidOperationException("Email cannot exceed 100 characters.");

            string phone = (request.PhoneNumber ?? string.Empty).Trim();
            if (!Regex.IsMatch(phone, @"^\d{10,15}$"))
                throw new InvalidOperationException("Phone number must contain 10 to 15 digits.");

            if (request.DateOfBirth.HasValue && request.DateOfBirth.Value.Date > DateTime.Today)
                throw new InvalidOperationException("Date of birth cannot be in the future.");

            string passwordError = ValidatePasswordStrength(request.Password);
            if (passwordError != null)
                throw new InvalidOperationException(passwordError);
        }
    }
}
