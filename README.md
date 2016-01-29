# NReco LambdaParser
Runtime parser for string expressions (formulas, method calls). Builds LINQ expression tree and compiles it to lambda delegate.

* PCL (Portable) library: can be used with *any* .NET target framework
* any number of variables (provided as dictionary or by callback delegate)
* supports all arithmetic operations and conditionals
* access object properties, call methods and indexers
* runtime type conversions to match method signature or arithmetic operations
* create arrays and dictionaries with simplified syntax: `new dictionary{ {"a", 1}, {"b", 2} }` , `new []{ 1, 2, 3}`

```
var lambdaParser = new NReco.Linq.LambdaParser();

var varContext = new Dictionary<string,object>();
varContext["pi"] = 3.14M;
varContext["one"] = 1M;
varContext["two"] = 2M;
varContext["test"] = "test";
Console.WriteLine( lambdaParser.Eval("pi>one && 0<one ? (1+8)/3+1*two : 0", varContext) ); // --> 5
```
