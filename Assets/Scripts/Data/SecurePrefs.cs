using System;
using System.Text;
using UnityEngine;

namespace SmallGames
{
    /// <summary>
    /// 存档读写层：JSON 落盘前先过一道 XXTEA，避免玩家拿文本编辑器直接改 PlayerPrefs。
    /// 注意这是「提高篡改门槛」，不是「防破解」——密钥就在包体里，真要逆向一定能拿到。
    /// 旧版本存的明文 JSON 会被自动识别并就地迁移成密文，不会丢档。
    /// </summary>
    public static class SecurePrefs
    {
        private const string Secret = "SmallGames/save/v1/2026";
        private const uint Delta = 0x9E3779B9;
        private const int HeaderSize = 10; // magic(2) + 明文长度(4) + 校验(4)

        private static uint[] _key;

        private static uint[] Key
        {
            get { return _key ?? (_key = BuildKey()); }
        }

        public static bool HasKey(string key)
        {
            return PlayerPrefs.HasKey(key);
        }

        public static void Save()
        {
            PlayerPrefs.Save();
        }

        public static void DeleteKey(string key)
        {
            PlayerPrefs.DeleteKey(key);
        }

        /// <summary>读一段 JSON。读不到、或密文被改坏时返回 fallback。</summary>
        public static string GetString(string key, string fallback = null)
        {
            if (!PlayerPrefs.HasKey(key))
                return fallback;

            var stored = PlayerPrefs.GetString(key);
            if (string.IsNullOrEmpty(stored))
                return fallback;

            var json = TryDecrypt(stored);
            if (json != null)
                return json;

            // 解密失败：大概率是旧版本的明文存档。识别出来后立刻加密回写。
            if (stored[0] == '{')
            {
                SetString(key, stored);
                return stored;
            }

            Debug.LogWarning($"[存档] {key} 内容无法识别，已重置为默认");
            return fallback;
        }

        /// <summary>写一段 JSON，内部加密后落到 PlayerPrefs。</summary>
        public static void SetString(string key, string json)
        {
            if (string.IsNullOrEmpty(key))
                return;

            PlayerPrefs.SetString(key, Encrypt(json ?? string.Empty));
        }

        public static int GetInt(string key, int fallback)
        {
            if (!PlayerPrefs.HasKey(key))
                return fallback;

            var stored = PlayerPrefs.GetString(key);
            if (!string.IsNullOrEmpty(stored))
            {
                var json = TryDecrypt(stored);
                if (json != null)
                    return int.TryParse(json, out var parsed) ? parsed : fallback;

                // 旧版本直接存的是整数字符串
                if (int.TryParse(stored, out var legacy))
                {
                    SetInt(key, legacy);
                    return legacy;
                }
            }

            // 再兼容一次真正的旧明文 int
            var raw = PlayerPrefs.GetInt(key, fallback);
            SetInt(key, raw);
            return raw;
        }

        public static void SetInt(string key, int value)
        {
            if (string.IsNullOrEmpty(key))
                return;

            PlayerPrefs.SetString(key, Encrypt(value.ToString()));
        }

        #region 加解密

        private static string Encrypt(string json)
        {
            var plain = Encoding.UTF8.GetBytes(json);
            var length = plain.Length;
            int blockCount;
            var blocks = ToBlocks(plain, out blockCount);
            Cipher(blocks, Key, true);

            var body = ToBytes(blocks, blockCount * 4);
            var packed = new byte[HeaderSize + body.Length];
            var magic = (ushort)('S' | ('G' << 8));
            packed[0] = (byte)(magic & 0xFF);
            packed[1] = (byte)(magic >> 8);
            WriteInt(packed, 2, length);
            WriteInt(packed, 6, unchecked((int)Hash(body)));
            Buffer.BlockCopy(body, 0, packed, HeaderSize, body.Length);

            return Convert.ToBase64String(packed);
        }

        /// <summary>解密失败返回 null（调用方据此判断是不是旧明文）。</summary>
        private static string TryDecrypt(string stored)
        {
            byte[] packed;
            try
            {
                packed = Convert.FromBase64String(stored);
            }
            catch (Exception)
            {
                return null;
            }

            if (packed.Length < HeaderSize + 8)
                return null;
            if (packed[0] != (byte)'S' || packed[1] != (byte)'G')
                return null;

            var length = ReadInt(packed, 2);
            var checksum = ReadInt(packed, 6);
            if (length < 0 || length > packed.Length)
                return null;

            var body = new byte[packed.Length - HeaderSize];
            Buffer.BlockCopy(packed, HeaderSize, body, 0, body.Length);
            if (unchecked((int)Hash(body)) != checksum)
            {
                Debug.LogWarning("[存档] 校验失败，内容可能被改过");
                return null;
            }

            int blockCount;
            var blocks = ToBlocks(body, out blockCount);
            Cipher(blocks, Key, false);

            var plain = ToBytes(blocks, length);
            return Encoding.UTF8.GetString(plain);
        }

        /// <summary>原地加/解密 block 数组。</summary>
        private static void Cipher(uint[] v, uint[] k, bool encrypt)
        {
            var n = v.Length;
            if (n < 2)
                return;

            uint sum = 0;
            var rounds = 6 + 52 / n;

            if (encrypt)
            {
                var z = v[n - 1];
                for (var r = 0; r < rounds; r++)
                {
                    sum += Delta;
                    var e = (sum >> 2) & 3u;
                    for (var p = 0; p < n - 1; p++)
                    {
                        var y = v[p + 1];
                        z = v[p] += Mix(sum, y, z, p, e, k);
                    }

                    var y0 = v[0];
                    z = v[n - 1] += Mix(sum, y0, z, n - 1, e, k);
                }
            }
            else
            {
                sum = (uint)rounds * Delta;
                var y = v[0];
                for (var r = 0; r < rounds; r++)
                {
                    var e = (sum >> 2) & 3u;
                    for (var p = n - 1; p > 0; p--)
                    {
                        var zPrev = v[p - 1];
                        y = v[p] -= Mix(sum, y, zPrev, p, e, k);
                    }

                    var zLast = v[n - 1];
                    y = v[0] -= Mix(sum, y, zLast, 0, e, k);
                    sum -= Delta;
                }
            }
        }

        private static uint Mix(uint sum, uint y, uint z, int p, uint e, uint[] k)
        {
            return (((z >> 5) ^ (y << 2)) + ((y >> 3) ^ (z << 4))) ^ ((sum ^ y) + (k[(p & 3) ^ e] ^ z));
        }

        private static uint[] ToBlocks(byte[] data, out int blockCount)
        {
            blockCount = (data.Length + 3) / 4;
            if (blockCount < 2)
                blockCount = 2; // XXTEA 至少要两个 block

            var blocks = new uint[blockCount];
            for (var i = 0; i < data.Length; i++)
                blocks[i / 4] |= (uint)data[i] << ((i % 4) * 8);
            return blocks;
        }

        private static byte[] ToBytes(uint[] blocks, int length)
        {
            var result = new byte[length];
            for (var i = 0; i < length; i++)
                result[i] = (byte)(blocks[i / 4] >> ((i % 4) * 8));
            return result;
        }

        private static uint[] BuildKey()
        {
            var secret = Encoding.UTF8.GetBytes(Secret);
            var key = new uint[4];
            for (var i = 0; i < 4; i++)
            {
                var h = 0x811c9dc5u;
                for (var j = 0; j < secret.Length; j++)
                    h = (h ^ secret[j]) * 0x01000193u;
                key[i] = (h ^ (uint)i) * 0x01000193u;
            }

            return key;
        }

        private static uint Hash(byte[] data)
        {
            var h = 0x811c9dc5u;
            for (var i = 0; i < data.Length; i++)
                h = (h ^ data[i]) * 0x01000193u;
            return h;
        }

        private static void WriteInt(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
        }

        private static int ReadInt(byte[] buffer, int offset)
        {
            return buffer[offset]
                | (buffer[offset + 1] << 8)
                | (buffer[offset + 2] << 16)
                | (buffer[offset + 3] << 24);
        }

        #endregion
    }
}
