using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace PushBoxz.Data
{
    /// <summary>
    /// Encodes player-created levels into offline share codes and decodes them back
    /// into transient LevelDataAsset instances. The public code uses only A-Z, 0-9,
    /// and '-' so it behaves like a serial number when copied between players.
    /// </summary>
    public static class CustomLevelCodeCodec
    {
        private const string Prefix = "PBZ1";
        private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private const int BitsPerCell = 3;
        private const int ChecksumMod = 1679616; // 36^4

        private enum EncodedCell
        {
            Empty = 0,
            Floor = 1,
            Wall = 2,
            Goal = 3,
            Box = 4,
            Player = 5,
            BoxOnGoal = 6,
            PlayerOnGoal = 7
        }

        public static bool TryEncode(LevelDataAsset level, out string code, out string message)
        {
            code = string.Empty;
            if (!CustomLevelStorage.ValidatePlayable(level, out message))
            {
                return false;
            }

            if (level.width > 16 || level.height > 16)
            {
                message = "Level code supports maps up to 16x16.";
                return false;
            }

            var payload = BuildPayload(level);
            var encodedPayload = EncodeBase36(payload);
            var checksum = EncodeFixedBase36(ComputeChecksum(encodedPayload), 4);
            code = Prefix + GroupText(encodedPayload + checksum, 4);
            message = "Copied level code.";
            return true;
        }

        public static bool TryDecode(string code, out LevelDataAsset level, out string message)
        {
            level = null;
            var normalized = NormalizeCode(code);
            if (!normalized.StartsWith(Prefix, StringComparison.Ordinal))
            {
                message = "Level code must start with PBZ1.";
                return false;
            }

            var body = normalized.Substring(Prefix.Length);
            if (body.Length <= 4)
            {
                message = "Level code is too short.";
                return false;
            }

            var encodedPayload = body.Substring(0, body.Length - 4);
            var checksum = body.Substring(body.Length - 4);
            var expectedChecksum = EncodeFixedBase36(ComputeChecksum(encodedPayload), 4);
            if (checksum != expectedChecksum)
            {
                message = "Level code checksum failed.";
                return false;
            }

            var payload = DecodeBase36(encodedPayload);
            if (payload == null || payload.Count < 3)
            {
                message = "Level code payload is invalid.";
                return false;
            }

            if (!TryBuildLevel(payload, out level, out message))
            {
                if (level != null)
                {
                    UnityEngine.Object.Destroy(level);
                    level = null;
                }

                return false;
            }

            return true;
        }

        private static List<byte> BuildPayload(LevelDataAsset level)
        {
            var bits = new BitWriter();
            bits.Write(1, 4);
            bits.Write(level.width - 1, 4);
            bits.Write(level.height - 1, 4);

            var boxes = new HashSet<Vector2Int>(level.boxStarts ?? new List<Vector2Int>());
            for (var y = 0; y < level.height; y++)
            {
                for (var x = 0; x < level.width; x++)
                {
                    var position = new Vector2Int(x, y);
                    var hasGoal = level.HasGoal(x, y);
                    var hasBox = boxes.Contains(position);
                    var hasPlayer = level.playerStart == position;
                    bits.Write((int)GetEncodedCell(level.GetBaseTile(x, y), hasGoal, hasBox, hasPlayer), BitsPerCell);
                }
            }

            return bits.ToBytes();
        }

        private static bool TryBuildLevel(List<byte> payload, out LevelDataAsset level, out string message)
        {
            level = null;
            var bits = new BitReader(payload);
            if (!bits.TryRead(4, out var version) || version != 1)
            {
                message = "Unsupported level code version.";
                return false;
            }

            if (!bits.TryRead(4, out var widthValue) || !bits.TryRead(4, out var heightValue))
            {
                message = "Level code size data is invalid.";
                return false;
            }

            var width = widthValue + 1;
            var height = heightValue + 1;
            if (width < 2 || height < 2 || width > 16 || height > 16)
            {
                message = "Level code map size is invalid.";
                return false;
            }

            var cells = new List<TileCell>(width * height);
            var boxes = new List<Vector2Int>();
            var playerStart = new Vector2Int(-1, -1);
            var playerCount = 0;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    if (!bits.TryRead(BitsPerCell, out var cellValue) || cellValue < 0 || cellValue > 7)
                    {
                        message = "Level code cell data is invalid.";
                        return false;
                    }

                    var position = new Vector2Int(x, y);
                    var encodedCell = (EncodedCell)cellValue;
                    cells.Add(new TileCell(x, y, GetBaseTile(encodedCell), HasGoal(encodedCell)));
                    if (HasBox(encodedCell))
                    {
                        boxes.Add(position);
                    }

                    if (HasPlayer(encodedCell))
                    {
                        playerCount++;
                        playerStart = position;
                    }
                }
            }

            if (playerCount != 1)
            {
                message = "Level code must contain one player.";
                return false;
            }

            level = CustomLevelStorage.CreateRuntimeLevel(
                string.Empty,
                width,
                height,
                playerStart,
                cells,
                boxes);

            if (!CustomLevelStorage.ValidatePlayable(level, out message))
            {
                return false;
            }

            message = "Level code loaded.";
            return true;
        }

        private static EncodedCell GetEncodedCell(BaseTileType baseType, bool hasGoal, bool hasBox, bool hasPlayer)
        {
            if (hasPlayer)
            {
                return hasGoal ? EncodedCell.PlayerOnGoal : EncodedCell.Player;
            }

            if (hasBox)
            {
                return hasGoal ? EncodedCell.BoxOnGoal : EncodedCell.Box;
            }

            if (baseType == BaseTileType.Wall)
            {
                return EncodedCell.Wall;
            }

            if (baseType == BaseTileType.Empty)
            {
                return EncodedCell.Empty;
            }

            return hasGoal ? EncodedCell.Goal : EncodedCell.Floor;
        }

        private static BaseTileType GetBaseTile(EncodedCell cell)
        {
            return cell == EncodedCell.Wall ? BaseTileType.Wall : cell == EncodedCell.Empty ? BaseTileType.Empty : BaseTileType.Floor;
        }

        private static bool HasGoal(EncodedCell cell)
        {
            return cell == EncodedCell.Goal || cell == EncodedCell.BoxOnGoal || cell == EncodedCell.PlayerOnGoal;
        }

        private static bool HasBox(EncodedCell cell)
        {
            return cell == EncodedCell.Box || cell == EncodedCell.BoxOnGoal;
        }

        private static bool HasPlayer(EncodedCell cell)
        {
            return cell == EncodedCell.Player || cell == EncodedCell.PlayerOnGoal;
        }

        private static string NormalizeCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(code.Length);
            for (var i = 0; i < code.Length; i++)
            {
                var c = char.ToUpperInvariant(code[i]);
                if (c == '-')
                {
                    continue;
                }

                if (c >= 'A' && c <= 'Z' || c >= '0' && c <= '9')
                {
                    builder.Append(c);
                }
            }

            return builder.ToString();
        }

        private static string GroupText(string text, int groupSize)
        {
            var builder = new StringBuilder(Prefix.Length + 1 + text.Length + text.Length / groupSize);
            for (var i = 0; i < text.Length; i++)
            {
                if (i % groupSize == 0)
                {
                    builder.Append('-');
                }

                builder.Append(text[i]);
            }

            return builder.ToString();
        }

        private static string EncodeBase36(List<byte> bytes)
        {
            var bitLength = bytes.Count * 8;
            var digits = Mathf.CeilToInt(bitLength / 5.169925f);
            var value = new List<byte>(bytes);
            var builder = new StringBuilder(Mathf.Max(1, digits));
            while (HasNonZeroByte(value))
            {
                var remainder = DivideBytes(value, 36);
                builder.Insert(0, Alphabet[remainder]);
            }

            while (builder.Length < digits)
            {
                builder.Insert(0, '0');
            }

            return builder.ToString();
        }

        private static List<byte> DecodeBase36(string text)
        {
            var value = new List<byte> { 0 };
            for (var i = 0; i < text.Length; i++)
            {
                var digit = Alphabet.IndexOf(text[i]);
                if (digit < 0)
                {
                    return null;
                }

                MultiplyBytes(value, 36);
                AddByte(value, digit);
            }

            return value;
        }

        private static bool HasNonZeroByte(List<byte> bytes)
        {
            for (var i = 0; i < bytes.Count; i++)
            {
                if (bytes[i] != 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static int DivideBytes(List<byte> bytes, int divisor)
        {
            var remainder = 0;
            for (var i = 0; i < bytes.Count; i++)
            {
                var value = remainder * 256 + bytes[i];
                bytes[i] = (byte)(value / divisor);
                remainder = value % divisor;
            }

            return remainder;
        }

        private static void MultiplyBytes(List<byte> bytes, int multiplier)
        {
            var carry = 0;
            for (var i = bytes.Count - 1; i >= 0; i--)
            {
                var value = bytes[i] * multiplier + carry;
                bytes[i] = (byte)(value & 0xFF);
                carry = value >> 8;
            }

            while (carry > 0)
            {
                bytes.Insert(0, (byte)(carry & 0xFF));
                carry >>= 8;
            }
        }

        private static void AddByte(List<byte> bytes, int addend)
        {
            var carry = addend;
            for (var i = bytes.Count - 1; i >= 0 && carry > 0; i--)
            {
                var value = bytes[i] + carry;
                bytes[i] = (byte)(value & 0xFF);
                carry = value >> 8;
            }

            while (carry > 0)
            {
                bytes.Insert(0, (byte)(carry & 0xFF));
                carry >>= 8;
            }
        }

        private static int ComputeChecksum(string text)
        {
            uint hash = 2166136261;
            for (var i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= 16777619;
            }

            return (int)(hash % ChecksumMod);
        }

        private static string EncodeFixedBase36(int value, int length)
        {
            var chars = new char[length];
            for (var i = length - 1; i >= 0; i--)
            {
                chars[i] = Alphabet[value % 36];
                value /= 36;
            }

            return new string(chars);
        }

        private class BitWriter
        {
            private readonly List<byte> bytes = new List<byte>();
            private int currentByte;
            private int bitCount;

            public void Write(int value, int bits)
            {
                for (var i = bits - 1; i >= 0; i--)
                {
                    currentByte = currentByte << 1 | (value >> i & 1);
                    bitCount++;
                    if (bitCount == 8)
                    {
                        bytes.Add((byte)currentByte);
                        currentByte = 0;
                        bitCount = 0;
                    }
                }
            }

            public List<byte> ToBytes()
            {
                if (bitCount > 0)
                {
                    bytes.Add((byte)(currentByte << (8 - bitCount)));
                    currentByte = 0;
                    bitCount = 0;
                }

                return new List<byte>(bytes);
            }
        }

        private class BitReader
        {
            private readonly List<byte> bytes;
            private int bitIndex;

            public BitReader(List<byte> bytes)
            {
                this.bytes = bytes ?? new List<byte>();
            }

            public bool TryRead(int bits, out int value)
            {
                value = 0;
                if (bitIndex + bits > bytes.Count * 8)
                {
                    return false;
                }

                for (var i = 0; i < bits; i++)
                {
                    var byteIndex = bitIndex / 8;
                    var bitOffset = 7 - bitIndex % 8;
                    value = value << 1 | (bytes[byteIndex] >> bitOffset & 1);
                    bitIndex++;
                }

                return true;
            }
        }
    }
}
