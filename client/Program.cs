using System.Net.Sockets;
using System.Text;

try
{
    int bufferFileSizeInBytes = 8;
    int bufferToCode = 8;
    int bufferToCheckSum = 32;
    int bufferFileNameSizeInBytes = 255;
    //int bufferSizeForFileTransfer = 8000000;
    int bufferSizeForFileTransfer = 81920;
    int bufferSizeForHeader = bufferFileSizeInBytes + bufferToCode + bufferToCheckSum + bufferFileNameSizeInBytes;

    #region get file
    using var fileStream = new FileStream(@"C:\Users\ioliveira\Desktop\arquivoNovo.zip", FileMode.OpenOrCreate, FileAccess.ReadWrite);
    byte[] buffer = new byte[bufferSizeForFileTransfer];
    #endregion


    using var client = new TcpClient();

    client.Connect("10.10.1.61", 4000);

    Console.WriteLine("Connected to server.");

    using var networkStream = client.GetStream();

    buffer = new byte[bufferSizeForFileTransfer];

    byte[] header = new byte[bufferSizeForHeader];

    // add File size to header
    BitConverter.GetBytes(fileStream.Length).CopyTo(header, 0);

    // add code to header
    BitConverter.GetBytes(120109).CopyTo(header, bufferFileSizeInBytes);

    // add checksum to header
    byte[] checksum = await System.Security.Cryptography.SHA256.HashDataAsync(fileStream);
    checksum.CopyTo(header, bufferFileSizeInBytes + bufferToCode);
    

    // add file name to header
    Encoding.UTF8.GetBytes(Path.GetFileName(fileStream.Name)).CopyTo(header, bufferFileSizeInBytes + bufferToCode + bufferToCheckSum);

    // send header
    networkStream.Write(header, 0, bufferSizeForHeader);
    Console.WriteLine("Header sended");
    int bytesRead;

    //send chunked file
    fileStream.Position = 0;
    var total = fileStream.Length / bufferSizeForFileTransfer;
    long blocksSent = 0;
    while ((bytesRead = await fileStream.ReadAsync(buffer, 0, bufferSizeForFileTransfer)) > 0)
    {
        networkStream.Write(buffer, 0, bytesRead);
        blocksSent++;
        Console.WriteLine($"{blocksSent} enviados de {total}");
    }

    // get response
    byte[] response = [1];
    networkStream.ReadAsync(response, 0, 1).Wait(5000);

    if (response[0] == 0)
    {
        Console.WriteLine("File sent successfully.");
    }
    else
    {
        Console.WriteLine("File transfer failed.");
    }
}
catch(Exception e)
{
    Console.WriteLine($"Erro: {e.Message}");
}


static byte[] GetChecksum(byte[] file)
{
    System.Security.Cryptography.HashAlgorithm hasher = System.Security.Cryptography.SHA256.Create();
    using (hasher)
    {
        return hasher.ComputeHash(file);
    }
}
