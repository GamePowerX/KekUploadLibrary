using System;

namespace KekUploadLibrary
{
    /// <summary>
    /// This class contains the event arguments for the <see cref="UploadClient.UploadChunkCompleteEvent"/>.
    /// </summary>
    public class UploadChunkCompleteEventArgs : EventArgs
    {
        /// <summary>
        /// This creates a new instance of the <see cref="UploadChunkCompleteEventArgs"/> class.
        /// </summary>
        /// <param name="chunkHash">The hash of the chunk.</param>
        /// <param name="currentChunkCount">The current chunk count.</param>
        /// <param name="totalChunkCount">The total chunk count.</param>
        public UploadChunkCompleteEventArgs(string? chunkHash, int currentChunkCount, int totalChunkCount)
        {
            ChunkHash = chunkHash;
            CurrentChunkCount = currentChunkCount;
            TotalChunkCount = totalChunkCount;
        }

        /// <summary>
        /// The hash of the chunk.
        /// Can be <see langword="null"/> if <see cref="UploadClient._withChunkHashing"/> is <see langword="false"/>.
        /// </summary>
        public string? ChunkHash { get; set; }
        
        /// <summary>
        /// The current chunk count.
        /// </summary>
        public int CurrentChunkCount { get; set; }
        
        /// <summary>
        /// The total chunk count.
        /// </summary>
        public int TotalChunkCount { get; set; }
    }
}