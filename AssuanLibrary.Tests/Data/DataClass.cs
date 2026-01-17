using TUnit.Core.Interfaces;

namespace AssuanLibrary.Tests.Data;

public sealed class DataClass : IAsyncInitializer, IAsyncDisposable {
  public async ValueTask DisposeAsync()
    => await Console.Out.WriteLineAsync("And when the class is finished with, we can clean up any resources.");

  public Task InitializeAsync()
    => Console.Out.WriteLineAsync(
      "Classes can be injected into tests, and they can perform some initialisation logic such as starting an in-memory server or a test container.");
}
