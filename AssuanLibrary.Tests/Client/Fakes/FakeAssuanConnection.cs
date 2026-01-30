// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Client.Abstractions;
using AssuanLibrary.Transport;

namespace AssuanLibrary.Tests.Client.Fakes;

internal sealed class FakeAssuanConnection : IAssuanConnection {
  public List<byte[]> Writes { get; } = new();

  public Queue<byte[]> ReadBuffers { get; } = new();
  public Queue<ReadOnlyMemory<byte>> ReadMemoryBuffers { get; } = new();

  public InquireHandler? LastInquireHandler { get; private set; }
  public AsyncInquireHandler? LastAsyncInquireHandler { get; private set; }

  public int OpenCalls { get; private set; }
  public int CloseCalls { get; private set; }
  public int DisposeCalls { get; private set; }
  public int DisposeAsyncCalls { get; private set; }

  public Exception? ThrowOnOpen { get; set; }
  public Exception? ThrowOnOpenAsync { get; set; }
  public bool IsConnected { get; private set; }

  public void Open() {
    OpenCalls++;
    if (ThrowOnOpen is not null) {
      throw ThrowOnOpen;
    }

    IsConnected = true;
  }

  public Task OpenAsync(CancellationToken ct = default) {
    OpenCalls++;
    if (ThrowOnOpenAsync is not null) {
      return Task.FromException(ThrowOnOpenAsync);
    }

    IsConnected = true;
    return Task.CompletedTask;
  }

  public void Write(byte[] buffer) => Writes.Add(buffer);

  public Task WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default) {
    Writes.Add(buffer.ToArray());
    return Task.CompletedTask;
  }

  public byte[] Read() => ReadBuffers.Count > 0 ? ReadBuffers.Dequeue() : Array.Empty<byte>();

  public byte[] Read(InquireHandler inquireHandler) {
    LastInquireHandler = inquireHandler;
    return Read();
  }

  public ValueTask<ReadOnlyMemory<byte>> ReadAsync(CancellationToken ct = default)
    => new(ReadMemoryBuffers.Count > 0 ? ReadMemoryBuffers.Dequeue() : Read().AsMemory());

  public ValueTask<ReadOnlyMemory<byte>> ReadAsync(AsyncInquireHandler inquireHandler, CancellationToken ct = default) {
    LastAsyncInquireHandler = inquireHandler;
    return ReadAsync(ct);
  }

  public byte[] ReadAvailable() => Read();

  ValueTask<ReadOnlyMemory<byte>> IAssuanConnection.ReadAvailableAsync(CancellationToken ct)
    => ReadAsync(ct);

  public void DiscardPendingInput() {
    ReadBuffers.Clear();
    ReadMemoryBuffers.Clear();
  }

  public ValueTask DiscardPendingInputAsync(CancellationToken ct = default) {
    DiscardPendingInput();
    return ValueTask.CompletedTask;
  }

  public void Close() {
    CloseCalls++;
    IsConnected = false;
  }

  public Task CloseAsync(CancellationToken ct = default) {
    Close();
    return Task.CompletedTask;
  }

  public void Dispose() {
    DisposeCalls++;
    IsConnected = false;
  }

  public ValueTask DisposeAsync() {
    DisposeAsyncCalls++;
    IsConnected = false;
    return ValueTask.CompletedTask;
  }
}
