using TUnit.Core.Interfaces;

namespace AssuanLibrary.Tests.Data;

public sealed class DependencyInjectionClassConstructor : IClassConstructor {
  public Task<object> Create(Type type, ClassConstructorMetadata classConstructorMetadata) {
    Console.WriteLine(
      "You can also control how your test classes are new'd up, giving you lots of power and the ability to utilise tools such as dependency injection");

    return type == typeof(AndEvenMoreTests) ? Task.FromResult<object>(new AndEvenMoreTests(new DataClass())) : throw new NotImplementedException();
  }
}
