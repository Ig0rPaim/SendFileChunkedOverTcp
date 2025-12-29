using System.Net.Sockets;
using System.Text;

try
{
    int bufferFileSizeInBytes = 8;
    int bufferToCode = 8;
    int bufferToCheckSum = 32;
    int bufferFileNameSizeInBytes = 255;
    // Otimizado para 80KB (Múltiplo de 4KB e próximo de 85KB LOH)
    int bufferSizeForFileTransfer = 81920;
    int bufferSizeForHeader = bufferFileSizeInBytes + bufferToCode + bufferToCheckSum + bufferFileNameSizeInBytes;

    string filePath = @"C:\Users\ioliveira\Desktop\arquivoNovo.zip";

    using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);

    using var client = new TcpClient();

    client.NoDelay = true;

    Console.WriteLine("Conectando ao servidor...");
    await client.ConnectAsync("10.10.1.61", 4000);
    Console.WriteLine("Conectado.");

    using var networkStream = client.GetStream();

    byte[] header = new byte[bufferSizeForHeader];

    // --- Montagem do Header ---
    Console.WriteLine("Calculando Hash...");
    byte[] checksum = await System.Security.Cryptography.SHA256.HashDataAsync(fileStream);
    fileStream.Position = 0; // Volta para o início após ler pro hash

    BitConverter.GetBytes(fileStream.Length).CopyTo(header, 0);
    BitConverter.GetBytes(120109).CopyTo(header, bufferFileSizeInBytes);
    checksum.CopyTo(header, bufferFileSizeInBytes + bufferToCode);
    Encoding.UTF8.GetBytes(Path.GetFileName(filePath)).CopyTo(header, bufferFileSizeInBytes + bufferToCode + bufferToCheckSum);

    // Envia header
    await networkStream.WriteAsync(header, 0, bufferSizeForHeader);
    Console.WriteLine("Header enviado. Iniciando transmissão de dados...");

    // --- Transmissão do Arquivo ---
    byte[] buffer = new byte[bufferSizeForFileTransfer];
    int bytesRead;
    long totalSent = 0;
    long fileSize = fileStream.Length;

    // Variáveis para controle de log (Evitar spam no console)
    long lastLogBytes = 0;
    long logInterval = 1024 * 1024 * 10; // Logar a cada 10 MB

    var sw = System.Diagnostics.Stopwatch.StartNew();

    while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
    {
        await networkStream.WriteAsync(buffer, 0, bytesRead);
        totalSent += bytesRead;

        // Só imprime se já passou mais de 10MB desde o último print
        if ((totalSent - lastLogBytes) > logInterval)
        {
            double progress = (double)totalSent / fileSize * 100;
            double speed = (totalSent / 1024.0 / 1024.0) / sw.Elapsed.TotalSeconds;
            Console.WriteLine($"Progresso: {progress:F1}% - Velocidade: {speed:F1} MB/s");
            lastLogBytes = totalSent;
        }
    }
    sw.Stop();
    Console.WriteLine($"Envio concluído em {sw.Elapsed.TotalSeconds:F2}s.");

    // --- Recebimento da Resposta ---
    byte[] response = new byte[1];

    // Timeout simples usando CancellationToken
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    try
    {
        int bytesRec = await networkStream.ReadAsync(response, 0, 1, cts.Token);

        if (bytesRec > 0 && response[0] == 0)
        {
            Console.WriteLine("Sucesso: Servidor confirmou o recebimento e validação.");
        }
        else
        {
            Console.WriteLine("Falha: Servidor retornou erro ou checksum inválido.");
        }
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Erro: Timeout aguardando resposta do servidor.");
    }
}
catch (Exception e)
{
    Console.WriteLine($"Erro Fatal: {e.Message}");
}