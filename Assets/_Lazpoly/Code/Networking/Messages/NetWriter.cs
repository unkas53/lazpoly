using System;
using System.Collections.Generic;
using System.Text;

namespace Lazpoly.Net.Messages
{
    /// <summary>Little-endian binary writer with no allocations beyond its growing buffer.</summary>
    public sealed class NetWriter
    {
        private readonly List<byte> _bytes = new List<byte>(128);

        public void WriteByte(byte value) => _bytes.Add(value);

        public void WriteBool(bool value) => _bytes.Add(value ? (byte)1 : (byte)0);

        public void WriteInt(int value)
        {
            unchecked
            {
                _bytes.Add((byte)value);
                _bytes.Add((byte)(value >> 8));
                _bytes.Add((byte)(value >> 16));
                _bytes.Add((byte)(value >> 24));
            }
        }

        public void WriteULong(ulong value)
        {
            for (int shift = 0; shift < 64; shift += 8) _bytes.Add((byte)(value >> shift));
        }

        public void WriteString(string value)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(value ?? string.Empty);
            WriteInt(utf8.Length);
            _bytes.AddRange(utf8);
        }

        public byte[] ToArray() => _bytes.ToArray();
    }

    /// <summary>Reads what <see cref="NetWriter"/> wrote. Throws <see cref="FormatException"/> on truncated or bad data.</summary>
    public sealed class NetReader
    {
        public const int MaxStringBytes = 4096;
        public const int MaxCollection = 1024;

        private readonly byte[] _data;
        private int _position;

        public NetReader(byte[] data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
        }

        public bool AtEnd => _position == _data.Length;

        public byte ReadByte()
        {
            Require(1);
            return _data[_position++];
        }

        public bool ReadBool()
        {
            byte value = ReadByte();
            if (value > 1) throw new FormatException("Invalid bool.");
            return value == 1;
        }

        public int ReadInt()
        {
            Require(4);
            int value = _data[_position] | (_data[_position + 1] << 8) | (_data[_position + 2] << 16) | (_data[_position + 3] << 24);
            _position += 4;
            return value;
        }

        public ulong ReadULong()
        {
            Require(8);
            ulong value = 0;
            for (int i = 0; i < 8; i++) value |= (ulong)_data[_position + i] << (8 * i);
            _position += 8;
            return value;
        }

        public string ReadString()
        {
            int length = ReadInt();
            if (length < 0 || length > MaxStringBytes) throw new FormatException("Invalid string length.");
            Require(length);
            string value = Encoding.UTF8.GetString(_data, _position, length);
            _position += length;
            return value;
        }

        /// <summary>Reads a collection count and checks it against a sane upper bound.</summary>
        public int ReadCount()
        {
            int count = ReadInt();
            if (count < 0 || count > MaxCollection) throw new FormatException("Invalid collection size.");
            return count;
        }

        private void Require(int bytes)
        {
            if (_data.Length - _position < bytes) throw new FormatException("Message is truncated.");
        }
    }
}
