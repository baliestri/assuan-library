// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Text;

namespace AssuanLibrary.Protocol;

public sealed partial class AssuanResponse {
  /// <summary>
  ///   Creates an <c>OK</c> response with the specified buffer.
  /// </summary>
  /// <param name="buffer">The response buffer.</param>
  /// <returns>A new instance of <see cref="AssuanResponse" /> representing an <c>OK</c> response.</returns>
  public static AssuanResponse Ok(byte[] buffer) {
    var referenceBuffer = buffer;
    if (!AssuanEncoder.IsEncoded(buffer)) {
      referenceBuffer = AssuanEncoder.AsBytes(buffer);
    }

    return new AssuanResponse(AssuanResponseType.Ok, referenceBuffer);
  }

  /// <summary>
  ///   Creates an <c>OK</c> response with the specified message.
  /// </summary>
  /// <param name="message">The response message.</param>
  /// <returns>A new instance of <see cref="AssuanResponse" /> representing an <c>OK</c> response.</returns>
  public static AssuanResponse Ok(string message) {
    var referenceBuffer = Encoding.UTF8.GetBytes(message);
    if (!AssuanEncoder.IsEncoded(message)) {
      referenceBuffer = AssuanEncoder.AsBytes(message);
    }

    return new AssuanResponse(AssuanResponseType.Ok, referenceBuffer);
  }

  /// <summary>
  ///   Creates an <c>OK</c> response with an empty buffer.
  /// </summary>
  /// <returns>A new instance of <see cref="AssuanResponse" /> representing an <c>OK</c> response.</returns>
  public static AssuanResponse Ok()
    => Ok([]);

  /// <summary>
  ///   Creates an <c>Error</c> response with the specified buffer.
  /// </summary>
  /// <param name="buffer">The response buffer.</param>
  /// <returns>A new instance of <see cref="AssuanResponse" /> representing an <c>Error</c> response.</returns>
  public static AssuanResponse Error(byte[] buffer) {
    var referenceBuffer = buffer;
    if (!AssuanEncoder.IsEncoded(buffer)) {
      referenceBuffer = AssuanEncoder.AsBytes(buffer);
    }

    return new AssuanResponse(AssuanResponseType.Error, referenceBuffer);
  }

  /// <summary>
  ///   Creates an <c>Error</c> response with the specified code and message.
  /// </summary>
  /// <param name="code">The error code.</param>
  /// <param name="message">The error message.</param>
  /// <returns>A new instance of <see cref="AssuanResponse" /> representing an <c>Error</c> response.</returns>
  public static AssuanResponse Error(int code, string message) {
    var fullMessage = $"{code} {message}";
    var referenceBuffer = Encoding.UTF8.GetBytes(fullMessage);
    if (!AssuanEncoder.IsEncoded(fullMessage)) {
      referenceBuffer = AssuanEncoder.AsBytes(fullMessage);
    }

    return new AssuanResponse(AssuanResponseType.Ok, referenceBuffer);
  }

  /// <summary>
  ///   Creates a <c>Status</c> response with the specified buffer.
  /// </summary>
  /// <param name="buffer">The response buffer.</param>
  /// <returns>A new instance of <see cref="AssuanResponse" /> representing a <c>Status</c> response.</returns>
  public static AssuanResponse Status(byte[] buffer) {
    var referenceBuffer = buffer;
    if (!AssuanEncoder.IsEncoded(buffer)) {
      referenceBuffer = AssuanEncoder.AsBytes(buffer);
    }

    return new AssuanResponse(AssuanResponseType.Status, referenceBuffer);
  }

  /// <summary>
  ///   Creates a <c>Status</c> response with the specified message.
  /// </summary>
  /// <param name="message">The response message.</param>
  /// <returns>A new instance of <see cref="AssuanResponse" /> representing a <c>Status</c> response.</returns>
  public static AssuanResponse Status(string message) {
    var referenceBuffer = Encoding.UTF8.GetBytes(message);
    if (!AssuanEncoder.IsEncoded(message)) {
      referenceBuffer = AssuanEncoder.AsBytes(message);
    }

    return new AssuanResponse(AssuanResponseType.Status, referenceBuffer);
  }

  /// <summary>
  ///   Creates a <c>Comment</c> response with the specified buffer.
  /// </summary>
  /// <param name="buffer">The response buffer.</param>
  /// <returns>A new instance of <see cref="AssuanResponse" /> representing a <c>Comment</c> response.</returns>
  public static AssuanResponse Comment(byte[] buffer) {
    var referenceBuffer = buffer;
    if (!AssuanEncoder.IsEncoded(buffer)) {
      referenceBuffer = AssuanEncoder.AsBytes(buffer);
    }

    return new AssuanResponse(AssuanResponseType.Comment, referenceBuffer);
  }

  /// <summary>
  ///   Creates a <c>Comment</c> response with the specified message.
  /// </summary>
  /// <param name="message">The response message.</param>
  /// <returns>A new instance of <see cref="AssuanResponse" /> representing a <c>Comment</c> response.</returns>
  public static AssuanResponse Comment(string message) {
    var referenceBuffer = Encoding.UTF8.GetBytes(message);
    if (!AssuanEncoder.IsEncoded(message)) {
      referenceBuffer = AssuanEncoder.AsBytes(message);
    }

    return new AssuanResponse(AssuanResponseType.Status, referenceBuffer);
  }

  /// <summary>
  ///   Creates a <c>Comment</c> response with an empty buffer.
  /// </summary>
  /// <returns>A new instance of <see cref="AssuanResponse" /> representing a <c>Comment</c> response.</returns>
  public static AssuanResponse Comment()
    => Comment([]);

  /// <summary>
  ///   Creates a <c>Data</c> response with the specified buffer.
  /// </summary>
  /// <param name="buffer">The response buffer.</param>
  /// <returns>A new instance of <see cref="AssuanResponse" /> representing a <c>Data</c> response.</returns>
  public static AssuanResponse Data(byte[] buffer) {
    var referenceBuffer = buffer;
    if (!AssuanEncoder.IsEncoded(buffer)) {
      referenceBuffer = AssuanEncoder.AsBytes(buffer);
    }

    return new AssuanResponse(AssuanResponseType.Data, referenceBuffer);
  }

  /// <summary>
  ///   Creates a <c>Data</c> response with the specified message.
  /// </summary>
  /// <param name="message">The response message.</param>
  /// <returns>A new instance of <see cref="AssuanResponse" /> representing a <c>Data</c> response.</returns>
  public static AssuanResponse Data(string message) {
    var referenceBuffer = Encoding.UTF8.GetBytes(message);
    if (!AssuanEncoder.IsEncoded(message)) {
      referenceBuffer = AssuanEncoder.AsBytes(message);
    }

    return new AssuanResponse(AssuanResponseType.Data, referenceBuffer);
  }

  /// <summary>
  ///   Creates an <c>Inquire</c> response with the specified buffer.
  /// </summary>
  /// <param name="buffer">The response buffer.</param>
  /// <returns>A new instance of <see cref="AssuanResponse" /> representing an <c>Inquire</c> response.</returns>
  public static AssuanResponse Inquire(byte[] buffer) {
    var referenceBuffer = buffer;
    if (!AssuanEncoder.IsEncoded(buffer)) {
      referenceBuffer = AssuanEncoder.AsBytes(buffer);
    }

    return new AssuanResponse(AssuanResponseType.Data, referenceBuffer);
  }

  /// <summary>
  ///   Creates an <c>Inquire</c> response with the specified keyword and parameters.
  /// </summary>
  /// <param name="keyword">The inquire keyword.</param>
  /// <param name="parameters">The inquire parameters.</param>
  /// <returns>A new instance of <see cref="AssuanResponse" /> representing an <c>Inquire</c> response.</returns>
  public static AssuanResponse Inquire(string keyword, params string[] parameters) {
    var builder = new StringBuilder();
    builder.Append(keyword);
    foreach (var param in parameters) {
      builder.Append(' ');
      builder.Append(param);
    }

    return Inquire(AssuanEncoder.AsBytes(builder.ToString()));
  }
}
