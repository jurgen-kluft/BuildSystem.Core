
namespace GameCore
{
    #region IBinaryReader and IStreamReader

    public interface IBinaryReader
    {
        int Read(byte[] buffer, int offset, int count);
        sbyte ReadInt8();
        byte ReadUInt8();
        short ReadInt16();
        ushort ReadUInt16();
        int ReadInt32();
        uint ReadUInt32();
        long ReadInt64();
        ulong ReadUInt64();
        float ReadFloat();
        double ReadDouble();
        string ReadString();
    }


    #endregion

    #region BinaryReader (Endian)


    public interface IBinaryStreamReader : IBinaryStream, IBinaryReader
    {
    }


    public sealed class BinaryEndianReader : IBinaryReader, IBinaryStreamReader
    {
        private readonly IArchitecture _architecture;
        private readonly IBinaryStreamReader _reader;
        private readonly byte[] _buffer = new byte[8];

        public BinaryEndianReader(IArchitecture architecture, IBinaryStreamReader reader)
        {
            _architecture = architecture;
            _reader = reader;
        }

        public IArchitecture Architecture => _architecture;

        public long Position
        {
            get => _reader.Position;
            set => _reader.Position = value;
        }

        public long Length
        {
            get => _reader.Length;
            set => _reader.Position = value;
        }

        public long Seek(long offset)
        {
            return _reader.Seek(offset);
        }

        public bool SkipBytes(long numBytes)
        {
            var curPos = _reader.Position;
            var newPos = _reader.Seek(curPos + numBytes);
            return (newPos - curPos) == numBytes;
        }

        public int Read(byte[] data, int offset, int count)
        {
            return _reader.Read(data, offset, count);
        }

        public sbyte ReadInt8()
        {
            _reader.Read(_buffer, 0, 1);
            return (sbyte)_buffer[0];
        }

        public byte ReadUInt8()
        {
            _reader.Read(_buffer, 0, 1);
            return _buffer[0];
        }

        public short ReadInt16()
        {
            _reader.Read(_buffer, 0, 2);
            return _architecture.ReadInt16(_buffer, 0);
        }

        public ushort ReadUInt16()
        {
            _reader.Read(_buffer, 0, 2);
            return _architecture.ReadUInt16(_buffer, 0);
        }

        public int ReadInt32()
        {
            _reader.Read(_buffer, 0, 4);
            return _architecture.ReadInt32(_buffer, 0);
        }

        public uint ReadUInt32()
        {
            _reader.Read(_buffer, 0, 4);
            return _architecture.ReadUInt32(_buffer, 0);
        }

        public long ReadInt64()
        {
            _reader.Read(_buffer, 0, 8);
            return _architecture.ReadInt64(_buffer, 0);
        }

        public ulong ReadUInt64()
        {
            _reader.Read(_buffer, 0, 8);
            return _architecture.ReadUInt64(_buffer, 0);
        }

        public float ReadFloat()
        {
            _reader.Read(_buffer, 0, 4);
            return _architecture.ReadFloat(_buffer, 0);
        }

        public double ReadDouble()
        {
            _reader.Read(_buffer, 0, 8);
            return _architecture.ReadDouble(_buffer, 0);
        }

        public string ReadString()
        {
            var len = ReadInt32();
            var data = new byte[len + 1];
            Read(data, 0, len + 1);
            var s = System.Text.Encoding.UTF8.GetString(data, 0, len);
            return s;
        }

        public void Close()
        {
            _reader.Close();
        }
    }

    public sealed class BinaryFileReader : IBinaryStreamReader
    {
        private BinaryEndianReader _binaryReader;
        private BinaryStreamReader _streamReader;
        private FileStream _fileStream;

        public bool Open(string filepath)
        {
            if (!File.Exists(filepath)) return false;

            _fileStream = new FileStream(filepath, FileMode.Open, FileAccess.Read);
            _streamReader = new BinaryStreamReader(_fileStream);
            _binaryReader = new BinaryEndianReader(ArchitectureUtils.LittleArchitecture64, _streamReader);
            return true;

        }

        public void Close()
        {
            if (_fileStream == null) return;

            _binaryReader.Close();
            _fileStream.Close();
        }

        public IArchitecture Architecture => _binaryReader.Architecture;

        public long Position
        {
            get => _binaryReader.Position;
            set => _binaryReader.Position = value;
        }

        public long Length
        {
            get => _binaryReader.Length;
            set => _binaryReader.Position = value;
        }

        public long Seek(long offset)
        {
            return _binaryReader.Seek(offset);
        }

        public bool SkipBytes(long numBytes)
        {
            return _binaryReader.SkipBytes(numBytes);
        }

        public int Read(byte[] data, int offset, int size)
        {
            return _binaryReader.Read(data, offset, size);
        }

        public sbyte ReadInt8()
        {
            return _binaryReader.ReadInt8();
        }

        public byte ReadUInt8()
        {
            return _binaryReader.ReadUInt8();
        }

        public short ReadInt16()
        {
            return _binaryReader.ReadInt16();
        }

        public ushort ReadUInt16()
        {
            return _binaryReader.ReadUInt16();
        }

        public int ReadInt32()
        {
            return _binaryReader.ReadInt32();
        }

        public uint ReadUInt32()
        {
            return _binaryReader.ReadUInt32();
        }

        public long ReadInt64()
        {
            return _binaryReader.ReadInt64();
        }

        public ulong ReadUInt64()
        {
            return _binaryReader.ReadUInt64();
        }

        public float ReadFloat()
        {
            return _binaryReader.ReadFloat();
        }

        public double ReadDouble()
        {
            return _binaryReader.ReadDouble();
        }

        public string ReadString()
        {
            return _binaryReader.ReadString();
        }
    }

    #endregion


}
