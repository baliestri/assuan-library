// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Server;

namespace AssuanLibrary.Tests.Server;

public sealed class OptionsTests {
  [Fact]
  public void DefaultsBoundEachSessionOperation() {
    var options = ServerOptions.Default;
    Assert.Equal(TimeSpan.FromSeconds(120), options.GreetingTimeout);
    Assert.Equal(TimeSpan.FromSeconds(300), options.CommandTimeout);
    Assert.Equal(TimeSpan.FromSeconds(120), options.InquiryTimeout);
    Assert.Equal(TimeSpan.FromSeconds(300), options.IdleTimeout);
    Assert.Equal(TimeSpan.FromSeconds(30), options.ClosedTimeout);
    Assert.Equal(1024 * 1024, options.MaxInquiryBytes);
    Assert.Equal(128, options.MaxSessions);
    Assert.Equal(TimeSpan.FromSeconds(30), options.ShutdownGracePeriod);
  }

  [Fact]
  public void InvalidLimitsFailBeforeAnySessionBegins() {
    Assert.Equal(ServerErrorKind.InvalidOptions,
      Assert.Throws<ServerException>(() => new ServerOptions(commandTimeout: TimeSpan.Zero)).Kind);
    Assert.Equal(ServerErrorKind.InvalidOptions,
      Assert.Throws<ServerException>(() => new ServerOptions(inquiryTimeout: TimeSpan.FromTicks(-1))).Kind);
    Assert.Equal(ServerErrorKind.InvalidOptions,
      Assert.Throws<ServerException>(() => new ServerOptions(greetingTimeout: TimeSpan.MaxValue)).Kind);
    Assert.Equal(ServerErrorKind.InvalidOptions,
      Assert.Throws<ServerException>(() => new ServerOptions(maxInquiryBytes: 0)).Kind);
    Assert.Equal(ServerErrorKind.InvalidOptions,
      Assert.Throws<ServerException>(() => new ServerOptions(idleTimeout: TimeSpan.Zero)).Kind);
    Assert.Equal(ServerErrorKind.InvalidOptions,
      Assert.Throws<ServerException>(() => new ServerOptions(closedTimeout: TimeSpan.Zero)).Kind);
    Assert.Equal(ServerErrorKind.InvalidOptions,
      Assert.Throws<ServerException>(() => new ServerOptions(maxSessions: 0)).Kind);
    Assert.Equal(ServerErrorKind.InvalidOptions,
      Assert.Throws<ServerException>(() => new ServerOptions(shutdownGracePeriod: TimeSpan.Zero)).Kind);
    Assert.Equal(ServerErrorKind.InvalidOptions,
      Assert.Throws<ServerException>(() => new ServerOptions(shutdownGracePeriod: TimeSpan.MaxValue)).Kind);
  }
}
