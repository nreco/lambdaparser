# NReco LambdaParser
Runtime parser for string expressions (formulas, method calls, properties/fields/arrays accessors). `LambdaParser` builds dynamic LINQ expression tree and compiles it to the lambda delegate. Types are resolved at run-time like in dynamic languages. 

NuGet | Windows x64 | Linux
--- | --- | ---
[![NuGet Release](https://img.shields.io/nuget/v/NReco.LambdaParser.svg)](https://www.nuget.org/packages/NReco.LambdaParser/) | [![AppVeyor](https://img.shields.io/appveyor/ci/nreco/lambdaparser/master.svg)](https://ci.appveyor.com/project/nreco/lambdaparser) | ![Tests](https://github.com/nreco/lambdaparser/actions/workflows/dotnet-test.yml/badge.svg) 

* can be used in *any* .NET app: net45 (legacy .NET Framework apps), netstandard1.3 (.NET Core apps), netstandard2.0 (all modern .NET apps).
* any number of expression arguments (values can be provided as dictionary or by callback delegate)
* supports arithmetic operations (+, -, *, /, %), comparisons (==, !=, >, <, >=, <=), conditionals including (ternary) operator ( boolVal ? whenTrue : whenFalse )
* access object properties, call methods and indexers, invoke delegates
* dynamic typed variables: performs automatic type conversions to match method signature or arithmetic operations
* create arrays and dictionaries with simplified syntax: `new dictionary{ {"a", 1}, {"b", 2} }` , `new []{ 1, 2, 3}`
* local variables that may go before main expression: `var a = 5; var b = contextVar/total*100;`  (disabled by default, to enable use `LambdaParser.AllowVars` property)

Nuget package: [NReco.LambdaParser](https://www.nuget.org/packages/NReco.LambdaParser/)

```
var lambdaParser = new NReco.Linq.LambdaParser();

var varContext = new Dictionary<string,object>();
varContext["pi"] = 3.14M;
varContext["one"] = 1M;
varContext["two"] = 2M;
varContext["test"] = "test";
Console.WriteLine( lambdaParser.Eval("pi>one && 0<one ? (1+8)/3+1*two : 0", varContext) ); // --> 5
Console.WriteLine( lambdaParser.Eval("test.ToUpper()", varContext) ); // --> TEST
```
(see [unit tests](https://github.com/nreco/lambdaparser/blob/master/src/NReco.LambdaParser.Tests/LambdaParserTests.cs) for more expression examples)

## Custom values comparison
By default `LambdaParser` uses `ValueComparer` for values comparison. You can provide your own implementation or configure its option to get desired behaviour:
* `ValueComparer.NullComparison` determines how comparison with `null` is handled. 2 options: 
  * `MinValue`: null is treated as minimal possible value for any type - like .NET IComparer
  * `Sql`: null is not comparable with any type, including another null - like in SQL
* `ValueComparer.SuppressErrors` allows to avoid convert exception. If error appears during comparison exception is not thrown and this means that values are not comparable (= any condition leads to `false`).
```
var valComparer = new ValueComparer() { NullComparison = ValueComparer.NullComparisonMode.Sql };
var lambdaParser = new LambdaParser(valComparer); 
```
### Caching Expressions

The `UseCache` property determines whether the `LambdaParser` should cache parsed expressions. By default, `UseCache` is set to `true`, meaning expressions are cached to improve performance for repeated evaluations of the same expression. 

Therefore, using a singleton instance of `LambdaParser` is recommended, rather than creating a new instance each time.

You can disable caching by setting UseCache to false if you want to save memory, especially when evaluating a large number of unique expressions.

```csharp
var lambdaParser = new LambdaParser();
lambdaParser.UseCache = false;
```

## Who is using this?
NReco.LambdaParser is in production use at [SeekTable.com](https://www.seektable.com/) and [PivotData microservice](https://www.nrecosite.com/pivotdata_service.aspx) (used for user-defined calculated cube members: formulas, custom formatting).

## License
Copyright 2016-2024 Vitaliy Fedorchenko and contributors

Distributed under the MIT license
