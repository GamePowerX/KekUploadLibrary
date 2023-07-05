using System;

namespace KekUploadLibrary
{
    /// <summary>
    /// This class contains the event arguments for the <see cref="UploadClient.UploadErrorEvent"/>.
    /// </summary>
    public class UploadErrorEventArgs
    {
        /// <summary>
        /// This creates a new instance of the <see cref="UploadErrorEventArgs"/> class.
        /// </summary>
        /// <param name="ex">The exception that was thrown.</param>
        /// <param name="errorResponse">The error response from the server.</param>
        public UploadErrorEventArgs(Exception ex, RequestErrorResponse? errorResponse)
        {
            Exception = ex;
            ErrorResponse = errorResponse;
        }

        /// <summary>
        /// The exception that was thrown.
        /// </summary>
        public Exception Exception { get; set; }
        
        /// <summary>
        /// The error response from the server.
        /// Can be <see langword="null"/> if the error was not caused by the server or if the error response could not be parsed.
        /// </summary>
        public RequestErrorResponse? ErrorResponse { get; set; }
    }
}