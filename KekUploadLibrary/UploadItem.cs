using System;
using System.IO;

namespace KekUploadLibrary
{
    /// <summary>
    /// This class represents an item that can be uploaded to the KekUploadServer.
    /// It is used in the <see cref="UploadClient"/>.
    /// </summary>
    public class UploadItem
    {
        /// <summary>
        /// Creates a new <see cref="UploadItem"/> with the given file name.
        /// This constructor is used when a file is uploaded from a file.
        /// </summary>
        /// <param name="fileName">The file name.</param>
        /// <exception cref="KekException">Is thrown when the file does not exist.</exception>
        public UploadItem(string fileName)
        {
            UploadType = UploadType.File;
            var file = Path.GetFullPath(fileName);
            if (!File.Exists(file))
                throw new KekException("The provided file does not exist!",
                    new FileNotFoundException("The provided file does not exist!", file));

            var fileInfo = new FileInfo(file);
            FilePath = file;
            try
            {
                Extension = fileInfo.Extension[1..];
                Name = fileInfo.Name[..^fileInfo.Extension.Length];
            }
            catch (ArgumentOutOfRangeException)
            {
                Extension = "none";
                Name = fileInfo.Name;
            }
        }

        /// <summary>
        /// Creates a new <see cref="UploadItem"/> with the given <see cref="byte"/> array.
        /// This constructor is used when a file is uploaded from a <see cref="byte"/> array.
        /// </summary>
        /// <param name="data">The byte array.</param>
        /// <param name="extension">The extension of the file.</param>
        /// <param name="name">The name of the file.</param>
        public UploadItem(byte[] data, string extension, string? name = null)
        {
            UploadType = UploadType.ByteArray;
            Extension = extension;
            Data = data;
            Name = name;
        }

        /// <summary>
        /// Creates a new <see cref="UploadItem"/> with the given <see cref="Stream"/>.
        /// This constructor is used when a file is uploaded from a <see cref="Stream"/>.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="extension">The extension of the file.</param>
        /// <param name="name">The name of the file.</param>
        public UploadItem(Stream stream, string extension, string? name = null)
        {
            UploadType = UploadType.Stream;
            Extension = extension;
            Stream = stream;
            Name = name;
        }

        /// <summary>
        /// The type of location where the file is uploaded from.
        /// </summary>
        public UploadType UploadType { get; }
        
        /// <summary>
        /// The path of the to be uploaded file.
        /// Is null if the <see cref="UploadType"/> is not <see cref="UploadType.File"/>.
        /// </summary>
        public string? FilePath { get; }
        
        /// <summary>
        /// The extension of the file.
        /// </summary>
        public string Extension { get; }
        
        /// <summary>
        /// The name of the file.
        /// Can be <see langword="null"/>.
        /// </summary>
        public string? Name { get; }
        
        /// <summary>
        /// The <see cref="Stream"/> of the to be uploaded file.
        /// Is null if the <see cref="UploadType"/> is not <see cref="UploadType.Stream"/>.
        /// </summary>
        private Stream? Stream { get; }
        
        /// <summary>
        /// The <see cref="byte"/> array of the to be uploaded file.
        /// Is null if the <see cref="UploadType"/> is not <see cref="KekUploadLibrary.UploadType.ByteArray"/>.
        /// </summary>
        private byte[]? Data { get; }

        /// <summary>
        /// This method returns a <see cref="Stream"/> of the to be uploaded file.
        /// It is used in the <see cref="UploadClient"/>.
        /// It doesn't matter if the <see cref="UploadType"/> isn't <see cref="UploadType.Stream"/>,
        /// because the all the other types are converted to a <see cref="Stream"/> in this method.
        /// </summary>
        /// <returns>The <see cref="Stream"/> of the to be uploaded file.</returns>
        /// <exception cref="KekException">Is thrown when the <see cref="UploadType"/> is invalid.</exception>
        public Stream GetAsStream()
        {
            return UploadType switch
            {
                UploadType.File => File.OpenRead(FilePath!),
                UploadType.ByteArray => new MemoryStream(Data!),
                UploadType.Stream => Stream!,
                _ => throw new KekException("Invalid upload type!")
            };
        }

        /// <summary>
        /// This method returns a <see cref="byte"/> array of the to be uploaded file.
        /// It doesn't matter if the <see cref="UploadType"/> isn't <see cref="UploadType.ByteArray"/>,
        /// because the all the other types are converted to a <see cref="byte"/> array in this method.
        /// </summary>
        /// <returns>The <see cref="byte"/> array of the to be uploaded file.</returns>
        /// <exception cref="KekException">Is thrown when the <see cref="UploadType"/> is invalid.</exception>
        public byte[] GetAsByteArray()
        {
            return UploadType switch
            {
                UploadType.File => File.ReadAllBytes(FilePath!),
                UploadType.ByteArray => Data!,
                UploadType.Stream => throw new KekException("Cannot get stream as byte array!"),
                _ => throw new KekException("Invalid upload type!")
            };
        }
    }

    /// <summary>
    /// The type of location where the file is uploaded from.
    /// </summary>
    public enum UploadType
    {
        /// <summary>
        /// The file is uploaded from a file.
        /// </summary>
        File,
        /// <summary>
        /// The file is uploaded from a <see cref="byte"/> array.
        /// </summary>
        ByteArray,
        /// <summary>
        /// The file is uploaded from a <see cref="Stream"/>.
        /// </summary>
        Stream
    }
}