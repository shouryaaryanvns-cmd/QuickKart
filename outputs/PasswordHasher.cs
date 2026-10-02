using System;
using System.Security.Cryptography;

namespace QuickKart.Utilities
{
    /// <summary>
    /// Creates and verifies the PBKDF2-SHA256 password format used by
    /// QuickKartDB_Enhancement_Migration.sql.
    /// </summary>
    public static class PasswordHasher
    {
        private const int SaltSize = 16;
        private const int HashSize = 32;
        private const int Iterations = 100000;
        private const string AlgorithmName = "PBKDF2-SHA256";

        public static string HashPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Password is required.", nameof(password));

            byte[] salt = new byte[SaltSize];
            using (RandomNumberGenerator random = RandomNumberGenerator.Create())
            {
                random.GetBytes(salt);
            }

            byte[] hash;
            using (var deriveBytes = new Rfc2898DeriveBytes(
                password,
                salt,
                Iterations,
                HashAlgorithmName.SHA256))
            {
                hash = deriveBytes.GetBytes(HashSize);
            }

            return string.Join(
                "$",
                AlgorithmName,
                Iterations,
                Convert.ToBase64String(salt),
                Convert.ToBase64String(hash));
        }

        public static bool VerifyPassword(string password, string storedValue)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedValue))
                return false;

            string[] parts = storedValue.Split('$');
            if (parts.Length != 4 || parts[0] != AlgorithmName)
                return false;

            int iterations;
            byte[] salt;
            byte[] expectedHash;

            try
            {
                iterations = int.Parse(parts[1]);
                salt = Convert.FromBase64String(parts[2]);
                expectedHash = Convert.FromBase64String(parts[3]);
            }
            catch (FormatException)
            {
                return false;
            }
            catch (OverflowException)
            {
                return false;
            }

            byte[] actualHash;
            using (var deriveBytes = new Rfc2898DeriveBytes(
                password,
                salt,
                iterations,
                HashAlgorithmName.SHA256))
            {
                actualHash = deriveBytes.GetBytes(expectedHash.Length);
            }

            return FixedTimeEquals(actualHash, expectedHash);
        }

        private static bool FixedTimeEquals(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
                return false;

            int difference = 0;
            for (int i = 0; i < left.Length; i++)
                difference |= left[i] ^ right[i];

            return difference == 0;
        }
    }
}
