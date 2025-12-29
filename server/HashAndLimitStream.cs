using System.Security.Cryptography;

public class HashAndLimitStream : Stream
{
    private readonly Stream _innerStream;
    private readonly IncrementalHash _hasher;
    private readonly long _totalLength;
    private long _bytesReadSoFar;

    public HashAndLimitStream(Stream innerStream, long length, IncrementalHash hasher)
    {
        _innerStream = innerStream;
        _totalLength = length;
        _hasher = hasher;
        _bytesReadSoFar = 0;
    }

    // A MÁGICA: Intercepta a leitura
    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        // 1. Limite de segurança: Não deixa ler mais que o tamanho do arquivo
        // Isso impede que a Lib leia o cabeçalho do PRÓXIMO arquivo por engano.
        long remaining = _totalLength - _bytesReadSoFar;
        if (remaining <= 0) return 0;

        int toRead = (int)Math.Min(count, remaining);

        // 2. Lê da Rede
        int read = await _innerStream.ReadAsync(buffer, offset, toRead, cancellationToken);

        // 3. Aplica o Filtro (Calcula Hash)
        if (read > 0)
        {
            _hasher.AppendData(buffer, offset, read);
            _bytesReadSoFar += read;
        }

        return read;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        // Versão síncrona (se a lib não usar async)
        long remaining = _totalLength - _bytesReadSoFar;
        if (remaining <= 0) return 0;
        int toRead = (int)Math.Min(count, remaining);

        int read = _innerStream.Read(buffer, offset, toRead);
        if (read > 0)
        {
            _hasher.AppendData(buffer, offset, read);
            _bytesReadSoFar += read;
        }
        return read;
    }

    // Faz o Stream "fingir" que sabe seu tamanho (NetworkStream original lançaria erro aqui)
    public override long Length => _totalLength;
    public override long Position
    {
        get => _bytesReadSoFar;
        set => throw new NotSupportedException("Não é possível pular posições na rede.");
    }

    // Repassa configurações básicas
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override void Flush() { }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotImplementedException();
    }
}