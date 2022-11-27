using System;

namespace KekUploadLibrary
{
    public class UploadCompleteEventArgs : EventArgs
    {
        public UploadCompleteEventArgs(string? filePath, string fileUrl)
        {
            FilePath = filePath;
            FileUrl = fileUrl;
        }

        public string? FilePath { get; set; }
        public string FileUrl { get; set; }
    }
}