using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using SharpHash.Interfaces;

namespace KekUploadLibrary
{
    /// <summary>
    /// This class contains some useful methods.
    /// </summary>
    public static class Utils
    {
        /// <summary>
        /// This method hashes the given byte array with SHA1.
        /// </summary>
        /// <param name="data">The byte array.</param>
        /// <returns>The hash as a string.</returns>
        public static string HashBytes(byte[] data)
        {
            var hash = SHA1.Create().ComputeHash(data);
            return string.Concat(hash.Select(b => b.ToString("x2")));
        }

        /// <summary>
        /// This method hashes the given file with SHA1.
        /// </summary>
        /// <param name="file">The file path.</param>
        /// <returns>The hash as a string.</returns>
        public static string HashFile(string file)
        {
            var stream = File.OpenRead(file);
            var hash = SHA1.Create().ComputeHash(stream);
            return string.Concat(hash.Select(b => b.ToString("x2")));
        }

        /// <summary>
        /// This method hashes the given stream with SHA1.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <returns>The hash as a string.</returns>
        public static string HashStream(Stream stream)
        {
            var hash = SHA1.Create().ComputeHash(stream);
            return string.Concat(hash.Select(b => b.ToString("x2")));
        }

        /// <summary>
        /// This method transforms the given hash with the given data.
        /// </summary>
        /// <param name="hash">The hash.</param>
        /// <param name="data">The data.</param>
        /// <returns>The transformed hash.</returns>
        public static IHash UpdateHash(IHash hash, byte[] data)
        {
            hash.TransformBytes(data);
            return hash;
        }

        /// <summary>
        /// This method parses the upload stream id from the given string (JSON response from the KekUploadServer API).
        /// </summary>
        /// <param name="streamId">The string.</param>
        /// <returns>The upload stream id or null if the parsing failed.</returns>
        public static string? ParseUploadStreamId(string streamId)
        {
            var serializer = new JsonSerializer();
            var id = serializer.Deserialize<Dictionary<string, string>>(new JsonTextReader(new StringReader(streamId)));
            return id?["stream"];
        }

        /// <summary>
        /// This method parses the download id from the given string (JSON response from the KekUploadServer API).
        /// </summary>
        /// <param name="downloadId">The string.</param>
        /// <returns>The download id or null if the parsing failed.</returns>
        public static string? ParseDownloadId(string downloadId)
        {
            var serializer = new JsonSerializer();
            var id = serializer.Deserialize<Dictionary<string, string>>(
                new JsonTextReader(new StringReader(downloadId)));
            return id?["id"];
        }

        /// <summary>
        /// This method converts the given size to a human readable string.
        /// </summary>
        /// <param name="size">The size in bytes.</param>
        /// <returns>The human readable string.</returns>
        public static string SizeToString(long size)
        {
            if (size >= 1099511627776)
                return decimal.Round((decimal) (Math.Round(size / 10995116277.76) * 0.01), 2) + " TiB";
            if (size >= 1073741824)
                return decimal.Round((decimal) (Math.Round(size / 10737418.24) * 0.01), 2) + " GiB";
            if (size >= 1048576)
                return decimal.Round((decimal) (Math.Round(size / 10485.76) * 0.01), 2) + " MiB";
            if (size >= 1024)
                return decimal.Round((decimal) (Math.Round(size / 10.24) * 0.01), 2) + " KiB";
            return size + " bytes";
        }
    }
}