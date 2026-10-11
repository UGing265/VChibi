using System.IO;
namespace VChibi.Services;

public sealed class AppLock : IDisposable
{
    private readonly string _lockFilePath;
    private FileStream? _lockStream;

    public bool IsAcquired => _lockStream != null;

    public AppLock(string lockFilePath)
    {
        _lockFilePath = lockFilePath;
    }

    public bool TryAcquire()
    {
        try
        {
            var dir = Path.GetDirectoryName(_lockFilePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            _lockStream = new FileStream(
                _lockFilePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 1,
                FileOptions.DeleteOnClose
            );
            return true;
        }
        catch (IOException)
        {
            _lockStream = null;
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            _lockStream = null;
            return false;
        }
    }

    public void Dispose()
    {
        if (_lockStream != null)
        {
            try
            {
                _lockStream.Dispose();
            }
            catch
            {
                // Ignore disposal errors on shutdown
            }
            finally
            {
                _lockStream = null;
            }
        }
    }
}
