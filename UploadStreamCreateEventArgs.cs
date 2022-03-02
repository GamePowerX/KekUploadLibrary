namespace KekUploadLibrary;

public class UploadStreamCreateEventArgs : EventArgs
{
    public UploadStreamCreateEventArgs(string uploadStreamId)
    {
        UploadStreamId = uploadStreamId;
    }
    
    public string UploadStreamId { get; set; }
}