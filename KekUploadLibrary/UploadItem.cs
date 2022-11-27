using System;
using System.IO;

namespace KekUploadLibrary
{
    public class UploadItem
    {
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

        public UploadItem(byte[] data, string extension)
        {
            UploadType = UploadType.Data;
            Extension = extension;
            Data = data;
        }

        public UploadItem(byte[] data, string extension, string name)
        {
            UploadType = UploadType.Data;
            Extension = extension;
            Name = name;
            Data = data;
        }

        public UploadItem(Stream stream, string extension)
        {
            UploadType = UploadType.Stream;
            Extension = extension;
            Stream = stream;
        }

        public UploadItem(Stream stream, string extension, string name)
        {
            UploadType = UploadType.Stream;
            Extension = extension;
            Name = name;
            Stream = stream;
        }

        public UploadType UploadType { get; }
        public string? FilePath { get; }
        public string Extension { get; }
        public string? Name { get; }
        private Stream? Stream { get; }
        private byte[]? Data { get; }

        public Stream GetAsStream()
        {
            return UploadType switch
            {
                UploadType.File => File.OpenRead(FilePath!),
                UploadType.Data => new MemoryStream(Data!),
                UploadType.Stream => Stream!,
                _ => throw new KekException("Invalid upload type!")
            };
        }
    }

    public enum UploadType
    {
        File,
        Data,
        Stream
    }
}