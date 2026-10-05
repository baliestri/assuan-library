// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Client;

/// <summary>Answers a greeting inquiry, completing it with END or CAN before returning.</summary>
public interface IGreetingInquiryHandler {
  /// <summary>Receives exclusive ownership of one inquiry for the callback's lifetime.</summary>
  ValueTask RespondAsync(ClientInquiry inquiry, CancellationToken cancellationToken);
}
