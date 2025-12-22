using System.Net;
using System.Net.Sockets;


int bufferFileSizeInBytes = 32;
int bufferFileNameSizeInBytes = 255;
int bufferSizeForFileTransfer = 8192;
int bufferSizeForHeader = bufferFileSizeInBytes + bufferFileNameSizeInBytes;

using var tcpListener = new TcpListener(IPAddress.Any, 4000);
tcpListener.Start();
Console.WriteLine("Servidor aguardando conexões...");

while (true)
{
    try
    {
        using var connection = await tcpListener.AcceptTcpClientAsync();
        using var networkStream = connection.GetStream();
        Console.WriteLine("Cliente conectado.");

        byte[] header = new byte[bufferSizeForHeader];
        int totalReadHeader = 0;
        while (totalReadHeader < bufferSizeForHeader)
        {
            int read = await networkStream.ReadAsync(header, totalReadHeader, bufferSizeForHeader - totalReadHeader);
            if (read == 0) break; // Cliente fechou a conexão
            totalReadHeader += read;
        }
        byte[] fileLenght = header.AsSpan(0, 32).ToArray();
        byte[] fileName = header.AsSpan(32).ToArray();

        long fileSize = BitConverter.ToInt64(fileLenght, 0);
        Console.WriteLine($"Tamanho esperado do arquivo: {fileSize} bytes");

        string fileNameStr = System.Text.Encoding.UTF8.GetString(fileName).TrimEnd('\0');
        Console.WriteLine($"Nome do arquivo: {fileNameStr}");

        byte[] buffer = new byte[bufferSizeForFileTransfer];
        long totalBytesRead = 0;

        using var memoryStream = new MemoryStream();
        while (totalBytesRead < fileSize)
        {
            int bytesRead = await networkStream.ReadAsync(buffer, 0, buffer.Length);
            if (bytesRead == 0) break; // Cliente fechou a conexão

            totalBytesRead += bytesRead;

            await memoryStream.WriteAsync(buffer, 0, bytesRead);
        }

        using var fileStream = new FileStream(@$"path/to/save/file/{fileNameStr}", FileMode.Create, FileAccess.Write);
        fileStream.Write(memoryStream.ToArray());

        Console.WriteLine($"Download concluído: {totalBytesRead} bytes recebidos.");
    }
    catch (Exception e)
    {
        Console.WriteLine($"Erro: {e.Message}");
    }
}