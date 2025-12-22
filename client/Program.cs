
using System.Net.Sockets;
using System.Text;

try
{
    int bufferFileSizeInBytes = 32;
    int bufferFileNameSizeInBytes = 255;
    int bufferSizeForFileTransfer = 8192;
    int bufferSizeForHeader = bufferFileSizeInBytes + bufferFileNameSizeInBytes;

    #region get file
    using var fileStream = new FileStream(@"/path/to/file", FileMode.OpenOrCreate, FileAccess.ReadWrite);
    byte[] buffer = new byte[bufferSizeForFileTransfer];
    #endregion


    using var client = new TcpClient();

    client.Connect("localhost", 4000);

    using var networkStream = client.GetStream();

    buffer = new byte[bufferSizeForFileTransfer];

    byte[] header = new byte[bufferSizeForHeader];
    BitConverter.GetBytes(fileStream.Length).CopyTo(header, 0);
    Encoding.UTF8.GetBytes(Path.GetFileName(fileStream.Name)).CopyTo(header, bufferFileSizeInBytes);

    
    networkStream.Write(header, 0, bufferSizeForHeader);
    int bytesRead;

    while ((bytesRead = await fileStream.ReadAsync(buffer, 0, bufferSizeForFileTransfer)) > 0)
    {
        networkStream.Write(buffer, 0, bytesRead);
    }
}
catch(Exception e)
{
    Console.WriteLine($"Erro: {e.Message}");
}

