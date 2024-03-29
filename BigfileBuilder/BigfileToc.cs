using GameCore;
using GameData;

namespace DataBuildSystem
{
    public sealed class BigfileToc
    {
        private enum ECompressed
        {
            No,
            Yes
        }

        private interface IReadContext
        {
            #region Methods

            int Begin(int block, IBinaryReader reader);
            void Read(int block, int item, IBinaryReader reader);
            bool Next(int block);

            #endregion
        }

        private interface IWriteContext
        {
            #region Methods

            int Begin(int block, IBinaryWriter writer);
            void Write(int block, int item, IBinaryWriter writer);
            bool Next(int block);

            #endregion
        }

        public interface ITocEntry
        {
            #region Properties

            StreamOffset FileOffset { get; set; }
            Int32 FileSize { get; set; }
            bool IsCompressed { get; set; }
            string Filename { get; set; }
            Int64 FileId { get; set; }
            Hash160 FileContentHash { get; set; }
            List<ITocEntry> Children { get; set; }

            #endregion
        }

        /// <summary>
        /// Factory for creating TocEntry, ReadContext and WriteContext
        /// </summary>
        private static class Factory
        {
            #region Create

            public static ITocEntry Create()
            {
                return new TocEntry();
            }

            public static ITocEntry Create(Int64 fileId, StreamOffset fileOffset, Int32 fileSize, string filename, ECompressed type, Hash160 contentHash)
            {
                return new TocEntry(fileId, fileOffset, fileSize, filename, type, contentHash);
            }

            public static ITocEntry Create(Int32 fileSize, string filename, ECompressed type, Hash160 contentHash)
            {
                return new TocEntry(fileSize, filename, type, contentHash);
            }

            public static IReadContext CreateReadTocContext(List<TocSection> sections, List<ITocEntry> entries)
            {
                return new ReadToc32Context(sections, entries);
            }

            public static IReadContext CreateReadFdbContext(IReadOnlyList<TocSection> sections)
            {
                return new ReadFdbContext(sections);
            }

            public static IReadContext CreateReadHdbContext(IReadOnlyList<TocSection> sections)
            {
                return new ReadHdbContext(sections);
            }

            public static IWriteContext CreateWriteTocContext(IReadOnlyList<TocSection> sections, IReadOnlyList<ITocEntry> entries)
            {
                return new WriteToc32Context(sections, entries);
            }

            public static IWriteContext CreateWriteFdbContext(IReadOnlyList<TocSection> sections, IReadOnlyList<ITocEntry> entries)
            {
                return new WriteFdbContext(sections, entries);
            }

            public static IWriteContext CreateWriteHdbContext(IReadOnlyList<TocSection> sections, IReadOnlyList<ITocEntry> entries)
            {
                return new WriteHdbContext(sections, entries);
            }

            #endregion
        }


        public sealed class TocSection
        {
            #region Properties

            public Int32 TocOffset { get; set; }

            public Int32 TocCount
            {
                get { return Toc.Count; }
            }

            public Int32 TocExtraCount { get; set; }
            public Int64 DataOffset { get; set; }
            public List<ITocEntry> Toc { get; set; } = new();

            #endregion
        }

        /// <summary>
        /// TocEntry with a FileOffset(Int32)[] & FileSize
        ///
        /// Layout:
        ///     (Int32 FileOffset) OR (Int32 DataOffset to a FileOffset(Int32)[]) depending on bit 31 (0=Single, 1=Multi)
        ///     Int32 FileSize
        ///
        /// </summary>
        private sealed class TocEntry : ITocEntry
        {
            #region Constructor

            public TocEntry()
                : this(Int64.MaxValue, StreamOffset.Empty, -1, String.Empty, ECompressed.No, Hash160.Empty)
            {
            }

            public TocEntry(string filename, ECompressed type, Hash160 contentHash)
                : this(Int64.MaxValue, StreamOffset.Empty, -1, filename, type, contentHash)
            {
            }

            public TocEntry(Int32 fileSize, string filename, ECompressed type, Hash160 contentHash)
                : this(Int64.MaxValue, StreamOffset.Empty, fileSize, filename, type, contentHash)
            {
            }

            public TocEntry(Int64 fileId, StreamOffset fileOffset, Int32 fileSize)
                : this(fileId, fileOffset, fileSize, String.Empty, ECompressed.No, Hash160.Empty)
            {
            }

            public TocEntry(Int64 fileId, StreamOffset fileOffset, Int32 fileSize, string filename, ECompressed type, Hash160 contentHash)
            {
                FileOffset = fileOffset;
                Filename = filename;
                FileId = fileId;
                FileContentHash = contentHash;
                FileSize = fileSize;
                IsCompressed = (type == ECompressed.Yes);
            }

            #endregion

            #region Properties

            public static Int32 BinarySize => (sizeof(Int32) + sizeof(Int32));

            public TocSection Section { get; set; }
            public Int64 FileId { get; set; }
            public StreamOffset FileOffset { get; set; }
            public Int32 FileSize { get; set; }
            public string Filename { get; set; }
            public Hash160 FileContentHash { get; set; }
            public bool IsCompressed { get; set; }
            public List<ITocEntry> Children { get; set; } = new();

            public static Int32 EncodeFileSize(Int32 fileSize, bool isCompressed)
            {
                if (isCompressed)
                    return (Int32)(fileSize | 0x80000000);
                return fileSize & 0x7fffffff;
            }

            #endregion

            #region TocEntryFileIdComparer (IComparer<ITocEntry>)

            public class TocEntryFileIdComparer : IComparer<ITocEntry>
            {
                public int Compare(ITocEntry x, ITocEntry y)
                {
                    if (x.FileId == y.FileId)
                        return 0;
                    if (x.FileId < y.FileId)
                        return -1;
                    return 1;
                }
            }

            #endregion
        }

        // <summary>
        // The (32 bit) Toc (holding TocEntry[] with multiple file offsets) consists of 2 iterations
        //
        //     Iteration 1:
        //         Reading the TocEntry[] where every TocEntry holds an offset to a FileOffset(Int32)[] and a FileSize(Int32)
        //
        //     Iteration 2:
        //         Reading the FileOffset(Int32)[] for every TocEntry
        //
        // </summary>

        private sealed class ReadToc32Context : IReadContext
        {
            #region Fields

            private List<TocSection> Sections { get; set; }
            private List<ITocEntry> Entries { get; set; }

            #endregion

            #region Constructor

            public ReadToc32Context(List<TocSection> sections, List<ITocEntry> entries)
            {
                Sections = sections;
                Entries = entries;
            }

            #endregion

            #region IReadContext Members

            public int Begin(int block, IBinaryReader reader)
            {
                if (block == -1)
                {
                    // Read the header
                    int totalNumberOfEntries = reader.ReadInt32();
                    int numberOfSections = reader.ReadInt32();

                    Sections = new(numberOfSections);
                    Entries.Capacity = totalNumberOfEntries;

                    return numberOfSections;
                }

                if (block < Sections.Count)
                {
                    if ((block & 1) == 0)
                    {
                        return Sections[block].Toc.Count;
                    }
                    else
                    {
                        return Sections[block].TocExtraCount;
                    }
                }

                return 0;
            }

            private static bool HasChildren(ITocEntry e)
            {
                return e.Children.Count > 0;
            }

            public void Read(int block, int item, IBinaryReader reader)
            {
                switch (block)
                {
                    case -1:
                        TocSection ts = Sections[item];
                        var count = reader.ReadInt32();
                        ts.DataOffset = reader.ReadInt32() << 5;
                        ts.TocOffset = reader.ReadInt32();
                        ts.Toc = new(count);
                        for (int j = 0; j < count; ++j)
                        {
                            var e = new TocEntry();
                            ts.Toc.Add(e);
                            Entries.Add(e);
                        }

                        break;

                    case >= 0:
                        if ((block & 1) == 0)
                        {
                            // Read Toc Entries
                            Int32 fileOffset = reader.ReadInt32();
                            Int32 fileSize = reader.ReadInt32();
                            int sectionIndex = (block / 2);
                            TocEntry e = new((Int64)item, new StreamOffset(fileOffset), fileSize);
                            Sections[sectionIndex].Toc.Add(e);
                            if (HasChildren(e))
                            {
                                Sections[sectionIndex].TocExtraCount++;
                            }
                        }
                        else
                        {
                            // Read extra info for some TocEntry
                            int sectionIndex = (block / 2);
                            ITocEntry e = Sections[sectionIndex].Toc[item];
                            if (HasChildren(e))
                            {
                                int numChildren = reader.ReadInt32();
                                for (int i = 0; i < numChildren; ++i)
                                {
                                    int childFileId = reader.ReadInt32();
                                    ITocEntry childEntry = Sections[sectionIndex].Toc[childFileId];
                                    e.Children.Add(childEntry);
                                }
                            }
                        }

                        break;
                }
            }

            public bool Next(int block)
            {
                return block < (Sections.Count * 2);
            }

            #endregion
        }

        // <summary>
        // The Fdb (holding the Offset[] and Filenames[]) consists of 3 iterations
        //
        //     Iteration 1:
        //         Reading the Offset(Int32) to the filename for every TocEntry
        //
        //     Iteration 2:
        //         Reading the filenames
        //
        //     Iteration 3:
        //         Getting the filename for every TocEntry
        //
        // </summary>
        private sealed class ReadFdbContext : IReadContext
        {
            #region Fields

            private IReadOnlyList<TocSection> Sections { get; set; }

            #endregion

            #region Constructor

            public ReadFdbContext(IReadOnlyList<TocSection> sections)
            {
                Sections = sections;
            }

            #endregion

            #region IReadContext Members

            public int Begin(int block, IBinaryReader reader)
            {
                if (block == -1)
                {
                    // Read the header
                    int numTotalEntries = reader.ReadInt32();
                    int numSections = reader.ReadInt32();

                    return numSections;
                }

                if ((block & 1) == 0)
                {
                    return Sections[block].Toc.Count;
                }

                return Sections[block].TocExtraCount;
            }

            public void Read(int block, int item, IBinaryReader reader)
            {
                switch (block)
                {
                    case -1:
                        reader.ReadInt32(); // read total number of entries
                        reader.ReadInt32(); // read number of sections

                        break;

                    case >= 0:
                        // Read Toc Entry filenames
                        if ((block & 1) == 0)
                        {
                            reader.ReadInt32();
                        }
                        else
                        {
                            reader.Position = CMath.Align(reader.Position, 4);
                            string filename = reader.ReadString();

                            int sectionIndex = (block / 2);
                            ITocEntry te = Sections[sectionIndex].Toc[item];
                            te.Filename = filename;
                        }

                        break;
                }
            }

            public bool Next(int block)
            {
                return block < (Sections.Count * 2);
            }

            #endregion
        }

        // <summary>
        // The Hdb (holding the Hash[]) consists of 1 iteration
        //
        //     Iteration 1:
        //         Reading the Hash[] to the filename for every TocEntry
        //
        //     Iteration 2:
        //         Reading the filenames
        //
        //     Iteration 3:
        //         Getting the filename for every TocEntry
        //
        // </summary>
        private sealed class ReadHdbContext : IReadContext
        {
            #region Fields

            private IReadOnlyList<TocSection> Sections { get; set; }

            #endregion

            #region Constructor

            public ReadHdbContext(IReadOnlyList<TocSection> sections)
            {
                Sections = sections;
            }

            #endregion

            #region IReadContext Members

            public int Begin(int block, IBinaryReader reader)
            {
                if (block == -1)
                {
                    // Read the header
                    int numTotalEntries = reader.ReadInt32();
                    int numSections = reader.ReadInt32();

                    return numSections;
                }

                if (block < Sections.Count)
                {
                    return Sections[block].Toc.Count;
                }

                return 0;
            }

            public void Read(int block, int item, IBinaryReader reader)
            {
                switch (block)
                {
                    case -1:
                        TocSection ts = Sections[item];
                        var count = reader.ReadInt32();
                        reader.ReadInt32();
                        reader.ReadInt64();

                        break;

                    case >= 0:
                        // Read Toc Entry content hash
                        byte[] hash = reader.ReadBytes(Hash160.Size);

                        int sectionIndex = block;
                        ITocEntry te = Sections[sectionIndex].Toc[item];
                        te.FileContentHash = Hash160.ConstructTake(hash);
                        break;
                }
            }

            public bool Next(int block)
            {
                return block < Sections.Count;
            }

            #endregion
        }

        // <summary>
        // Write the multi-section TOC
        // </summary>
        private sealed class WriteToc32Context : IWriteContext
        {
            #region Fields

            private int Iteration { get; set; }
            private int Index { get; set; }
            private int Offset { get; set; }

            private IReadOnlyList<TocSection> Sections { get; set; }
            private IReadOnlyList<ITocEntry> Entries { get; set; }

            #endregion

            #region IWriteContext Members

            public WriteToc32Context(IReadOnlyList<TocSection> sections, IReadOnlyList<ITocEntry> entries)
            {
                Sections = sections;
                Entries = entries;

                // Compute the offset of each section
                Int32 offset = sizeof(Int32) + sizeof(Int32) + Sections.Count * sizeof(Int32);
                foreach (var section in Sections)
                {
                    section.TocOffset = offset;

                    // The size of TocEntry[]
                    offset += section.TocCount * TocEntry.BinarySize;
                    // However we also have an 'extra' block where we are writing Children of TocEntry's that have them
                    foreach (var te in section.Toc)
                    {
                        if (HasChildren(te))
                        {
                            int binarySize = sizeof(Int32) + te.Children.Count * sizeof(Int32);
                            offset += binarySize;
                        }
                    }
                }
            }

            private static bool HasChildren(ITocEntry e)
            {
                return e.Children.Count > 0;
            }

            private static Int32 MarkHasChildrenInFileSize(Int32 value)
            {
                return (Int32)((UInt32)value | (UInt32)0x80000000);
            }

            public int Begin(int block, IBinaryWriter writer)
            {
                if (block == -1)
                {
                    // Header
                    writer.Write(Entries.Count);
                    writer.Write(Sections.Count);
                    return Sections.Count;
                }

                TocSection section = Sections[block / 2];
                return section.TocCount;
            }

            public void Write(int block, int item, IBinaryWriter writer)
            {
                switch (block)
                {
                    case -1:
                        {
                            // Write the offset to each section, use 64-bit so that
                            // the C++ side can replace it with a pointer after loading.
                            TocSection section = Sections[item];
                            writer.Write((UInt64)section.TocOffset);
                            break;
                        }
                    case >= 0:
                        {
                            if (block.IsEven())
                            {
                                TocSection section = Sections[block / 2];
                                ITocEntry e = section.Toc[item];

                                Int32 offset = (Int32)(e.FileOffset.Offset >> 5);
                                Int32 size = e.FileSize;
                                if (HasChildren(e))
                                {
                                    // Mark file size so that it is known that offset is actually an offset
                                    // within TOC to an array:
                                    // Int32   Number of Children
                                    // Int32[] FileId
                                    size = MarkHasChildrenInFileSize(size);
                                }

                                writer.Write(offset); // 32-bit
                                writer.Write(size); // 32-bit
                            }
                            else
                            {
                                TocSection section = Sections[block / 2];
                                ITocEntry e = section.Toc[item];
                                if (HasChildren(e))
                                {
                                    writer.Write(e.Children.Count);
                                    foreach (var ce in e.Children)
                                    {
                                        writer.Write(ce.FileId.Lower32());
                                    }
                                }
                            }

                            break;
                        }
                }
            }

            public bool Next(int block)
            {
                return block < (2 * Sections.Count);
            }

            #endregion
        }

        // <summary>
        // Each section of the Fdb holds {FilenameOffset(Int32)[],  Filename(string)[]}
        // </summary>
        private sealed class WriteFdbContext : IWriteContext
        {
            #region Fields

            private int Index { get; set; }

            private IReadOnlyList<TocSection> Sections { get; set; }
            private IReadOnlyList<ITocEntry> Entries { get; set; }
            private List<Int32> SectionOffsets { get; set; }
            private List<Int32> FilenameOffsets { get; set; }

            #endregion

            #region IWriteContext Members

            public WriteFdbContext(IReadOnlyList<TocSection> sections, IReadOnlyList<ITocEntry> entries)
            {
                Sections = sections;
                Entries = entries;

                SectionOffsets = new(Sections.Count);
                FilenameOffsets = new(entries.Count);

                // Compute the offset of each section
                Int32 offset = sizeof(Int32) + sizeof(Int32) + Sections.Count * sizeof(Int32);
                foreach (var section in Sections)
                {
                    SectionOffsets.Add(offset);
                    offset += section.TocCount * sizeof(Int32); // The size of Offset[]
                    foreach (var e in section.Toc)
                    {
                        FilenameOffsets.Add(offset);
                        offset += sizeof(Int32) + e.Filename.Length + 1;
                        offset = CMath.Align32(offset, 4);
                    }
                }
            }

            public int Begin(int block, IBinaryWriter writer)
            {
                Index = 0;

                if (block == -1)
                {
                    // Header
                    writer.Write(Entries.Count);
                    writer.Write(Sections.Count);
                    return Sections.Count;
                }

                TocSection section = Sections[block / 2];
                return section.TocCount;
            }

            public void Write(int block, int item, IBinaryWriter writer)
            {
                if (block == -1)
                {
                    writer.Write(SectionOffsets[item]); // Write the offset to each section
                }
                else
                {
                    if (block.IsEven())
                    {
                        writer.Write(FilenameOffsets[Index++]);
                    }
                    else
                    {
                        TocSection section = Sections[block / 2];
                        writer.Write(section.Toc[item].Filename);
                    }
                }
            }

            public bool Next(int block)
            {
                return block < (2 * Sections.Count);
            }

            #endregion
        }

        // <summary>
        // The Hdb (holding Hash160[])
        // </summary>
        private sealed class WriteHdbContext : IWriteContext
        {
            #region Fields

            private IReadOnlyList<TocSection> Sections { get; set; }
            private IReadOnlyList<ITocEntry> Entries { get; set; }
            private List<Int32> SectionOffsets { get; set; }

            #endregion

            #region IWriteContext Members

            public WriteHdbContext(IReadOnlyList<TocSection> sections, IReadOnlyList<ITocEntry> entries)
            {
                Sections = sections;
                Entries = entries;

                SectionOffsets = new(Sections.Count);

                // Compute the offset of each section
                Int32 offset = sizeof(Int32) + sizeof(Int32) + Sections.Count * sizeof(Int32);
                foreach (var section in Sections)
                {
                    SectionOffsets.Add(offset);
                    offset += section.TocCount * Hash160.Size; // The size of Hash[]
                }
            }

            public int Begin(int block, IBinaryWriter writer)
            {
                if (block == -1)
                {
                    // Header
                    writer.Write(Entries.Count);
                    writer.Write(Sections.Count);
                    return Sections.Count;
                }

                TocSection section = Sections[block];
                return section.TocCount;
            }

            public void Write(int block, int item, IBinaryWriter writer)
            {
                if (block == -1)
                {
                    writer.Write(SectionOffsets[item]); // Write the offset to each section
                }
                else
                {
                    TocSection section = Sections[block];
                    section.Toc[item].FileContentHash.WriteTo(writer);
                }
            }

            public bool Next(int block)
            {
                return block < Sections.Count;
            }

            #endregion
        }

        #region Constructor(s)

        public BigfileToc()
        {
        }

        #endregion

        #region Methods

        public static bool Exists(string filename)
        {
            string bigFileTocFilename = filename;
            bigFileTocFilename = Path.ChangeExtension(bigFileTocFilename, BigfileConfig.BigFileTocExtension);
            FileInfo fileInfo = new(bigFileTocFilename);
            return fileInfo.Exists;
        }

        public static void CopyFilesOrder(BigfileToc currentToc, BigfileToc sourceToc)
        {
            // TODO Missing implementation
        }

        private static void ReadTable(IReadContext context, FileStream stream, EPlatform platform)
        {
            IBinaryReader binaryReader = EndianUtils.CreateBinaryReader(stream, platform);
            {
                int block = -1;
                do
                {
                    int count = context.Begin(block, binaryReader);
                    for (int i = 0; i < count; ++i)
                        context.Read(block, i, binaryReader);
                    block += 1;
                } while (context.Next(block));
            }
            binaryReader.Close();
        }

        private static FileStream OpenFileStreamForReading(string filename)
        {
            FileInfo fileInfo = new(filename);
            if (!fileInfo.Exists)
            {
                Console.WriteLine("We tried to open '" + fileInfo + "' but it does not exist.");
                return null;
            }

            FileStream fileStream = new(fileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.None);
            return fileStream;
        }

        private static void WriteTable(IWriteContext context, FileStream stream, EPlatform platform)
        {
            IBinaryStream binaryWriter = EndianUtils.CreateBinaryStream(stream, platform);
            {
                int block = -1;
                do
                {
                    int count = context.Begin(block, binaryWriter);
                    for (int i = 0; i < count; ++i)
                        context.Write(block, i, binaryWriter);
                    block += 1;
                } while (context.Next(block));
            }
            binaryWriter.Close();
        }

        private static FileStream OpenFileStreamForWriting(string filename)
        {
            FileInfo fileInfo = new(filename);
            if (!fileInfo.Exists)
            {
                FileStream fileCreationStream = File.Create(fileInfo.FullName);
                fileCreationStream.Close();
            }

            FileStream fileStream = new(fileInfo.FullName, FileMode.Truncate, FileAccess.Write, FileShare.Write, 1 * 1024 * 1024, FileOptions.Asynchronous);
            return fileStream;
        }

        public bool Load(string filename, EPlatform platform, List<Bigfile> bigfiles)
        {
            List<TocSection> sections = new();
            List<ITocEntry> entries = new();

            try
            {
                FileStream bigFileTocFileStream = OpenFileStreamForReading(Path.ChangeExtension(filename, BigfileConfig.BigFileTocExtension));
                FileStream bigFileFdbFileStream = OpenFileStreamForReading(Path.ChangeExtension(filename, BigfileConfig.BigFileFdbExtension));
                FileStream bigFileHdbFileStream = OpenFileStreamForReading(Path.ChangeExtension(filename, BigfileConfig.BigFileHdbExtension));
                {
                    try
                    {
                        ReadTable(Factory.CreateReadTocContext(sections, entries), bigFileTocFileStream, platform);
                        ReadTable(Factory.CreateReadFdbContext(sections), bigFileFdbFileStream, platform);
                        ReadTable(Factory.CreateReadHdbContext(sections), bigFileHdbFileStream, platform);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                        return false;
                    }
                }
                bigFileTocFileStream.Close();
                bigFileFdbFileStream.Close();
                bigFileHdbFileStream.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }

            return true;
        }

        public bool Save(string bigfileFilename, EPlatform platform, List<Bigfile> bigfiles)
        {
            // Create all TocEntry items in the same order as the Bigfile files which is important
            // because the FileId is equal to the location(index) in the List/Array.
            Int32 totalEntries = 0;
            foreach (var bf in bigfiles)
            {
                totalEntries += bf.Files.Count;
            }

            List<TocSection> sections = new();
            List<ITocEntry> entries = new(totalEntries);
            foreach (var bf in bigfiles)
            {
                TocSection section = new();
                foreach (var file in bf.Files)
                {
                    ITocEntry fileEntry = Factory.Create(file.FileId, file.FileOffset, file.FileSize, file.Filename, ECompressed.No, file.FileContentHash);
                    entries.Add(fileEntry);
                    section.Toc.Add(fileEntry);
                }
                sections.Add(section);
            }

            // Manage children of each TocEntry
            for (int i=0; i<bigfiles.Count; ++i)
            {
                Bigfile bf = bigfiles[i];
                TocSection section = sections[i];

                for (int j=0; j<bf.Files.Count; ++j)
                {
                    BigfileFile bff = bf.Files[i];
                    ITocEntry entry = entries[(int)bff.FileId];
                    foreach (var child in bff.Children)
                    {
                        ITocEntry childEntry = entries[(int)child.FileId];
                        entry.Children.Add(childEntry);
                        section.TocExtraCount++;
                    }
                }
            }

            try
            {
                FileStream bigFileTocFileStream = OpenFileStreamForWriting(Path.ChangeExtension(bigfileFilename, BigfileConfig.BigFileTocExtension));
                FileStream bigFileFdbFileStream = OpenFileStreamForWriting(Path.ChangeExtension(bigfileFilename, BigfileConfig.BigFileFdbExtension));
                FileStream bigFileHdbFileStream = OpenFileStreamForWriting(Path.ChangeExtension(bigfileFilename, BigfileConfig.BigFileHdbExtension));

                {
                    try
                    {
                        WriteTable(Factory.CreateWriteTocContext(sections, entries), bigFileTocFileStream, platform);
                        WriteTable(Factory.CreateWriteFdbContext(sections, entries), bigFileFdbFileStream, platform);
                        WriteTable(Factory.CreateWriteHdbContext(sections, entries), bigFileHdbFileStream, platform);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                        return false;
                    }
                }

                bigFileTocFileStream.Close();
                bigFileFdbFileStream.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }

            return true;
        }

        #endregion
    }
}
