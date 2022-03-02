using System.Runtime.Serialization;

namespace KekUploadLibrary;

[Serializable]
public class KekException : Exception
{
    public KekException() : base() { }
    public KekException(string message) : base(message) { }
    public KekException(string message, Exception? inner) : base(message, inner) { }
    protected KekException(SerializationInfo info, StreamingContext context) : base(info, context) { }
}