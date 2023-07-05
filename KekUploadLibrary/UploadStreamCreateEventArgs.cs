using System;

namespace KekUploadLibrary
{
    /// <summary>
    /// This class contains the event arguments for the <see cref="UploadClient.UploadStreamCreateEvent"/>.
    /// </summary>
    public class UploadStreamCreateEventArgs : EventArgs
    {
        /// <summary>
        /// This creates a new instance of the <see cref="UploadStreamCreateEventArgs"/> class.
        /// </summary>
        /// <param name="uploadStreamId">The id of the upload stream.</param>
        public UploadStreamCreateEventArgs(string uploadStreamId)
        {
            UploadStreamId = uploadStreamId;
        }

        /// <summary>
        /// The id of the upload stream.
        /// </summary>
        public string UploadStreamId { get; set; }
    }
}