// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Buffers;
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
        if (!array[i].Equals(delimiter)) {
          continue;
        }

        var copyArray = ArrayPool<T>.Shared.Rent(array.Length - i - 1);

        Array.Copy(array, i + 1, copyArray, 0, copyArray.Length);
        return copyArray;
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
        if (!array[i].Equals(delimiter)) {
          continue;
        }

        var copyArray = ArrayPool<T>.Shared.Rent(i);

        Array.Copy(array, 0, copyArray, 0, copyArray.Length);
        return copyArray;
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
        if (!array[i].Equals(delimiter)) {
          continue;
        }

        var length = includeDelimiter
          ? (i - startIndex) + 1
          : i - startIndex;
        var copyArray = ArrayPool<T>.Shared.Rent(length);

        Array.Copy(array, startIndex, copyArray, 0, length);
        yield return copyArray;

        startIndex = i + 1;
      }

      if (startIndex < array.Length) {
        var length = array.Length - startIndex;
        var copyArray = ArrayPool<T>.Shared.Rent(length);

        Array.Copy(array, startIndex, copyArray, 0, length);
        yield return copyArray;
      }
    }

    /// <summary>
    ///   Calculates a hash code for the sequence of elements in the array.
    /// </summary>
    /// <returns>A hash code representing the sequence of elements.</returns>
    [Pure]
    public int GetSequenceHashCode() {
      return GetHashCodes(array).Aggregate((x, y) => x ^ y);

      static IEnumerable<int> GetHashCodes(T[] array) {
        foreach (var element in array) {
          yield return element.GetHashCode();
        }
      }
    }
  }
}
