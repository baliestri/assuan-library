// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics.Contracts;

namespace AssuanLibrary.Extensions;

/// <summary>
///   Extension methods for arrays.
/// </summary>
internal static class ArrayExtensions {
  /// <summary>
  ///   Extension methods for arrays.
  /// </summary>
  /// <param name="array">The array to extend.</param>
  /// <typeparam name="T">The type of the array elements.</typeparam>
  extension<T>(T[] array) where T : notnull {
    /// <summary>
    ///   Skips elements in the array until the specified delimiter is found,
    ///   then returns a new array containing the elements after the delimiter.
    /// </summary>
    /// <param name="delimiter">The delimiter to search for.</param>
    /// <returns>A new array containing the elements after the delimiter.</returns>
    [Pure]
    public T[] Skip(T delimiter) {
      if (array.Length == 0) {
        return [];
      }

      for (var i = 0; i < array.Length; i++) {
        if (!EqualityComparer<T>.Default.Equals(array[i], delimiter)) {
          continue;
        }

        return array.AsSpan(i + 1).ToArray();
      }

      return [];
    }

    /// <summary>
    ///   Takes elements in the array until the specified delimiter is found,
    ///   then returns a new array containing the elements before the delimiter.
    /// </summary>
    /// <param name="delimiter">The delimiter to search for.</param>
    /// <returns>A new array containing the elements before the delimiter.</returns>
    [Pure]
    public T[] Take(T delimiter) {
      if (array.Length == 0) {
        return array;
      }

      for (var i = 0; i < array.Length; i++) {
        if (!EqualityComparer<T>.Default.Equals(array[i], delimiter)) {
          continue;
        }

        return array.AsSpan(0, i).ToArray();
      }

      return array;
    }

    /// <summary>
    ///   Splits the array into multiple arrays based on the specified delimiter.
    /// </summary>
    /// <param name="delimiter">The delimiter to split the array on.</param>
    /// <param name="includeDelimiter">Whether to include the delimiter in the resulting arrays.</param>
    /// <returns>An enumerable of arrays split by the delimiter.</returns>
    [Pure]
    public IEnumerable<T[]> Split(T delimiter, bool includeDelimiter = false) {
      if (array.Length == 0) {
        yield break;
      }

      var startIndex = 0;

      for (var i = 0; i < array.Length; i++) {
        if (!EqualityComparer<T>.Default.Equals(array[i], delimiter)) {
          continue;
        }

        var length = (i - startIndex) + (includeDelimiter ? 1 : 0);
        yield return array.AsSpan(startIndex, length).ToArray();

        startIndex = i + 1;
      }

      if (startIndex < array.Length) {
        yield return array.AsSpan(startIndex).ToArray();
      }
    }

    /// <summary>
    ///   Calculates a hash code for the sequence of elements in the array.
    /// </summary>
    /// <returns>A hash code representing the sequence of elements.</returns>
    [Pure]
    public int GetSequenceHashCode() {
      return GetHashCodes(array).Aggregate(17, (current, hashCode) => (current * 31) + hashCode);

      static IEnumerable<int> GetHashCodes(T[] array) {
        foreach (var element in array) {
          yield return element.GetHashCode();
        }
      }
    }
  }
}
