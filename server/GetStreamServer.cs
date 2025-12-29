using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace server
{
    internal class GetStreamServer
    {
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

                using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

                using var streamParaLib = new HashAndLimitStream(networkStream, fileSize, hasher);

                try
                {
                    /// call library method to read from streamParaLib and process the file

                    if (streamParaLib.Position < fileSize)
                    {
                        Console.WriteLine("Aviso: A lib não leu o arquivo todo.");
                    }

                    byte[] actualHash = hasher.GetHashAndReset();

                    if (actualHash.SequenceEqual(receivedCheckSum))
                    {
                        Console.WriteLine("Sucesso: Lib processou e Hash validado.");
                        networkStream.Write(new byte[] { 0 }); // Sucesso
                    }
                    else
                    {
                        Console.WriteLine("Erro: Hash inválido após processamento da Lib.");
                        networkStream.Write(new byte[] { 1 }); // Erro
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro durante a recepção do arquivo: {ex.Message}");
                    return;
                }

            }
        }
    }
}
