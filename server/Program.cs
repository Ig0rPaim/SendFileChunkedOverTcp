using System.Net;
using System.Net.Sockets;
using System.Text;


int bufferFileSizeInBytes = 8;
int bufferToCode = 8;
int bufferToCheckSum = 32;
int bufferFileNameSizeInBytes = 255;
int bufferSizeForFileTransfer = 8192;
int bufferSizeForHeader = bufferFileSizeInBytes + bufferToCode + bufferToCheckSum + bufferFileNameSizeInBytes;

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
        var initTime = DateTime.Now;
        using (connection)
        using (var networkStream = connection.GetStream())
        {
            Console.WriteLine($"Cliente conectado: {connection.Client.RemoteEndPoint}");

            byte[] header = new byte[bufferSizeForHeader];
            int totalReadHeader = 0;
            while (totalReadHeader < bufferSizeForHeader)
            {
                int read = await networkStream.ReadAsync(header, totalReadHeader, bufferSizeForHeader - totalReadHeader);
                if (read == 0) return;
                totalReadHeader += read;
            }

            long fileSize = BitConverter.ToInt64(header, 0);
            int code = BitConverter.ToInt32(header, 8);
            byte[] receivedCheckSum = header[16..48];
            string fileNameStr = Encoding.UTF8.GetString(header, 48, 255).TrimEnd('\0');

            string savePath = Path.Combine(@"C:\Users\ioliveira\Desktop\", "recebido_" + fileNameStr);
            bool isValid = await ReceiveAndVerifyFile(fileSize, networkStream, savePath, receivedCheckSum);

            if (!isValid)
            {
                Console.WriteLine("Erro: Checksum não confere!");
                networkStream.Write(new byte[] { 1 }); // 1 para Erro
            }
            else
            {
                var duration = DateTime.Now - initTime;
                Console.WriteLine($"Sucesso: {fileNameStr} recebido em {duration.TotalSeconds:F2}s. Code: {code}");
                networkStream.Write(new byte[] { 0 }); // 0 para Sucesso
            }

            await networkStream.FlushAsync();
        }
    }
    catch (Exception e)
    {
        Console.WriteLine($"Erro no processamento: {e.Message}");
    }
}

async Task<bool> ReceiveAndVerifyFile(long fileSize, NetworkStream networkStream, string savePath, byte[] expectedHash)
{
    using var sha256 = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
    using var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);

    byte[] buffer = new byte[bufferSizeForFileTransfer];
    long totalBytesRead = 0;

    while (totalBytesRead < fileSize)
    {
        int toRead = (int)Math.Min(buffer.Length, fileSize - totalBytesRead);
        int bytesRead = await networkStream.ReadAsync(buffer, 0, toRead);
        if (bytesRead == 0) break;

        await fileStream.WriteAsync(buffer, 0, bytesRead);

        sha256.AppendData(buffer, 0, bytesRead);

        totalBytesRead += bytesRead;
    }

    byte[] actualHash = sha256.GetHashAndReset();
    return actualHash.SequenceEqual(expectedHash);
}
