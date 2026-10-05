// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Net;
using System.Security.Cryptography;
using System.Text;
using AssuanLibrary.Protocol;
using AssuanLibrary.Protocol.State;
using AssuanLibrary.Transport;
using AssuanLibrary.Transport.Discovery;
using AssuanLibrary.Transport.Unix;
using AssuanLibrary.Transport.Windows;

namespace AssuanLibrary.Client;

/// <summary>Owns one serial Assuan client connection and its borrowed response transactions.</summary>
public sealed class AssuanClient : IAsyncDisposable {
  private readonly ClientCore _core;
  private readonly ClientOptions _options;

  private AssuanClient(ClientCore core, ClientOptions options) {
    _core = core;
    _options = options;
  }

  /// <summary>Whether local state permits another command; not a peer liveness check.</summary>
  public bool IsUsable => _core.IsUsable && Volatile.Read(ref _core.TransactionActive) == 0;

  /// <summary>Closes the connection; no pending transaction remains reusable.</summary>
  public ValueTask DisposeAsync() {
    _core.Invalidate();
    return ValueTask.CompletedTask;
  }

  /// <summary>Takes ownership of a connected duplex stream and consumes a noninteractive greeting.</summary>
  public static Task<AssuanClient> FromStreamAsync(Stream stream, ClientOptions? options = null,
  CancellationToken cancellationToken = default)
    => FromStreamWithClockAsync(stream, options, TimeProvider.System, cancellationToken);

  // Deterministic tests advance only client-owned total budgets; channel I/O still
  // enforces its real per-call deadline using the remaining amount passed to it.
  internal static async Task<AssuanClient> FromStreamWithClockAsync(Stream stream, ClientOptions? options,
  TimeProvider clock, CancellationToken cancellationToken) {
    ArgumentNullException.ThrowIfNull(stream);
    ArgumentNullException.ThrowIfNull(clock);
    options ??= ClientOptions.Default;
    var core = new ClientCore(stream, clock);
    var transferred = false;
    try {
      var deadline = new TransportDeadline(options.GreetingTimeout, clock);
      if (cancellationToken.IsCancellationRequested) {
        throw new ClientException(ClientErrorKind.Cancelled);
      }

      while (true) {
        await core.Channel.ReadLineAsync(deadline.Remaining, cancellationToken).ConfigureAwait(false);
        ProtocolResponseKind kind;
        uint? errorCode = null;

        {
          var response = ProtocolResponse.Parse(core.Channel.Line);
          kind = response.Kind;
          if (kind == ProtocolResponseKind.Err) {
            errorCode = response.ErrorCode;
          }
        }

        switch (kind) {
          case ProtocolResponseKind.Inquire:
            throw new ClientException(ClientErrorKind.UnsupportedInquiry);
          case ProtocolResponseKind.Err:
            throw new ClientException(ClientErrorKind.GreetingRejected, errorCode);
        }

        core.Machine.Receive(kind);
        if (deadline.Remaining == TimeSpan.Zero) {
          throw new ClientException(ClientErrorKind.Timeout);
        }

        if (kind != ProtocolResponseKind.Ok) {
          continue;
        }

        transferred = true;
        return new AssuanClient(core, options);
      }
    }
    catch (TransportException error) { throw ClientCore.Classify(error, cancellationToken); }
    catch (ProtocolException error) { throw new ClientException(ClientErrorKind.Protocol, technicalCause: error); }
    catch (StateException error) { throw new ClientException(ClientErrorKind.State, technicalCause: error); }
    catch (OperationCanceledException error) {
      throw new ClientException(cancellationToken.IsCancellationRequested
        ? ClientErrorKind.Cancelled
        : ClientErrorKind.Timeout, technicalCause: error);
    }
    finally {
      if (!transferred) {
        core.Invalidate();
      }
    }
  }

  /// <summary>Connects to a chosen TCP address and consumes the greeting.</summary>
  public static Task<AssuanClient> ConnectAsync(IPEndPoint endpoint, ClientOptions? options = null,
  CancellationToken cancellationToken = default) {
    ArgumentNullException.ThrowIfNull(endpoint);

    return ConnectViaAsync((timeout, token) => TcpConnector.ConnectAsync(endpoint, timeout, token),
      options ?? ClientOptions.Default, cancellationToken);
  }

  /// <summary>Connects to an explicit Unix socket or Windows named pipe.</summary>
  public static Task<AssuanClient> ConnectAsync(LocalEndpoint endpoint, ClientOptions? options = null,
  CancellationToken cancellationToken = default) {
    ArgumentNullException.ThrowIfNull(endpoint);

    return ConnectViaAsync((timeout, token) => endpoint.Kind switch {
      LocalEndpointKind.UnixSocket => UnixConnector.ConnectAsync(endpoint, timeout, token),
      LocalEndpointKind.NamedPipe => PipeConnector.ConnectAsync(endpoint, timeout, token),
      var _ => throw new ClientException(ClientErrorKind.InvalidOptions)
    }, options ?? ClientOptions.Default, cancellationToken);
  }

  /// <summary>Uses the resolved endpoint's own connection method, including a Windows nonce handshake.</summary>
  public static Task<AssuanClient> ConnectAsync(ResolvedAgentEndpoint endpoint, ClientOptions? options = null,
  CancellationToken cancellationToken = default) {
    ArgumentNullException.ThrowIfNull(endpoint);

    return ConnectViaAsync((timeout, token) => endpoint.ConnectAsync(timeout, token),
      options ?? ClientOptions.Default, cancellationToken);
  }

  /// <summary>Starts one validated command; its transaction owns all reads until a final response.</summary>
  public async Task<ClientTransaction> CommandAsync(string name, ReadOnlyMemory<byte> arguments,
  PayloadSensitivity sensitivity = PayloadSensitivity.Public, CancellationToken cancellationToken = default) {
    if (Volatile.Read(ref _core.TransactionActive) != 0 ||
        !_core.IsUsable) {
      throw new ClientException(ClientErrorKind.State);
    }

    if (!Enum.IsDefined(sensitivity)) {
      throw new ClientException(ClientErrorKind.InvalidOptions);
    }

    var deadline = new TransportDeadline(_options.CommandTimeout, _core.Clock);
    var output = new byte[1000];
    int written;
    try {
      ArgumentNullException.ThrowIfNull(name);

      if (name.Length is 0 or >= 1000 ||
          name.Any(character => character > 127)) {
        throw new ProtocolException(ProtocolErrorKind.InvalidToken);
      }

      var command = ProtocolCommand.Create(Encoding.ASCII.GetBytes(name), arguments.Span);
      written = ProtocolEncoder.EncodeCommand(command, output);
    }
    catch (Exception error) when (error is ProtocolException or ArgumentException) {
      throw new ClientException(ClientErrorKind.Protocol);
    }

    try {
      if (_core.Channel.HasBufferedInput) {
        _core.Invalidate();
        throw new ClientException(ClientErrorKind.Protocol);
      }

      if (Interlocked.CompareExchange(ref _core.TransactionActive, 1, 0) != 0) {
        throw new ClientException(ClientErrorKind.State);
      }

      _core.Machine.BeginCommand();
      _core.IoUncertain = true;
      await _core.Channel.WriteLineAsync(output.AsMemory(0, written), deadline.Remaining, cancellationToken)
        .ConfigureAwait(false);
      if (deadline.Remaining == TimeSpan.Zero) {
        throw new ClientException(ClientErrorKind.Timeout);
      }

      _core.IoUncertain = false;
      return new ClientTransaction(_core, deadline, sensitivity);
    }
    catch (Exception error) when (error is not ClientException { Kind: ClientErrorKind.State }) {
      _core.Invalidate();
      Interlocked.Exchange(ref _core.TransactionActive, 0);
      throw ClientCore.Classify(error, cancellationToken);
    }
    finally { CryptographicOperations.ZeroMemory(output); }
  }

  /// <summary>Explicitly collects bounded public data and copied status/comment metadata.</summary>
  public async Task<CollectedResponse> CollectAsync(string name, ReadOnlyMemory<byte> arguments,
  CollectLimits? limits = null, CancellationToken cancellationToken = default) {
    var collected = await CollectCoreAsync(name, arguments, limits ?? CollectLimits.Default, false,
      cancellationToken).ConfigureAwait(false);
    return new CollectedResponse(collected.Data, collected.Statuses, collected.Comments);
  }

  /// <summary>Explicitly collects bounded secret data in fixed protected storage.</summary>
  public async Task<SecretCollectedResponse> CollectSecretAsync(string name, ReadOnlyMemory<byte> arguments,
  CollectLimits? limits = null, CancellationToken cancellationToken = default) {
    var collected = await CollectCoreAsync(name, arguments, limits ?? CollectLimits.Default, true,
      cancellationToken).ConfigureAwait(false);
    return new SecretCollectedResponse(collected.Secret!, collected.Statuses, collected.Comments);
  }

  private async Task<(byte[] Data, SecretBuffer? Secret, List<CollectedStatus> Statuses, List<byte[]> Comments)>
    CollectCoreAsync(string name, ReadOnlyMemory<byte> arguments, CollectLimits limits, bool secret,
    CancellationToken token) {
    var protectedData = secret ? new SecretBuffer(limits.MaxBytes) : null;
    using var publicData = new MemoryStream();
    var statuses = new List<CollectedStatus>();
    var comments = new List<byte[]>();
    var retained = 0;
    var count = 0;
    var transferred = false;
    try {
      await using var transaction = await CommandAsync(name, arguments,
        secret ? PayloadSensitivity.Secret : PayloadSensitivity.Public, token).ConfigureAwait(false);
      while (await transaction.ReadNextAsync(token).ConfigureAwait(false) is { } current) {
        using (current) {
          if (++count > limits.MaxEvents) {
            _core.Invalidate();
            throw new ClientException(ClientErrorKind.Limit);
          }

          switch (current.Kind) {
            case ClientEventKind.Data: {
              var data = secret ? current.ExposeSecret() : current.PayloadBytes;
              CheckBudget(data.Length);
              if (secret) {
                protectedData!.Append(data.Span);
              }
              else {
                publicData.Write(data.Span);
              }

              break;
            }
            case ClientEventKind.Status: {
              var keyword = current.KeywordBytes;
              var data = secret ? current.ExposeSecret() : current.PayloadBytes;
              CheckBudget(checked(keyword.Length + 1 + data.Length));
              statuses.Add(new CollectedStatus(keyword.ToArray(), data.ToArray()));
              break;
            }
            case ClientEventKind.Comment: {
              var data = secret ? current.ExposeSecret() : current.PayloadBytes;
              CheckBudget(data.Length);
              comments.Add(data.ToArray());
              break;
            }
          }
        }
      }

      transaction.Finish();
      transferred = true;
      return (secret ? [] : publicData.ToArray(), protectedData, statuses, comments);
    }
    finally {
      if (!transferred) {
        protectedData?.Dispose();
      }
    }

    void CheckBudget(int bytes) {
      if (bytes > (limits.MaxBytes - retained)) {
        _core.Invalidate();
        throw new ClientException(ClientErrorKind.Limit);
      }

      retained += bytes;
    }
  }

  private static async Task<AssuanClient> ConnectViaAsync(Func<TimeSpan, CancellationToken, Task<Stream>> connect,
  ClientOptions options, CancellationToken cancellationToken) {
    try {
      if (cancellationToken.IsCancellationRequested) {
        throw new ClientException(ClientErrorKind.Cancelled);
      }

      var stream = await connect(options.ConnectTimeout, cancellationToken).ConfigureAwait(false);
      return await FromStreamAsync(stream, options, cancellationToken).ConfigureAwait(false);
    }
    catch (Exception error) when (error is TransportException or OperationCanceledException or TimeoutException) {
      throw ClientCore.Classify(error, cancellationToken);
    }
  }
}
