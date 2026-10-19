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
            // Remove the appended v8 suffix first so historical unlock fields
            // are again at the end of the canonical v5/v6/v7 payload.
            if(dto.saveVersion < 8)
                json = json.Substring(0, json.LastIndexOf(",\"achievementIds\":", StringComparison.Ordinal)) + "}";
            if(dto.saveVersion < 7)
                json = System.Text.RegularExpressions.Regex.Replace(json, ",\"lichUnlocked\":(?:true|false)(?=})", "");
            // Older saves were signed before the unlock field existed.
            if(dto.saveVersion < 6)
                json = System.Text.RegularExpressions.Regex.Replace(json, ",\"berserkerUnlocked\":(?:true|false)(?=})", "");
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
