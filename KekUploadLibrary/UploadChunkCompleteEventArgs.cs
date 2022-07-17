namespace KekUploadLibrary;

public class UploadChunkCompleteEventArgs : EventArgs
{
    public UploadChunkCompleteEventArgs(string chunkHash, int currentChunkCount, int totalChunkCount)
    {
        ChunkHash = chunkHash;
        CurrentChunkCount = currentChunkCount;
        TotalChunkCount = totalChunkCount;
    }

    public string ChunkHash { get; set; }
    public int CurrentChunkCount { get; set; }
    public int TotalChunkCount { get; set; }
}