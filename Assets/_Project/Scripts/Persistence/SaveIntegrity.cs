using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Grimhand.Persistence
{
    public static class SaveIntegrity
    {
        static readonly byte[] AppSecret = Encoding.UTF8.GetBytes("Grimhand.Save.v1.20260710");

        public static string ComputeHash(PlayerProfileSaveData dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            var payload = CloneWithoutHash(dto);
            payload.integrityHash = "";
            var json = JsonUtility.ToJson(payload);
            return ComputeHmac(json);
        }

        public static bool Verify(PlayerProfileSaveData dto)
        {
            if (dto == null || string.IsNullOrEmpty(dto.integrityHash))
                return false;

            var expected = ComputeHash(dto);
            return SlowEquals(expected, dto.integrityHash);
        }

        public static void ApplyHash(PlayerProfileSaveData dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            dto.integrityHash = ComputeHash(dto);
        }

        static PlayerProfileSaveData CloneWithoutHash(PlayerProfileSaveData source) =>
            JsonUtility.FromJson<PlayerProfileSaveData>(JsonUtility.ToJson(source));

        static string ComputeHmac(string payload)
        {
            using var hmac = new HMACSHA256(AppSecret);
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload ?? ""));
            return Convert.ToBase64String(hash);
        }

        static bool SlowEquals(string a, string b)
        {
            if (a == null || b == null || a.Length != b.Length)
                return false;

            var diff = 0;
            for (var i = 0; i < a.Length; i++)
                diff |= a[i] ^ b[i];

            return diff == 0;
        }
    }
}
