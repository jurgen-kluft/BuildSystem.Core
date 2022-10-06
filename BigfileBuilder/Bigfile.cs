﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using GameCore;

namespace DataBuildSystem
{
    public sealed class BigfileWriter
    {
        private byte[] mReadCache;
        private FileStream mFileStream;

        public Int64 Position { get; private set; }

        public void SetLength(Int64 length)
        {
            mFileStream.SetLength(length);
        }

        public bool Open(string filepath)
        {
            try
            {
                Close();

                string bigfileFilepath = Path.ChangeExtension(filepath, BigfileConfig.BigFileExtension);
                FileInfo bigfileInfo = new(bigfileFilepath);

                mReadCache = new byte[BigfileConfig.ReadBufferSize];

                DirUtils.Create(bigfileInfo.DirectoryName);

                mFileStream = new(bigfileInfo.FullName, FileMode.Create, FileAccess.Write, FileShare.None, (Int32)BigfileConfig.WriteBufferSize, FileOptions.Asynchronous);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Save all BigfileFiles into the Bigfile, this uses a different approach. It allocates
        /// the full size of the Bigfile first and uses seek to write all the BigfileFiles.
        /// </summary>
        /// <param name="path">The absolute path of where 'files' can be found</param>
        /// <param name="files">All the files to include in the Bigfile</param>
        /// <returns>True if successful</returns>
        public Int64 Save(string filepath)
        {
            try
            {
                FileStream inputStream = new(filepath, FileMode.Open, FileAccess.Read, FileShare.Read);
                Int64 fileOffset = Write(inputStream, inputStream.Length);
                inputStream.Close();
                return fileOffset;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return -1;
            }
        }

        private Int64 Write(Stream readStream, Int64 fileSize)
        {
            // Align the file on the calculated additionalLength
            mFileStream.Position = Alignment.Align(mFileStream.Position, BigfileConfig.FileAlignment);
            Int64 position = mFileStream.Position;

            Debug.Assert(fileSize < Int32.MaxValue);

            if (fileSize <= BigfileConfig.ReadBufferSize)
            {
                readStream.Read(mReadCache, 0, (Int32)fileSize);
                mFileStream.Write(mReadCache, 0, (Int32)fileSize);
            }
            else
            {
                int br;
                while ((br = readStream.Read(mReadCache, 0, mReadCache.Length)) > 0)
                    mFileStream.Write(mReadCache, 0, br);
            }
            return position;
        }


        public void Close()
        {
            if (mFileStream != null)
            {
                mFileStream.Close();
                mFileStream = null;
            }
        }
    }

    public sealed class BigfileReader
    {
        private FileStream mFileStream;
        private BinaryReader mBinaryReader;

        public bool Open(string filepath)
        {
            try
            {
                Close();

                string bigfileFilepath = Path.ChangeExtension(filepath, BigfileConfig.BigFileExtension);
                FileInfo bigfileInfo = new(bigfileFilepath);

                mFileStream = new(bigfileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, (Int32)BigfileConfig.ReadBufferSize, FileOptions.Asynchronous);
                mBinaryReader = new(mFileStream);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }

            return true;
        }

        public void Close()
        {
            if (mFileStream != null)
            {
                if (mBinaryReader != null)
                {
                    mBinaryReader.Close();
                    mBinaryReader = null;
                }

                mFileStream.Close();
                mFileStream = null;
            }
        }
    }

    public sealed class Bigfile
    {
        #region Fields

        public List<BigfileFile> Files {get;set;} = new();

        #endregion

        #region Methods

        public void WriteTo(BigfileWriter writer)
        {
            Int64 additionalLength = 0;
            foreach(var bff in Files)
            {
                additionalLength += bff.FileSize;
                additionalLength = Alignment.Align(additionalLength, BigfileConfig.FileAlignment);
            }
            writer.SetLength(writer.Position + additionalLength);

            foreach(var bff in Files)
            {
                Int64 fileOffset = writer.Save(bff.Filename);
                bff.FileOffset = new StreamOffset(fileOffset);
            }
        }

        #endregion
    }
}
