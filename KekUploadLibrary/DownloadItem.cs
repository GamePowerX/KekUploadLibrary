using System;
using System.IO;
using System.Threading.Tasks;

namespace KekUploadLibrary
{

    /// <summary>
    /// This class represents a download item from the KekUploadServer.
    /// It is used in the <see cref="DownloadClient"/>.
    /// </summary>
    public class DownloadItem
    {
        /// <summary>
        /// Creates a new <see cref="DownloadItem"/> with the given file name.
        /// This constructor is used when a file is downloaded to a file.
        /// </summary>
        /// <param name="fileName">The file name.</param>
        public DownloadItem(string fileName)
        {
            DownloadType = DownloadType.File;
            var file = Path.GetFullPath(fileName);
            FilePath = file;
            _fileStream = new FileStream(file, FileMode.OpenOrCreate, FileAccess.Write);
        }

        /// <summary>
        /// Creates a new <see cref="DownloadItem"/> with the given <see cref="byte"/> array.
        /// This constructor is used when a file is downloaded to a <see cref="byte"/> array.
        /// </summary>
        /// <param name="data">The byte array.</param>
        public DownloadItem(byte[] data)
        {
            DownloadType = DownloadType.ByteArray;
            Data = data;
        }

        /// <summary>
        /// Creates a new <see cref="DownloadItem"/> with the given <see cref="Stream"/>.
        /// This constructor is used when a file is downloaded to a <see cref="Stream"/>.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <exception cref="KekException">Is thrown when the stream is not writable.</exception>
        public DownloadItem(Stream stream)
        {
            DownloadType = DownloadType.Stream;
            // check if the stream is writable
            if (!stream.CanWrite)
                throw new KekException("The provided stream is not writable!",
                    new ArgumentException("The provided stream is not writable!", nameof(stream)));
            Stream = stream;
        }

        /// <summary>
        /// The type of location where the file is downloaded to.
        /// </summary>
        public DownloadType DownloadType { get; }

        /// <summary>
        /// The file path where the file is downloaded to.
        /// Is <see langword="null"/> when the <see cref="DownloadType"/> is not <see cref="DownloadType.File"/>.
        /// </summary>
        public string? FilePath { get; }

        /// <summary>
        /// The stream where the file is downloaded to.
        /// Is <see langword="null"/> when the <see cref="DownloadType"/> is not <see cref="DownloadType.Stream"/>.
        /// </summary>
        public Stream? Stream { get; }

        /// <summary>
        /// The byte array where the file is downloaded to.
        /// Is <see langword="null"/> when the <see cref="DownloadType"/> is not <see cref="DownloadType.ByteArray"/>.
        /// </summary>
        public byte[]? Data { get; private set; }

        private FileStream? _fileStream;

        /// <summary>
        /// Writes the given data to the file, stream or byte array.
        /// </summary>
        /// <param name="data">The data to write.</param>
        /// <exception cref="ArgumentOutOfRangeException">Is thrown when the <see cref="DownloadType"/> is invalid.</exception>
        public void WriteData(byte[] data)
        {
            switch (DownloadType)
            {
                case DownloadType.File:
                    _fileStream!.Write(data);
                    break;
                case DownloadType.Stream:
                    Stream!.Write(data);
                    break;
                case DownloadType.ByteArray:
                    // append the data to the byte array
                    var temp = new byte[Data!.Length + data.Length];
                    Array.Copy(Data, temp, Data.Length);
                    Array.Copy(data, 0, temp, Data.Length, data.Length);
                    Data = temp;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        /// <summary>
        /// Writes the given data to the file, stream or byte array asynchronously.
        /// This is used by the <see cref="DownloadClient"/> to write the data to the correct location.
        /// </summary>
        /// <param name="buffer">The data to write.</param>
        /// <param name="i">The offset.</param>
        /// <param name="bytesRead">The number of bytes to write.</param>
        /// <exception cref="ArgumentOutOfRangeException">Is thrown when the <see cref="DownloadType"/> is not valid.</exception>
        public async Task WriteDataAsync(byte[] buffer, int i, int bytesRead)
        {
            switch (DownloadType)
            {
                case DownloadType.File:
                    await _fileStream!.WriteAsync(buffer, i, bytesRead);
                    break;
                case DownloadType.Stream:
                    await Stream!.WriteAsync(buffer, i, bytesRead);
                    break;
                case DownloadType.ByteArray:
                    // append the data to the byte array
                    var temp = new byte[Data!.Length + bytesRead];
                    Array.Copy(Data, temp, Data.Length);
                    Array.Copy(buffer, i, temp, Data.Length, bytesRead);
                    Data = temp;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// This method is used to close the file, stream or byte array when the download is finished.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Is thrown when the <see cref="DownloadType"/> is not valid.</exception>
        public void Close()
        {
            switch (DownloadType)
            {
                case DownloadType.File:
                    _fileStream!.Close();
                    break;
                case DownloadType.Stream:
                    Stream!.Close();
                    break;
                case DownloadType.ByteArray:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    /// <summary>
    /// The type of location where the file is downloaded to.
    /// </summary>
    public enum DownloadType
    {
        /// <summary>
        /// The file is downloaded to a file.
        /// </summary>
        File,
        /// <summary>
        /// The file is downloaded to a <see cref="Stream"/>.
        /// </summary>
        Stream,
        /// <summary>
        /// The file is downloaded to a <see cref="byte"/> array.
        /// </summary>
        ByteArray
    }

}