// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Server.Abstractions;

namespace AssuanLibrary.Tests.Server.Fakes;

internal sealed class FakeServerInquireContext : IServerInquireContext {
  public FakeServerInquireContext(string keyword, IReadOnlyCollection<string> parameters) {
    Keyword = keyword;
    Parameters = parameters;
  }

  public List<byte[]> Received { get; } = new();

  public int CompleteCalls { get; private set; }
  public int CancelCalls { get; private set; }

  public string Keyword { get; }
  public IReadOnlyCollection<string> Parameters { get; }

  public void Receive(ReadOnlySpan<byte> buffer) => Received.Add(buffer.ToArray());

  public void Complete() => CompleteCalls++;

  public void Cancel() => CancelCalls++;

  public byte[] Wait() => Array.Empty<byte>();

  public ValueTask<ReadOnlyMemory<byte>> WaitAsync(CancellationToken ct = default)
    => ValueTask.FromResult(ReadOnlyMemory<byte>.Empty);

  public void Dispose() { }
}
