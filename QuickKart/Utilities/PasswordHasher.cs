using System;
using System.Security.Cryptography;

namespace QuickKart.Utilities
{
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
                password, salt, Iterations, HashAlgorithmName.SHA256))
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
            if (parts.Length != 4 || !string.Equals(parts[0], AlgorithmName, StringComparison.Ordinal))
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
                password, salt, iterations, HashAlgorithmName.SHA256))
            {
                actualHash = deriveBytes.GetBytes(expectedHash.Length);
            }

            int difference = actualHash.Length ^ expectedHash.Length;
            int length = Math.Min(actualHash.Length, expectedHash.Length);
            for (int index = 0; index < length; index++)
                difference |= actualHash[index] ^ expectedHash[index];

            return difference == 0;
        }
    }
}
