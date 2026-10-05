// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Transport;

namespace AssuanLibrary.Client;

/// <summary>Validated total operation budgets for a client connection.</summary>
public sealed class ClientOptions {
  /// <summary>Rejects zero, negative or unrepresentable budgets before I/O.</summary>
  public ClientOptions(TimeSpan? connectTimeout = null, TimeSpan? greetingTimeout = null,
  TimeSpan? commandTimeout = null, TimeSpan? inquiryTimeout = null, int maxInquiryBytes = 1024 * 1024) {
    ConnectTimeout = Validate(connectTimeout ?? TimeSpan.FromSeconds(10));
    GreetingTimeout = Validate(greetingTimeout ?? TimeSpan.FromSeconds(120));
    CommandTimeout = Validate(commandTimeout ?? TimeSpan.FromSeconds(300));
    InquiryTimeout = Validate(inquiryTimeout ?? TimeSpan.FromSeconds(120));
    if (maxInquiryBytes <= 0) {
      throw new ClientException(ClientErrorKind.InvalidOptions);
    }

    MaxInquiryBytes = maxInquiryBytes;
  }

  /// <summary>Default connect, greeting, command and reserved inquiry budgets.</summary>
  public static ClientOptions Default { get; } = new();

  /// <summary>Transport connection budget; defaults to ten seconds.</summary>
  public TimeSpan ConnectTimeout { get; }

  /// <summary>Total greeting budget; defaults to 120 seconds.</summary>
  public TimeSpan GreetingTimeout { get; }

  /// <summary>Total command budget including all events and final; defaults to 300 seconds.</summary>
  public TimeSpan CommandTimeout { get; }

  /// <summary>Inquiry budget, capped by the greeting or command budget; defaults to 120 seconds.</summary>
  public TimeSpan InquiryTimeout { get; }

  /// <summary>Maximum cumulative raw bytes sent during one inquiry; defaults to one MiB.</summary>
  public int MaxInquiryBytes { get; }

  private static TimeSpan Validate(TimeSpan value) {
    try { _ = new TransportDeadline(value); }
    catch (TransportException) { throw new ClientException(ClientErrorKind.InvalidOptions); }

    return value;
  }
}
