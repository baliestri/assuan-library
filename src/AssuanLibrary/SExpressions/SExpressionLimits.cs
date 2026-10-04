// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.SExpressions;

/// <summary>Validated resource limits for canonical binary S-expressions.</summary>
public sealed class SExpressionLimits {
  /// <summary>The greatest configurable number of nested lists.</summary>
  public const int MAX_NESTING_DEPTH = 256;

  /// <summary>Creates limits for parsing; zero atom size and zero list depth are permitted.</summary>
  /// <exception cref="SExpressionParseException">The limits are inconsistent or unsupported.</exception>
  public SExpressionLimits(int maxInputBytes, int maxAtomBytes, int maxNodes, int maxDepth) {
    if (maxInputBytes <= 0 ||
        maxNodes <= 0 ||
        maxAtomBytes < 0 ||
        maxAtomBytes > maxInputBytes ||
        maxDepth is < 0 or > MAX_NESTING_DEPTH) {
      throw new SExpressionParseException(SExpressionParseErrorKind.InvalidLimits);
    }

    MaxInputBytes = maxInputBytes;
    MaxAtomBytes = maxAtomBytes;
    MaxNodes = maxNodes;
    MaxDepth = maxDepth;
  }

  /// <summary>Default limits: 16 MiB of input and atom data, 65,536 nodes, and 64 nested lists.</summary>
  public static SExpressionLimits Default { get; } = new(16 * 1024 * 1024, 16 * 1024 * 1024, 65_536, 64);

  /// <summary>Maximum number of bytes in the entire supplied input, including any unconsumed suffix.</summary>
  public int MaxInputBytes { get; }

  /// <summary>Maximum number of payload bytes in one atom.</summary>
  public int MaxAtomBytes { get; }

  /// <summary>Maximum combined number of list and atom nodes.</summary>
  public int MaxNodes { get; }

  /// <summary>Maximum number of lists containing a value; a top-level atom has depth zero.</summary>
  public int MaxDepth { get; }
}
