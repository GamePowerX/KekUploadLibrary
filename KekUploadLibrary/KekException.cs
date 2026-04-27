using System;

namespace KekUploadLibrary
{
    /// <summary>
    /// This type of exception is thrown when an error occurs in the KekUploadLibrary.
    /// </summary>
    [Serializable]
    public class KekException : Exception
    {
        /// <summary>
        /// This creates a new instance of the <see cref="KekException"/> class without any additional information.
        /// </summary>
        public KekException()
        {
        }

        /// <summary>
        /// This creates a new instance of the <see cref="KekException"/> class with the given message.
        /// </summary>
        /// <param name="message">The message of the exception.</param>
        public KekException(string message) : base(message)
        {
        }

        /// <summary>
        /// This creates a new instance of the <see cref="KekException"/> class with the given message and inner exception.
        /// </summary>
        /// <param name="message">The message of the exception.</param>
        /// <param name="inner">The inner exception.</param>
        public KekException(string message, Exception? inner) : base(message, inner)
        {
        }

        /// <summary>
        /// This creates a new instance of the <see cref="KekException"/> class with the given message, inner exception and <see cref="RequestErrorResponse"/>.
        /// </summary>
        /// <param name="message">The message of the exception.</param>
        /// <param name="inner">The inner exception.</param>
        /// <param name="error">The <see cref="RequestErrorResponse"/> of the exception.</param>
        public KekException(string message, Exception? inner, RequestErrorResponse? error) : base(message, inner)
        {
            Error = error;
        }

        /// <summary>
        /// The <see cref="RequestErrorResponse"/> of the exception.
        /// It is null if the exception is not caused by an error response to a request.
        /// </summary>
        public RequestErrorResponse? Error { get; }
    }
}
