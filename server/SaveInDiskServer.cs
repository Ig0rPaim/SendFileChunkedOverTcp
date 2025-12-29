using System.Net;
using System.Net.Sockets;
using System.Text;

namespace server
{
    internal class SaveInDiskServer
    {
        // 8MB é muito grande para buffers de rede. 81KB é um múltiplo do tamanho de cluster comum (4096)
        // e próximo do limite do LOH (Large Object Heap) do .NET.
        int bufferSizeForFileTransfer = 81920;

        static int bufferFileSizeInBytes = 8;
        static int bufferToCode = 8;
        static int bufferToCheckSum = 32;
        static int bufferFileNameSizeInBytes = 255;
        int bufferSizeForHeader = bufferFileSizeInBytes + bufferToCode + bufferToCheckSum + bufferFileNameSizeInBytes;

        public async Task Start()
        {
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
        }

        async Task ProcessRequest(TcpClient connection)
        {
            using (var networkStream = connection.GetStream())
            {
                
                DateTime initTime = DateTime.Now;
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

                string tempFolder = @"D:\REPOSITORIO_ARQUIVOS\Sapro\Diversos\GDrive\Temp";
                string tempFilePath = Path.Combine(tempFolder, "temp_" + fileNameStr);

                try
                {

                    // Caminho final na rede
                    string finalNetworkPath = Path.Combine(@"\\freia\REPOSITORIO_ARQUIVOS\Sapro\Diversos\GDrive\", "recebido_" + fileNameStr);

                    // Recebe no disco local rápido
                    bool isValid = await ReceiveAndVerifyFile(fileSize, networkStream, tempFilePath, receivedCheckSum);

                    if (!isValid)
                    {
                        Console.WriteLine("Erro: Checksum não confere!");
                        networkStream.Write(new byte[] { 1 });
                        // Limpa o arquivo corrompido
                        File.Delete(tempFilePath);
                    }
                    else
                    {
                        // 3. MOVER PARA A REDE DEPOIS (Assíncrono ao cliente)
                        // Avisamos o cliente que deu certo ANTES de mover para a rede, 
                        // pois o upload dele terminou com sucesso.
                        networkStream.Write(new byte[] { 0 });

                        var duration = DateTime.Now - initTime;
                        Console.WriteLine($"Sucesso: Recebido em {duration.TotalSeconds:F2}s. Movendo para rede...");

                        // Move o arquivo (agora isso é problema do servidor, o cliente já está livre)
                        //await Task.Run(() => File.Move(tempFilePath, finalNetworkPath, true));
                        try
                        {
                            // Usamos nossa função de buffer grande
                            await MoveFileToNetworkAsync(tempFilePath, finalNetworkPath);
                            var moveFileDuration = DateTime.Now - initTime;
                            Console.WriteLine($"Arquivo movido para {finalNetworkPath} em {moveFileDuration.TotalSeconds:F2}s");
                        }
                        catch (Exception ex)
                        {
                            // O cliente já foi embora feliz, mas precisamos logar o erro no servidor
                            Console.WriteLine($"CRÍTICO: Erro ao mover para rede: {ex.Message}");
                            // Opcional: Implementar lógica de retry ou mover para uma pasta de "Erro" local
                        }
                    }

                    await networkStream.FlushAsync();
                }
                catch (Exception ex)
                {
                    // Tratamento de erro e limpeza
                    if (File.Exists(tempFilePath)) File.Delete(tempFilePath);
                    throw;
                }
            }
        }

        async Task<bool> ReceiveAndVerifyFile(long fileSize, NetworkStream networkStream, string savePath, byte[] expectedHash)
        {
            using var sha256 = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);

            /// stream para compartilhar com outros componentes, se necessário
            //using var streamParaLib = new HashAndLimitStream(networkStream, fileSize, sha256);

            // FileOptions.Asynchronous é CRUCIAL para performance real de I/O
            using var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous);

            byte[] buffer = new byte[bufferSizeForFileTransfer];
            long totalBytesRead = 0;

            // Variáveis para controle de log (para não spammar o console)
            long lastLogBytes = 0;
            long logInterval = 1024 * 1024 * 10; // Logar a cada 10 MB

            while (totalBytesRead < fileSize)
            {
                // Lê o que tiver disponível no stream, até o tamanho do buffer
                int bytesRead = await networkStream.ReadAsync(buffer, 0, buffer.Length);

                if (bytesRead == 0) break; // Conexão fechada

                // Escreve no disco e calcula hash
                await fileStream.WriteAsync(buffer, 0, bytesRead);
                sha256.AppendData(buffer, 0, bytesRead);

                totalBytesRead += bytesRead;

                // Log mais inteligente (menos bloqueante)
                if ((totalBytesRead - lastLogBytes) > logInterval)
                {
                    double progress = (double)totalBytesRead / fileSize * 100;
                    Console.WriteLine($"Progresso: {progress:F1}% ({totalBytesRead / 1024 / 1024} MB)");
                    lastLogBytes = totalBytesRead;
                }
            }

            byte[] actualHash = sha256.GetHashAndReset();
            return actualHash.SequenceEqual(expectedHash);
        }

        //async Task MoveFileToNetworkAsync(string sourceFile, string destinationFile)
        //{
        //    // Buffer de 1MB para a cópia de REDE (SMB). 
        //    // Diferente do TCP, aqui quanto maior, melhor (até uns 4-8MB), 
        //    // pois reduz o número de "viagens" (round-trips) do protocolo.
        //    const int smbBufferSize = 1024 * 1024 * 8; // 8MB

        //    using (var sourceStream = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan))
        //    using (var destStream = new FileStream(destinationFile, FileMode.CreateNew, FileAccess.Write, FileShare.None, smbBufferSize, FileOptions.Asynchronous))
        //    {
        //        byte[] buffer = new byte[smbBufferSize];
        //        int bytesRead;

        //        while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        //        {
        //            await destStream.WriteAsync(buffer, 0, bytesRead);
        //        }
        //    }

        //    // Só deleta o original se a cópia terminar sem erros
        //    File.Delete(sourceFile);
        //}

        private async Task MoveFileToNetworkAsync(string sourceFile, string destinationFile)
        {
            const int smbBufferSize = 1024 * 1024 * 8; // 8MB

            // Obter o tamanho total para calcular a porcentagem
            long totalBytes = new FileInfo(sourceFile).Length;
            long totalRead = 0;
            long lastLogBytes = 0;
            long logInterval = 1024 * 1024 * 10; // Logar a cada 10 MB

            Console.WriteLine($"Iniciando cópia para rede: {totalBytes / 1024 / 1024} MB");

            using (var sourceStream = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan))
            using (var destStream = new FileStream(destinationFile, FileMode.CreateNew, FileAccess.Write, FileShare.None, smbBufferSize, FileOptions.Asynchronous))
            {
                byte[] buffer = new byte[smbBufferSize];
                int bytesRead;

                var sw = System.Diagnostics.Stopwatch.StartNew();

                while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await destStream.WriteAsync(buffer, 0, bytesRead);

                    totalRead += bytesRead;

                    // Log de progresso a cada X MB
                    if ((totalRead - lastLogBytes) > logInterval)
                    {
                        double progress = (double)totalRead / totalBytes * 100;
                        double speed = (totalRead / 1024.0 / 1024.0) / sw.Elapsed.TotalSeconds; // MB/s

                        Console.WriteLine($"[Rede] Movendo: {progress:F1}% - {speed:F1} MB/s");
                        lastLogBytes = totalRead;
                    }
                }
                sw.Stop();
                Console.WriteLine($"[Rede] Concluído em {sw.Elapsed.TotalSeconds:F1}s.");
            }

            // Só deleta o original se a cópia terminar sem erros
            File.Delete(sourceFile);
        }
    }
}
