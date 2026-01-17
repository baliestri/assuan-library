// Here you could define global logic that would affect all tests

// You can use attributes at the assembly level to apply to all tests in the assembly

using System.Diagnostics.CodeAnalysis;

[assembly: Retry(3)]
[assembly: ExcludeFromCodeCoverage]

namespace AssuanLibrary.Tests;

public sealed class GlobalHooks {
  [Before(TestSession)]
  public static void SetUp()
    => Console.WriteLine(@"Or you can define methods that do stuff before...");

  [After(TestSession)]
  public static void CleanUp()
    => Console.WriteLine(@"...and after!");
}
