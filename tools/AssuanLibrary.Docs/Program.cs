// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Docs;

var root = DocumentationBuilder.FindRepositoryRoot(AppContext.BaseDirectory);
try {
  switch (args) {
    case ["build", "--version", var _, "--base-url", var _, "--output", var _]:
      Console.WriteLine(await DocumentationBuilder.BuildAsync(root, args[2], args[4], args[6],
        CancellationToken.None));
      return 0;
    case ["verify", "--bundle", var _]: {
      var bundle = Path.IsPathRooted(args[2]) ? args[2] : Path.Combine(root, args[2]);
      var errors = DocumentationBuilder.Verify(bundle);
      foreach (var error in errors) {
        Console.Error.WriteLine(error);
      }

      return errors.Count == 0 ? 0 : 1;
    }
    default:
      Console.Error.WriteLine("Usage: build --version <version> --base-url <url> --output <root> | verify --bundle <path>");
      return 2;
  }
}
catch (Exception error) when (error is IOException or InvalidDataException or ArgumentException) {
  Console.Error.WriteLine("Documentation operation failed: " + error.GetType().Name);
  return 1;
}
