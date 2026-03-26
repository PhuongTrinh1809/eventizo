using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Eventizo.Helper
{
    public static class EncryptionHelper
    {
        // 🔑 Khóa bí mật (nên lưu trong appsettings hoặc user secrets)
        private static readonly string Key = "Eventizo_ChatEncrypt_2025";

        public static string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText;

            using (Aes aes = Aes.Create())
            {
                aes.Key = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(Key));
                aes.IV = new byte[16]; 

                using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
                using var ms = new MemoryStream();
                using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                using (var sw = new StreamWriter(cs))
                {
                    sw.Write(plainText);
                }

                return Convert.ToBase64String(ms.ToArray());
            }
        }

        public static string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
                return cipherText;

            try
            {
                using (Aes aes = Aes.Create())
                {
                    aes.Key = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(Key));
                    aes.IV = new byte[16];

                    using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                    using var ms = new MemoryStream(Convert.FromBase64String(cipherText));
                    using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
                    using var sr = new StreamReader(cs);
                    return sr.ReadToEnd();
                }
            }
            catch
            {
                // ⚠️ Nếu không decrypt được → có thể là plaintext
                return cipherText;
            }
        }
    }
}
