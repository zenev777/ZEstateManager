namespace ZEstate.Core.Interfaces
{
    public interface IFileStorage
    {
        // Returns an opaque storage path to save on the Document row - pass it back
        // to OpenReadAsync/Delete, don't parse it.
        Task<string> SaveAsync(Stream content, string fileName, CancellationToken ct = default);

        Task<Stream?> OpenReadAsync(string storagePath, CancellationToken ct = default);

        void Delete(string storagePath);
    }
}
