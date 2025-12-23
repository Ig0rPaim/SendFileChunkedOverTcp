using System.Net.Sockets;
using System.Text;

try
{
    int bufferFileSizeInBytes = 8;
    int bufferToCheckSum = 32;
    int bufferFileNameSizeInBytes = 255;
    int bufferSizeForFileTransfer = 8192;
    int bufferSizeForHeader = bufferFileSizeInBytes + bufferToCheckSum + bufferFileNameSizeInBytes;

    #region get file
    using var fileStream = new FileStream(@"C:\Users\ioliveira\Desktop\sapro_processo_comp.zip", FileMode.OpenOrCreate, FileAccess.ReadWrite);
    byte[] buffer = new byte[bufferSizeForFileTransfer];
    #endregion


    using var client = new TcpClient();

    client.Connect("localhost", 4000);

    using var networkStream = client.GetStream();

    buffer = new byte[bufferSizeForFileTransfer];

    byte[] header = new byte[bufferSizeForHeader];

    // add File size to header
    BitConverter.GetBytes(fileStream.Length).CopyTo(header, 0);

    // add checksum to header
    using (var ms = new MemoryStream())
    {
        await fileStream.CopyToAsync(ms);

        byte[] checksum = GetChecksum(ms.ToArray());

        checksum.CopyTo(header, bufferFileSizeInBytes);
    }

    // add file name to header
    Encoding.UTF8.GetBytes(Path.GetFileName(fileStream.Name)).CopyTo(header, bufferFileSizeInBytes + bufferToCheckSum);

    // send header
    networkStream.Write(header, 0, bufferSizeForHeader);
    int bytesRead;

    //send chunked file
    fileStream.Position = 0;
    while ((bytesRead = await fileStream.ReadAsync(buffer, 0, bufferSizeForFileTransfer)) > 0)
    {
        networkStream.Write(buffer, 0, bytesRead);
    }

    // get response
    byte[] response = [1];
    networkStream.ReadAsync(response, 0, 1).Wait(3000);

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
