using System;
using System.Runtime.Serialization;

namespace KekUploadLibrary
{
    [Serializable]
    public class KekException : Exception
    {
        public KekException()
        {
        }

        public KekException(string message) : base(message)
        {
        }

        public KekException(string message, Exception? inner) : base(message, inner)
        {
        }

        public KekException(string message, Exception? inner, RequestErrorResponse? error) : base(message, inner)
        {
            Error = error;
        }

        protected KekException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

        public RequestErrorResponse? Error { get; }
    }
}