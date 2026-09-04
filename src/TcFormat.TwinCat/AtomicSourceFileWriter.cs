namespace TcFormat.TwinCat;

public static class AtomicSourceFileWriter
{
    public static void Write(SourceDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var directory = System.IO.Path.GetDirectoryName(document.Path)
            ?? throw new SourceDocumentException($"{document.Path}: source file has no parent directory.");
        var temporaryPath = System.IO.Path.Combine(
            directory,
            $".{System.IO.Path.GetFileName(document.Path)}.{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllBytes(temporaryPath, document.Encode());
            File.Replace(temporaryPath, document.Path, destinationBackupFileName: null, ignoreMetadataErrors: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new SourceDocumentException($"{document.Path}: could not replace source file atomically.", exception);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}

