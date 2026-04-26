// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Protocol;

namespace AssuanLibrary.Tests.Protocol;

public sealed class AssuanCommandFormatterTests {
  [Fact]
  public void Format_ShouldReturnSameBytes_AsCommandSerialization() {
    var formatter = new AssuanCommandFormatter();

    var command = new AssuanCommand("GETINFO");
    command.Add("version");

    var formatted = formatter.Format(command);

    formatted.ShouldBe(command.ToBytes());
  }

  [Fact]
  public void FormatAsync_ShouldReturnSameBytes_AsCommandSerialization() {
    var formatter = new AssuanCommandFormatter();

    var command = new AssuanCommand("OPTION");
    command.Add("ttyname=/dev/pts/1");

    var formatted = formatter.FormatAsync(command);

    formatted.ToArray().ShouldBe(command.ToBytes());
  }
}

