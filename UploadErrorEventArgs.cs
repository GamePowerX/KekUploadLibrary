namespace KekUploadLibrary;

public class UploadErrorEventArgs
{
    public UploadErrorEventArgs(Exception ex)
    {
        Exception = ex;
    }
    public Exception Exception { get; set; }
}