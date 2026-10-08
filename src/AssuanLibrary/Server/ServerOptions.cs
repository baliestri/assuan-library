// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Diagnostics;
using AssuanLibrary.Transport;

namespace AssuanLibrary.Server;

/// <summary>Positive, bounded deadlines and payload limits for one server session.</summary>
public sealed class ServerOptions {
  /// <summary>Validates all limits before a session writes to the connection.</summary>
  public ServerOptions(TimeSpan? greetingTimeout = null, TimeSpan? commandTimeout = null,
  TimeSpan? inquiryTimeout = null, TimeSpan? idleTimeout = null, TimeSpan? closedTimeout = null,
  int maxInquiryBytes = 1024 * 1024, int maxSessions = 128, TimeSpan? shutdownGracePeriod = null,
  IAssuanEventSink? eventSink = null) {
    GreetingTimeout = Check(greetingTimeout ?? TimeSpan.FromSeconds(120));
    CommandTimeout = Check(commandTimeout ?? TimeSpan.FromSeconds(300));
    InquiryTimeout = Check(inquiryTimeout ?? TimeSpan.FromSeconds(120));
    IdleTimeout = Check(idleTimeout ?? TimeSpan.FromSeconds(300));
    ClosedTimeout = Check(closedTimeout ?? TimeSpan.FromSeconds(30));
    if (maxInquiryBytes <= 0) {
      throw new ServerException(ServerErrorKind.InvalidOptions);
    }

    MaxInquiryBytes = maxInquiryBytes;
    if (maxSessions <= 0) {
      throw new ServerException(ServerErrorKind.InvalidOptions);
    }

    MaxSessions = maxSessions;
    ShutdownGracePeriod = Check(shutdownGracePeriod ?? TimeSpan.FromSeconds(30));
    EventSink = eventSink;
  }

  /// <summary>The standard per-session limits.</summary>
  public static ServerOptions Default { get; } = new();

  /// <summary>Budget shared by authentication and the greeting.</summary>
  public TimeSpan GreetingTimeout { get; }

  /// <summary>Budget shared by one command, its handler and final response.</summary>
  public TimeSpan CommandTimeout { get; }

  /// <summary>Inquiry budget, capped by the enclosing command.</summary>
  public TimeSpan InquiryTimeout { get; }

  /// <summary>Budget between commands, including comments and partial lines.</summary>
  public TimeSpan IdleTimeout { get; }

  /// <summary>Maximum time for the close hook after the session ends.</summary>
  public TimeSpan ClosedTimeout { get; }

  /// <summary>Maximum decoded payload bytes in one inquiry.</summary>
  public int MaxInquiryBytes { get; }

  /// <summary>Maximum admitted sessions, including authentication and close hooks.</summary>
  public int MaxSessions { get; }

  /// <summary>One shared grace period after the host stops accepting connections.</summary>
  public TimeSpan ShutdownGracePeriod { get; }

  /// <summary>Optional payload-free diagnostic observer; null avoids event creation.</summary>
  public IAssuanEventSink? EventSink { get; }

  private static TimeSpan Check(TimeSpan timeout) {
    try { _ = new TransportDeadline(timeout); }
    catch (TransportException) { throw new ServerException(ServerErrorKind.InvalidOptions); }

    return timeout;
  }
}
