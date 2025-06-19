namespace Ormur.Services;

public class SyncService
{
    private readonly string _dbFile = Path.Combine(FileSystem.AppDataDirectory, "ormur.db");
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private static readonly SemaphoreSlim FileSemaphore = new(1, 1);
    private readonly TimeSpan _fileOperationTimeout = TimeSpan.FromSeconds(10);

    public static string? GetLocalIpAddress()
    {
        try
        {
            // First try to get the most likely IP (works for both Android and desktop)
            foreach (var netInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                // Skip loopback and non-operational interfaces
                if (netInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                    netInterface.OperationalStatus != OperationalStatus.Up)
                {
                    continue;
                }

                // Get the first IPv4 address
                var ipInfo = netInterface.GetIPProperties().UnicastAddresses
                    .FirstOrDefault(ip => ip.Address.AddressFamily == AddressFamily.InterNetwork &&
                                          !IPAddress.IsLoopback(ip.Address));

                if (ipInfo?.Address != null)
                {
                    return ipInfo.Address.ToString();
                }
            }

            // Fallback for desktop environments
            var hostEntry = Dns.GetHostEntry(Dns.GetHostName());
            return hostEntry.AddressList
                .FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork)?
                .ToString();
        }
        catch
        {
            return null;
        }
    }

    public async Task SyncViaWiFiAsync(string serverIp, int port = 8888)
    {
        await FileSemaphore.WaitAsync();
        try
        {
            // Create a backup of current database
            var backupFile = $"{_dbFile}.backup";
            File.Copy(_dbFile, backupFile, overwrite: true);

            try
            {
                await EnsureDatabaseClosedAsync();

                using var client = new TcpClient();
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)); // 10 second timeout

                try
                {
                    await client.ConnectAsync(serverIp, port, cts.Token);
                    await using var stream = client.GetStream();

                    // Read and send the file
                    var dbBytes = await ReadFileWithRetry(_dbFile);
                    await stream.WriteAsync(BitConverter.GetBytes(dbBytes.Length), cts.Token);
                    await stream.WriteAsync(dbBytes, cts.Token);

                    // Wait for confirmation
                    var responseBuffer = new byte[1];
                    await stream.ReadAsync(responseBuffer, cts.Token);
                    if (responseBuffer[0] != 1)
                    {
                        throw new Exception("Remote device failed to process the database");
                    }
                }
                catch (OperationCanceledException) when (cts.IsCancellationRequested)
                {
                    throw new TimeoutException("Connection attempt timed out");
                }
            }
            catch
            {
                // Restore backup if sync failed
                if (File.Exists(backupFile))
                {
                    File.Copy(backupFile, _dbFile, overwrite: true);
                }

                throw;
            }
            finally
            {
                if (File.Exists(backupFile))
                {
                    File.Delete(backupFile);
                }
            }
        }
        finally
        {
            FileSemaphore.Release();
        }
    }

    private async Task EnsureDatabaseClosedAsync()
    {
        // Close all SQLite connections
        SqliteConnection.ClearAllPools();

        // Additional check to ensure file is not locked
        await WaitForFileAccess(_dbFile, _fileOperationTimeout);
    }

    private async Task WaitForFileAccess(string filePath, TimeSpan timeout)
    {
        var startTime = DateTime.UtcNow;
        while (DateTime.UtcNow - startTime < timeout)
        {
            try
            {
                await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                return; // File is accessible
            }
            catch (IOException)
            {
                await Task.Delay(200);
            }
        }

        throw new TimeoutException($"Could not gain access to file {filePath} within timeout");
    }

    private async Task<byte[]> ReadFileWithRetry(string filePath, int maxRetries = 5, int delayMs = 200)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                return await File.ReadAllBytesAsync(filePath);
            }
            catch (IOException) when (i < maxRetries - 1)
            {
                await Task.Delay(delayMs * (i + 1));
            }
        }

        throw new IOException($"Failed to read file after {maxRetries} attempts");
    }

    public void StartWiFiSyncServer(int port = 8888)
    {
        if (_listener != null) return;

        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();

        _ = Task.Run(async () =>
        {
            try
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    var client = await _listener.AcceptTcpClientAsync(_cts.Token);
                    _ = HandleClientAsync(client).ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                        {
                            Console.WriteLine($"Client handling error: {t.Exception?.Message}");
                        }
                    });
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when server is stopped
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Sync server error: {ex.Message}");
            }
            finally
            {
                _listener?.Stop();
                _listener = null;
            }
        }, _cts.Token);
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        try
        {
            await using var stream = client.GetStream();

            // Read the size of incoming data
            var sizeBuffer = new byte[4];
            await stream.ReadAsync(sizeBuffer);
            int size = BitConverter.ToInt32(sizeBuffer, 0);

            // Read the actual data
            var buffer = new byte[size];
            int bytesRead = 0;
            while (bytesRead < size)
            {
                bytesRead += await stream.ReadAsync(buffer.AsMemory(bytesRead, size - bytesRead));
            }

            await FileSemaphore.WaitAsync();
            try
            {
                // Create backup
                var backupFile = $"{_dbFile}.backup";
                File.Copy(_dbFile, backupFile, overwrite: true);

                try
                {
                    await EnsureDatabaseClosedAsync();

                    // Write to temporary file first
                    var tempFile = Path.GetTempFileName();
                    try
                    {
                        await File.WriteAllBytesAsync(tempFile, buffer);

                        // Atomic replacement
                        File.Replace(tempFile, _dbFile, null);

                        // Send success confirmation
                        await stream.WriteAsync(new byte[] { 1 });
                    }
                    finally
                    {
                        if (File.Exists(tempFile))
                        {
                            File.Delete(tempFile);
                        }
                    }
                }
                catch
                {
                    // Restore backup if sync failed
                    if (File.Exists(backupFile))
                    {
                        File.Copy(backupFile, _dbFile, overwrite: true);
                    }

                    await stream.WriteAsync(new byte[] { 0 });
                    throw;
                }
                finally
                {
                    if (File.Exists(backupFile))
                    {
                        File.Delete(backupFile);
                    }
                }
            }
            finally
            {
                FileSemaphore.Release();
            }
        }
        finally
        {
            client.Dispose();
        }
    }

    public void StopWiFiSyncServer()
    {
        _cts?.Cancel();
    }
}