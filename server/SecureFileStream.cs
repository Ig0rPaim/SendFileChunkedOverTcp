namespace server
{
    public class SecureFileStream : Stream
    {
        private readonly Stream _innerStream;
        private readonly long _fileLength;
        private long _bytesRead = 0;

        public SecureFileStream(Stream networkStream, long fileSize)
        {
            _innerStream = networkStream;
            _fileLength = fileSize;
        }

        // Método auxiliar para calcular quanto ainda pode ser lido
        private int GetAllowedCount(int requestedCount)
        {
            long remaining = _fileLength - _bytesRead;
            if (remaining <= 0) return 0;
            return (int)Math.Min(requestedCount, remaining);
        }

        // 1. Sobrescreve a leitura assíncrona (moderna)
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int toRead = GetAllowedCount(buffer.Length);
            if (toRead <= 0) return 0;

            int read = await _innerStream.ReadAsync(buffer.Slice(0, toRead), cancellationToken);
            _bytesRead += read;
            return read;
        }

        // 2. Sobrescreve a leitura assíncrona (legado)
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            int toRead = GetAllowedCount(count);
            if (toRead <= 0) return 0;

            int read = await _innerStream.ReadAsync(buffer, offset, toRead, cancellationToken);
            _bytesRead += read;
            return read;
        }

        // 3. Sobrescreve a leitura síncrona
        public override int Read(byte[] buffer, int offset, int count)
        {
            int toRead = GetAllowedCount(count);
            if (toRead <= 0) return 0;

            int read = _innerStream.Read(buffer, offset, toRead);
            _bytesRead += read;
            return read;
        }

        // 4. Sobrescreve a leitura de um único byte
        public override int ReadByte()
        {
            if (_bytesRead >= _fileLength) return -1;
            int b = _innerStream.ReadByte();
            if (b != -1) _bytesRead++;
            return b;
        }

        // Bloqueios de Segurança Adicionais
        public override bool CanRead => _innerStream.CanRead;
        public override bool CanSeek => false;
        public override long Length => _fileLength;
        public override long Position
        {
            get => _bytesRead;
            set => throw new NotSupportedException("Set Position bloqueado");
        }
        public override bool CanWrite => false;

        public override void Flush() => _innerStream.Flush();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException("Seek bloqueado.");
        public override void SetLength(long value) => throw new NotSupportedException("SetLength bloqueado.");
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException("Write bloqueado.");
    }
}
