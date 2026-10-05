// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Security.Cryptography;
using System.Text;
using AssuanLibrary.Protocol;
using AssuanLibrary.Protocol.State;
using AssuanLibrary.Transport;

namespace AssuanLibrary.Server;

/// <summary>Session-owned state and nonfinal output during one command invocation.</summary>
/// <remarks>Borrowed arguments cease to be valid after the current handler or hook returns.</remarks>
public sealed class CommandContext<TState> {
  private readonly AssuanChannel _channel;
  private readonly TransportDeadline _deadline;
  private readonly TimeSpan _inquiryTimeout;
  private readonly ServerMachine _machine;
  private readonly int _maxInquiryBytes;
  private readonly byte[] _scratch = new byte[1000];
  private int _active;
  private int _closed;
  private int _inquiryActive;

  internal CommandContext(TState state, ReadOnlyMemory<byte> arguments, PeerIdentity? peerIdentity,
  bool isAuthenticated, AssuanChannel channel, ServerMachine machine, TransportDeadline deadline,
  int maxInquiryBytes = 1024 * 1024, TimeSpan? inquiryTimeout = null) {
    State = state;
    Arguments = arguments;
    PeerIdentity = peerIdentity;
    IsAuthenticated = isAuthenticated;
    _channel = channel;
    _machine = machine;
    _deadline = deadline;
    _inquiryTimeout = inquiryTimeout ?? TimeSpan.FromSeconds(120);
    _maxInquiryBytes = maxInquiryBytes;
  }

  /// <summary>Borrowed wire arguments, stable across awaits only for the current invocation.</summary>
  public ReadOnlyMemory<byte> Arguments { get; }

  /// <summary>The application state of this session alone.</summary>
  public TState State { get; set; }

  /// <summary>Verified OS peer identity, if supplied by the transport.</summary>
  public PeerIdentity? PeerIdentity { get; }

  /// <summary>Whether the library's authentication hook accepted this connection.</summary>
  public bool IsAuthenticated { get; }

  internal bool IsClosed => Volatile.Read(ref _closed) != 0;

  /// <summary>Streams raw binary data in bounded, escaped D lines without a final response.</summary>
  public async ValueTask SendDataAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default) {
    Begin();
    try {
      do {
        var (consumed, written) = ProtocolEncoder.EncodeDataChunk(data.Span, _scratch);
        await WriteAsync(written, ProtocolResponseKind.Data, cancellationToken).ConfigureAwait(false);
        data = data[consumed..];
      } while (!data.IsEmpty);
    }
    finally { End(); }
  }

  /// <summary>Emits an informational status line, leaving the final response to the runner.</summary>
  public async ValueTask SendStatusAsync(string keyword, ReadOnlyMemory<byte> arguments,
  CancellationToken cancellationToken = default) {
    Begin();
    try {
      ArgumentNullException.ThrowIfNull(keyword);

      if (keyword.Any(character => character > 127)) {
        throw new ProtocolException(ProtocolErrorKind.InvalidToken);
      }

      var bytes = Encoding.ASCII.GetBytes(keyword);
      var written = ProtocolEncoder.EncodeResponse(ProtocolResponse.Status(bytes, arguments.Span), _scratch);

      await WriteAsync(written, ProtocolResponseKind.Status, cancellationToken).ConfigureAwait(false);
    }
    finally { End(); }
  }

  internal async ValueTask SendCommentAsync(ReadOnlyMemory<byte> body, CancellationToken cancellationToken) {
    Begin();
    try {
      ProtocolFields.ValidateLine(body.Span);
      if (body.Length > 997) {
        throw new ProtocolException(ProtocolErrorKind.LineTooLong);
      }

      _scratch[0] = (byte)'#';
      _scratch[1] = (byte)' ';
      body.CopyTo(_scratch.AsMemory(2));
      _scratch[body.Length + 2] = (byte)'\n';

      await WriteAsync(body.Length + 3, ProtocolResponseKind.Comment, cancellationToken).ConfigureAwait(false);
    }
    finally { End(); }
  }

  /// <summary>Begins exclusive, bounded reading of client data until END or CAN and Finish.</summary>
  public async Task<ServerInquiry> BeginInquiryAsync(string keyword, ReadOnlyMemory<byte> arguments,
  PayloadSensitivity sensitivity, CancellationToken cancellationToken = default) {
    if (!Enum.IsDefined(sensitivity)) {
      throw new ArgumentOutOfRangeException(nameof(sensitivity));
    }

    Begin();
    try {
      ArgumentNullException.ThrowIfNull(keyword);

      if (keyword.Any(character => character > 127)) {
        throw new ProtocolException(ProtocolErrorKind.InvalidToken);
      }

      var name = Encoding.ASCII.GetBytes(keyword);
      var written = ProtocolEncoder.EncodeResponse(ProtocolResponse.Inquire(name, arguments.Span), _scratch);

      await WriteAsync(written, ProtocolResponseKind.Inquire, cancellationToken).ConfigureAwait(false);
      var budget = TimeSpan.FromTicks(Math.Min(_deadline.Remaining.Ticks, _inquiryTimeout.Ticks));
      if (budget == TimeSpan.Zero) {
        throw new TransportException(TransportErrorKind.Timeout);
      }

      var inquiry = new ServerInquiry(_channel, _machine, new TransportDeadline(budget),
        _maxInquiryBytes, sensitivity, ReleaseInquiry, Abort);
      Volatile.Write(ref _inquiryActive, 1);
      return inquiry;
    }
    catch {
      Abort();
      throw;
    }
    finally { End(); }
  }

  internal void Close() {
    Volatile.Write(ref _closed, 1);
    if (Volatile.Read(ref _active) == 0) {
      CryptographicOperations.ZeroMemory(_scratch);
    }
  }

  internal void Abort() {
    Volatile.Write(ref _closed, 1);
    _machine.Invalidate();
    _channel.Close();
  }

  private void ReleaseInquiry()
    => Volatile.Write(ref _inquiryActive, 0);

  private void Begin() {
    if (IsClosed) {
      throw new ServerException(ServerErrorKind.Closed);
    }

    if (Volatile.Read(ref _inquiryActive) != 0) {
      throw new InvalidOperationException("Finish the active inquiry before another output operation.");
    }

    if (Interlocked.CompareExchange(ref _active, 1, 0) != 0) {
      throw new InvalidOperationException("Another context operation is in progress.");
    }

    if (!IsClosed) {
      return;
    }

    End();
    throw new ServerException(ServerErrorKind.Closed);
  }

  private void End() {
    CryptographicOperations.ZeroMemory(_scratch);
    Interlocked.Exchange(ref _active, 0);
  }

  private async ValueTask WriteAsync(int count, ProtocolResponseKind kind, CancellationToken cancellationToken) {
    try {
      _machine.SendResponse(kind);
      if (_deadline.Remaining == TimeSpan.Zero) {
        throw new TransportException(TransportErrorKind.Timeout);
      }

      await _channel.WriteLineAsync(_scratch.AsMemory(0, count), _deadline.Remaining, cancellationToken)
        .ConfigureAwait(false);
      if (_deadline.Remaining == TimeSpan.Zero) {
        throw new TransportException(TransportErrorKind.Timeout);
      }
    }
    catch {
      Volatile.Write(ref _closed, 1);
      _machine.Invalidate();
      _channel.Close();
      throw;
    }
  }
}
