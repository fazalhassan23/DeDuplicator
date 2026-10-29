namespace DeDuplicator.Infrastructure;

internal sealed class ReadMeter(Stream source, Action<int> consumed) : Stream
{
    public override bool CanRead => source.CanRead;
    public override bool CanSeek => source.CanSeek;
    public override bool CanWrite => false;
    public override long Length => source.Length;
    public override long Position { get => source.Position; set => source.Position = value; }
    public override int Read(byte[] buffer, int offset, int count) { int read = source.Read(buffer, offset, count); consumed(read); return read; }
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default) { int read = await source.ReadAsync(buffer, token); consumed(read); return read; }
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token) => ReadAsync(buffer.AsMemory(offset, count), token).AsTask();
    public override long Seek(long offset, SeekOrigin origin) => source.Seek(offset, origin);
    public override void Flush() => source.Flush();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    // This meter borrows the validated handle; its owner controls the handle lifetime.
}
