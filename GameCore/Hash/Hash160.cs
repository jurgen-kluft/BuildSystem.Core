﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace GameCore
{
    public class Hash160
    {
        public static readonly byte[] hash_null_ = new byte[20] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        public static readonly byte[] hash_error_ = new byte[20] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };

        public static Hash160 Null { get { return new Hash160(hash_null_, 0); } }
        public static Hash160 Empty { get { return new Hash160(hash_null_, 0); } }
        public static Hash160 Error { get { return new Hash160(hash_error_, 0); } }

        public bool IsErrorHash()
        {
            bool equal = (hash_[0] == hash_error_[0]);
            for (int j = 1; j < Size && equal; j++)
                equal = (hash_[j] == hash_error_[j]);
            return equal;
        }

        public bool IsNullHash()
        {
            bool equal = (hash_[0] == hash_null_[0]);
            for (int j = 1; j < Size && equal; j++)
                equal = (hash_[j] == hash_null_[j]);
            return equal;
        }

        private byte[] hash_;
        private Hash160(byte[] _hash)
        {
            hash_ = _hash;
        }

        public Hash160()
        {
            hash_ = new byte[Size];
            CopyFrom(hash_null_, 0);
        }

        public Hash160(Hash160 _other)
        {
            hash_ = new byte[Size];
            CopyFrom(_other.Data, 0);
        }

        private Hash160(byte[] _array, int _start)
        {
            hash_ = new byte[Size];
            CopyFrom(_array, _start);
        }

        public static readonly int Size = 20;

        public static Hash160 ConstructTake(byte[] _hash)
        {
            return new Hash160(_hash);
        }
        public static Hash160 ConstructCopy(byte[] _hash)
        {
            return new Hash160(_hash, 0);
        }
        public static Hash160 ConstructCopy(byte[] _hash, int start)
        {
            return new Hash160(_hash, start);
        }

        public byte[] Data { get { return hash_; } }

        public byte[] Release()
        {
            byte[] h = hash_;
            hash_ = new byte[Size];
            CopyFrom(hash_null_, 0);
            return h;
        }
        public override int GetHashCode()
        {
            Int32 hashcode = BitConverter.ToInt32(hash_, Size - 4);
            return hashcode;
        }

        public override string ToString()
        {
            int length = hash_.Length;
            char[] chars = new char[length * 2];
            for (int n = 0; n < length; n++)
            {
                int i = n * 2;
                byte value = hash_[n];
                byte bh = (byte)(value >> 4);
                chars[i] = (char)((bh < 10) ? ('0' + bh) : ('A' + bh - 10));
                byte bl = (byte)(value & 0xF);
                chars[i+1] = (char)((bl < 10) ? ('0' + bl) : ('A' + bl - 10));
            }
            string str = new string(chars);
            while (str.Length < 32)
                str = "0" + str;
            return str;
        }

        private static byte[] FromStringN(string _hashstr, int _size_in_bytes)
        {
            byte[] hash = new byte[_size_in_bytes];

            int nc = 2 * _size_in_bytes;
            string str = _hashstr;
            while (str.Length < nc)
                str = "0" + str;

            for (int i = 0; i < nc; i += 2)
            {
                int b = 0;
                for (int j = 0; j < 2; ++j)
                {
                    char c = str[i + j];
                    Debug.Assert(Char.IsLetterOrDigit(c) && !Char.IsLower(c));

                    int n = 0;
                    if (c >= 'A' && c <= 'F')
                        n = (byte)((int)10 + ((int)c - (int)'A'));
                    else if (c >= 'a' && c <= 'f')
                        n = (byte)((int)10 + ((int)c - (int)'a'));
                    else if (c >= '0' && c <= '9')
                        n = (byte)((int)0 + ((int)c - (int)'0'));

                    Debug.Assert(n >= 0 && n <= 15);
                    b = (byte)((b << 4) | n);
                }

                hash[i / 2] = (byte)b;
            }
            return hash;
        }

        public static Hash160 FromString(string _hashstr)
        {
            return ConstructTake(FromStringN(_hashstr, 32));
        }

        public static Hash160 FromDateTime(DateTime dt)
        {
            return FromString(String.Format("00000000000000{0:X16}", dt.Ticks));
        }

        public int Copy(Hash160 other)
        {
            return CopyFrom(other.Data, 0);
        }

        public int CopyFrom(byte[] _hash, int _offset)
        {
            for (int j = 0; j < Size; j++)
                hash_[j] = _hash[_offset + j];
            return Size;
        }

        public int CopyTo(byte[] _header)
        {
            return CopyTo(_header, 0);
        }

        public int CopyTo(byte[] _header, int _index)
        {
            for (int j = 0; j < Size; j++)
                _header[j + _index] = hash_[j];
            return Size;
        }

        public void WriteTo(BinaryWriter _writer)
        {
            _writer.Write(hash_);
        }

        public static bool operator ==(Hash160 b1, Hash160 b2)
        {
            bool equal = (b1.hash_[0] == b2.hash_[0]);
            for (int j = 1; j < Size && equal; j++)
                equal = (b1.hash_[j] == b2.hash_[j]);
            return equal;
        }
        public static bool operator !=(Hash160 b1, Hash160 b2)
        {
            bool equal = (b1.hash_[0] == b2.hash_[0]);
            for (int j = 1; j < Size && equal; j++)
                equal = (b1.hash_[j] == b2.hash_[j]);
            return equal == false;
        }

        public override bool Equals(object obj)
        {
            Hash160 h = (Hash160)obj;
            return Compare(h) == 0;
        }

        public int Compare(Hash160 _other)
        {
            return Compare(_other.Data, 0);
        }
        public static int Compare(Hash160 a, Hash160 b)
        {
            return a.Compare(b);
        }

        public int Compare(byte[] _other, int _start)
        {
            for (int j = 0; j < Size; j++)
            {
                byte m = hash_[j];
                byte o = _other[_start + j];
                if (m < o) return -1;
                else if (m > o) return 1;
            }
            return 0;
        }

        #region Comparer (IEqualityComparer)

        public class Comparer : IEqualityComparer<Hash160>
        {
            public bool Equals(Hash160 a, Hash160 b)
            {
                return Hash160.Compare(a, b) == 0;
            }

            public int GetHashCode(Hash160 r)
            {
                return r.GetHashCode();
            }
        }

        #endregion

        public static void UnitTest()
        {
            Hash160 h1 = new Hash160();
            Hash160 h2 = new Hash160();

            Hash160 h3 = new Hash160(Hash160.Error);
            Hash160 h4 = new Hash160(Hash160.Error);

            Debug.Assert(h1 == Hash160.Null);
            Debug.Assert(h2 == Hash160.Null);
            Debug.Assert(h1 != Hash160.Error);
            Debug.Assert(h2 != Hash160.Error);
            Debug.Assert(h1 == h2);
            Debug.Assert(h1.Compare(h2) == 0);
            Debug.Assert(h1.GetHashCode() == h2.GetHashCode());

            Debug.Assert(h3 != Hash160.Null);
            Debug.Assert(h4 != Hash160.Null);
            Debug.Assert(h3 == Hash160.Error);
            Debug.Assert(h4 == Hash160.Error);
            Debug.Assert(h3 == h4);
            Debug.Assert(h3.Compare(h4) == 0);
            Debug.Assert(h3.GetHashCode() == h4.GetHashCode());

            Debug.Assert(h1 != h3);
            Debug.Assert(h2 != h4);

            Debug.Assert(Hash160.Error != Hash160.Null);
            Debug.Assert(Hash160.Error.Compare(Hash160.Null) == 1);
            Debug.Assert(Hash160.Null.Compare(Hash160.Error) == -1);
            Debug.Assert(Hash160.Error.GetHashCode() != Hash160.Null.GetHashCode());
        }
    };

    static public class HashUtility
    {
        #region Methods

        public static Hash160 compute(FileInfo s)
        {
            if (!s.Exists)
                return Hash160.Null;

            try
            {
                using (FileStream fs = new FileStream(s.FullName, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    MemoryStream ms = new MemoryStream();
                    fs.CopyTo(ms);
                    return compute(ms);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("[HashUtility:EXCEPTION]{0}", e);
                return Hash160.Null;
            }
        }
        public static Hash160 compute(FileStream fs)
        {
            MemoryStream ms = new MemoryStream();
            fs.CopyTo(ms);
            return compute(ms);
        }
        public static Hash160 compute(MemoryStream ms)
        {
            byte[] data = ms.GetBuffer();
            return SHA1.Compute(data);
        }
        public static Hash160 compute(bool[] values)
        {
            MemoryStream ms = new MemoryStream();
            foreach (var v in values)
            {
                ms.WriteByte(v ? (byte)1 : (byte)0);
            }
            return compute(ms);
        }
        public static Hash160 compute(byte[] v)
        {
            return compute(v, 0, v.Length);
        }
        public static Hash160 compute(byte[] v1, int v1Length, byte[] v2, int v2Length)
        {
            MemoryStream ms = new MemoryStream();
            ms.Write(v1, 0, v1Length);
            ms.Write(v2, 0, v2Length);
            return compute(ms);
        }
        public static Hash160 compute(byte[] v, int index, int count)
        {
            MemoryStream ms = new MemoryStream();
            ms.Write(v, index, count);
            return compute(ms);
        }
        public static Hash160 compute(sbyte[] v)
        {
            return compute(v, 0, v.Length);
        }
        public static Hash160 compute(sbyte[] v, int index, int count)
        {
            MemoryStream ms = new MemoryStream();
            for (int i = index; i < count; ++i)
            {
                ms.WriteByte((byte)v[i]);
            }
            return compute(ms);
        }
        public static Hash160 compute(short[] values)
        {
            MemoryStream ms = new MemoryStream();
            byte[] bytes = new byte[2];
            foreach (var v in values)
            {
                for (int i = 0; i < 2; ++i)
                {
                    bytes[i] = (byte)(v >> ((7 - i) * 8));
                }
                ms.Write(bytes, 0, bytes.Length);
            }
            return compute(ms);
        }
        public static Hash160 compute(ushort[] values)
        {
            MemoryStream ms = new MemoryStream();
            byte[] bytes = new byte[2];
            foreach (var v in values)
            {
                for (int i = 0; i < 2; ++i)
                {
                    bytes[i] = (byte)(v >> ((7 - i) * 8));
                }
                ms.Write(bytes, 0, bytes.Length);
            }
            return compute(ms);
        }
        public static Hash160 compute(int[] values)
        {
            MemoryStream ms = new MemoryStream();
            byte[] bytes = new byte[8];
            foreach (var v in values)
            {
                for (int i = 0; i < 4; ++i)
                {
                    bytes[i] = (byte)(v >> ((7 - i) * 8));
                }
                ms.Write(bytes, 0, bytes.Length);
            }
            return compute(ms);
        }
        public static Hash160 compute(uint[] values)
        {
            MemoryStream ms = new MemoryStream();
            byte[] bytes = new byte[8];
            foreach (var v in values)
            {
                for (int i = 0; i < 4; ++i)
                {
                    bytes[i] = (byte)(v >> ((7 - i) * 8));
                }
                ms.Write(bytes, 0, bytes.Length);
            }
            return compute(ms);
        }
        public static Hash160 compute(Int64[] values)
        {
            MemoryStream ms = new MemoryStream();
            byte[] bytes = new byte[8];
            foreach (var v in values)
            {
                for (int i = 0; i < 8; ++i)
                {
                    bytes[i] = (byte)(v >> ((7 - i) * 8));
                }
                ms.Write(bytes, 0, bytes.Length);
            }
            return compute(ms);
        }
        public static Hash160 compute(UInt64[] values)
        {
            MemoryStream ms = new MemoryStream();
            byte[] bytes = new byte[8];
            foreach (var v in values)
            {
                for (int i=0; i<8; ++i)
                {
                    bytes[i] = (byte)(v >> ((7 - i) * 8));
                }
                ms.Write(bytes, 0, bytes.Length);
            }
            return compute(ms);
        }
        public static Hash160 compute(float[] values)
        {
            MemoryStream ms = new MemoryStream();
            foreach (var v in values)
            {
                byte[] bytes = BitConverter.GetBytes(v);
                ms.Write(bytes, 0, bytes.Length);
            }
            return compute(ms);
        }

        #endregion
    }
}