// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Runtime.InteropServices;

namespace AssuanLibrary.SExpressions;

/// <summary>Writes bounded canonical binary S-expressions without intermediate text encoding.</summary>
public static class SExpressionWriter {
  /// <summary>Copies a validated view into a newly allocated canonical byte array.</summary>
  /// <exception cref="SExpressionEncodeException">The value exceeds default parsing limits.</exception>
  public static byte[] ToCanonical(SExpressionView value) {
    var source = value.CanonicalBytes;
    MeasureView(source);

    return source.ToArray();
  }

  /// <summary>Encodes an independently owned expression under default parsing limits.</summary>
  /// <exception cref="SExpressionEncodeException">The expression exceeds default parsing limits.</exception>
  public static byte[] ToCanonical(SExpression value) {
    ArgumentNullException.ThrowIfNull(value);
    var layout = MeasureOwned(value);
    var pending = new Stack<(SExpression? Node, bool Close)>(layout.PendingItems);
    var result = new byte[layout.Bytes];
    WriteOwned(value, result, pending);

    return result;
  }

  /// <summary>Appends a view's canonical bytes, leaving existing output unchanged on a limit failure.</summary>
  /// <exception cref="SExpressionEncodeException">The output would exceed default parsing limits.</exception>
  public static void AppendCanonical(SExpressionView value, List<byte> destination) {
    ArgumentNullException.ThrowIfNull(destination);

    var source = value.CanonicalBytes;
    var length = MeasureView(source);

    CheckDestination(destination.Count, length);
    var previous = destination.Count;

    destination.EnsureCapacity(previous + length);
    CollectionsMarshal.SetCount(destination, previous + length);
    source.CopyTo(CollectionsMarshal.AsSpan(destination)[previous..]);
  }

  /// <summary>Appends an owned expression, leaving existing output unchanged on a limit failure.</summary>
  /// <exception cref="SExpressionEncodeException">The output would exceed default parsing limits.</exception>
  public static void AppendCanonical(SExpression value, List<byte> destination) {
    ArgumentNullException.ThrowIfNull(value);
    ArgumentNullException.ThrowIfNull(destination);

    var layout = MeasureOwned(value);
    CheckDestination(destination.Count, layout.Bytes);

    var pending = new Stack<(SExpression? Node, bool Close)>(layout.PendingItems);
    var previous = destination.Count;

    destination.EnsureCapacity(previous + layout.Bytes);
    CollectionsMarshal.SetCount(destination, previous + layout.Bytes);
    WriteOwned(value, CollectionsMarshal.AsSpan(destination).Slice(previous, layout.Bytes), pending);
  }

  private static int MeasureView(ReadOnlySpan<byte> source) {
    var limits = SExpressionLimits.Default;
    if (source.Length > limits.MaxInputBytes) {
      throw new SExpressionEncodeException(SExpressionEncodeErrorKind.SizeLimit);
    }

    try {
      if (SExpressionScanner.Scan(source, limits, 0) != source.Length) {
        throw new InvalidOperationException("The previously validated expression was modified.");
      }
    }
    catch (SExpressionParseException error) {
      throw error.Kind switch {
        SExpressionParseErrorKind.AtomLimit => new SExpressionEncodeException(SExpressionEncodeErrorKind.AtomLimit),
        SExpressionParseErrorKind.NodeLimit => new SExpressionEncodeException(SExpressionEncodeErrorKind.NodeLimit),
        SExpressionParseErrorKind.DepthLimit => new SExpressionEncodeException(SExpressionEncodeErrorKind.DepthLimit),
        var _ => new InvalidOperationException("The previously validated expression was modified.")
      };
    }

    return source.Length;
  }

  private static Layout MeasureOwned(SExpression root) {
    var limits = SExpressionLimits.Default;
    var pending = new Stack<(SExpression Node, int Depth)>();
    pending.Push((root, 0));
    var nodes = 0;
    var lists = 0;
    long size = 0;
    while (pending.Count != 0) {
      var (node, depth) = pending.Pop();
      if (nodes >= limits.MaxNodes) {
        throw new SExpressionEncodeException(SExpressionEncodeErrorKind.NodeLimit);
      }

      ++nodes;
      long added;
      if (node.IsAtom) {
        var atomLength = node.AtomBytes.Length;
        if (atomLength > limits.MaxAtomBytes) {
          throw new SExpressionEncodeException(SExpressionEncodeErrorKind.AtomLimit);
        }

        added = DecimalDigits(atomLength) + 1L + atomLength;
      }
      else {
        if (depth >= limits.MaxDepth) {
          throw new SExpressionEncodeException(SExpressionEncodeErrorKind.DepthLimit);
        }

        ++lists;
        var children = node.Children;
        if (((long)nodes + pending.Count + children.Count) > limits.MaxNodes) {
          throw new SExpressionEncodeException(SExpressionEncodeErrorKind.NodeLimit);
        }

        for (var index = children.Count - 1; index >= 0; --index) {
          pending.Push((children[index], depth + 1));
        }

        added = 2;
      }

      if (size > (long.MaxValue - added)) {
        throw new SExpressionEncodeException(SExpressionEncodeErrorKind.LengthOverflow);
      }

      size += added;
      if (size > limits.MaxInputBytes) {
        throw new SExpressionEncodeException(SExpressionEncodeErrorKind.SizeLimit);
      }
    }

    return new Layout((int)size, nodes + lists);
  }

  private static int DecimalDigits(int length) {
    var digits = 1;
    while (length >= 10) {
      length /= 10;
      ++digits;
    }

    return digits;
  }

  private static void CheckDestination(int previous, int additional) {
    if (previous > (SExpressionLimits.Default.MaxInputBytes - additional)) {
      throw new SExpressionEncodeException(SExpressionEncodeErrorKind.SizeLimit);
    }
  }

  private static void WriteOwned(SExpression root, Span<byte> output, Stack<(SExpression? Node, bool Close)> pending) {
    pending.Push((root, false));
    var position = 0;
    Span<byte> digits = stackalloc byte[10];
    while (pending.Count != 0) {
      var (node, close) = pending.Pop();
      if (close) {
        output[position++] = (byte)')';
      }
      else if (node!.IsAtom) {
        var bytes = node.AtomBytes;
        var length = bytes.Length;
        var first = digits.Length;
        do {
          digits[--first] = (byte)('0' + (length % 10));
          length /= 10;
        } while (length != 0);

        digits[first..].CopyTo(output[position..]);
        position += digits.Length - first;
        output[position++] = (byte)':';
        bytes.CopyTo(output[position..]);
        position += bytes.Length;
      }
      else {
        output[position++] = (byte)'(';
        pending.Push((null, true));
        var children = node.Children;
        for (var index = children.Count - 1; index >= 0; --index) {
          pending.Push((children[index], false));
        }
      }
    }
  }

  private readonly record struct Layout(int Bytes, int PendingItems);
}
