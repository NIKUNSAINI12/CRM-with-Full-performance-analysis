using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ticketing_system_backend.Helpers
{
    public static class EncryptionHelper
    {
        // 256-bit key and 128-bit IV for AES-256 encryption
        private static readonly byte[] Key = Encoding.UTF8.GetBytes("T1ck3t1ng$yst3m@S3cur3K3y#2026!K"); // Exactly 32 bytes
        private static readonly byte[] Iv = Encoding.UTF8.GetBytes("T1ck3t1ng$yst3mI"); // Exactly 16 bytes

        public static string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return string.Empty;

            using (var aes = Aes.Create())
            {
                aes.Key = Key;
                aes.IV = Iv;
                var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
                using (var ms = new MemoryStream())
                {
                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        using (var sw = new StreamWriter(cs))
                        {
                            sw.Write(plainText);
                        }
                    }
                    return Convert.ToHexString(ms.ToArray()).ToLower();
                }
            }
        }

        public static string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return string.Empty;

            using (var aes = Aes.Create())
            {
                aes.Key = Key;
                aes.IV = Iv;
                var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                using (var ms = new MemoryStream(Convert.FromHexString(cipherText)))
                {
                    using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                    {
                        using (var sr = new StreamReader(cs))
                        {
                            return sr.ReadToEnd();
                        }
                    }
                }
            }
        }
    }
}
