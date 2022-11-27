using System;

namespace KekUploadLibrary
{
    public class UploadErrorEventArgs
    {
        public UploadErrorEventArgs(Exception ex, RequestErrorResponse? errorResponse)
        {
            Exception = ex;
            ErrorResponse = errorResponse;
        }

        public Exception Exception { get; set; }
        public RequestErrorResponse? ErrorResponse { get; set; }
    }
}