using System;

namespace KekUploadLibrary
{
    /// <summary>
    /// This class contains the event arguments for the <see cref="UploadClient.UploadCompleteEvent"/>.
    /// </summary>
    public class UploadCompleteEventArgs : EventArgs
    {
        /// <summary>
        /// This creates a new instance of the <see cref="UploadCompleteEventArgs"/> class.
        /// </summary>
        /// <param name="filePath">The path of the uploaded file.</param>
        /// <param name="fileUrl">The url of the uploaded file.</param>
        public UploadCompleteEventArgs(string? filePath, string fileUrl)
        {
            FilePath = filePath;
            FileUrl = fileUrl;
        }

        /// <summary>
        /// The path of the uploaded file.
        /// Is null if the the <see cref="UploadItem.UploadType"/> is not <see cref="UploadType.File"/>.
        /// </summary>
        public string? FilePath { get; set; }
        
        /// <summary>
        /// The url of the uploaded file.
        /// It can be used to download the file.
        /// </summary>
        public string FileUrl { get; set; }
    }
}