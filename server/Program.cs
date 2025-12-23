using System.Net;
using System.Net.Sockets;


int bufferFileSizeInBytes = 8;
int bufferToCheckSum = 32;
int bufferFileNameSizeInBytes = 255;
int bufferSizeForFileTransfer = 8192;
int bufferSizeForHeader = bufferFileSizeInBytes + bufferToCheckSum + bufferFileNameSizeInBytes;

using var tcpListener = new TcpListener(IPAddress.Any, 4000);
tcpListener.Start();
Console.WriteLine("Servidor aguardando conexões...");

while (true)
{
    try
    {
        var connection = await tcpListener.AcceptTcpClientAsync();
        _ = ProcessRequest(connection);
    }
    catch (Exception e)
    {
        Console.WriteLine($"Erro: {e.Message}");
    }
}



async Task ProcessRequest(TcpClient connection)
{
    try
    {
        var initTime = new TimeSpan(DateTime.Now.Ticks);
        using (connection)
        using (var networkStream = connection.GetStream())
        {
            Console.WriteLine($"Cliente conectado: {connection.Client.RemoteEndPoint}");

            byte[] header = new byte[bufferSizeForHeader];
            int totalReadHeader = 0;
            //get header
            while (totalReadHeader < bufferSizeForHeader)
            {
                int read = await networkStream.ReadAsync(header, totalReadHeader, bufferSizeForHeader - totalReadHeader);
                if (read == 0) return;
                totalReadHeader += read;
            }

            //get File size
            long fileSize = BitConverter.ToInt64(header[0..bufferFileSizeInBytes]);

            //get CheckSum
            byte[] receivedCheckSum = header[bufferFileSizeInBytes..(bufferFileSizeInBytes + bufferToCheckSum)];

            //get file Name
            string fileNameStr = System.Text.Encoding.UTF8.GetString(header, bufferFileSizeInBytes + bufferToCheckSum, bufferFileNameSizeInBytes).TrimEnd('\0');

            
            byte[] fileInBytes = await ReadAndStoreBytes(fileSize, networkStream);

            byte[] fileCheckSum = GetChecksum(fileInBytes);

            if (!fileCheckSum.SequenceEqual(receivedCheckSum))
            {
                Console.WriteLine($"Erro: o checksum é inválido");
                var finishErrorTime = new TimeSpan(DateTime.Now.Ticks);
                Console.WriteLine($"Duração: {finishErrorTime.Subtract(initTime).Seconds} segundos.");
                return;
            }

            Console.WriteLine($"Sucesso: {fileNameStr} recebido.");
            var finishTime = new TimeSpan(DateTime.Now.Ticks);
            Console.WriteLine($"Duração: {finishTime.Subtract(initTime).Seconds} segundos.");

            networkStream.Write(new byte[] { 0 });
            await networkStream.FlushAsync();
        }
    }
    catch (Exception e)
    {
        Console.WriteLine($"Erro: {e.Message}");
    }
}

static byte[] GetChecksum(byte[] file)
{
    System.Security.Cryptography.HashAlgorithm hasher = System.Security.Cryptography.SHA256.Create();
    using (hasher)
    {
        return hasher.ComputeHash(file);
    }
}

async Task ReadAndSaveFile(long fileSize, NetworkStream networkStream)
{
    string savePath = Path.Combine(@"C:\Users\ioliveira\Desktop\", "novoArquivo.zip");

    using (var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
    {
        byte[] buffer = new byte[bufferSizeForFileTransfer];
        long totalBytesRead = 0;

        while (totalBytesRead < fileSize)
        {
            int toRead = (int)Math.Min(buffer.Length, fileSize - totalBytesRead);
            int bytesRead = await networkStream.ReadAsync(buffer, 0, toRead);
            if (bytesRead == 0) break;

            await fileStream.WriteAsync(buffer, 0, bytesRead);
            totalBytesRead += bytesRead;
        }
    }
}

async Task<byte[]> ReadAndStoreBytes(long fileSize, NetworkStream networkStream)
{
    List<byte> file = new();
    byte[] buffer = new byte[bufferSizeForFileTransfer];
    long totalBytesRead = 0;

    while (totalBytesRead < fileSize)
    {
        int toRead = (int)Math.Min(buffer.Length, fileSize - totalBytesRead);
        int bytesRead = await networkStream.ReadAsync(buffer, 0, toRead);
        if (bytesRead == 0) break;
        file.AddRange(buffer[0..bytesRead]);
        totalBytesRead += bytesRead;
    }
    return file.ToArray();
}