# NReco LambdaParser
Runtime parser for string expressions (formulas, method calls). Builds dynamic LINQ queries at runtime and compiles them to lambda delegates.

* PCL (Portable) library: can be used with *any* .NET target: net40+sl5, net45+wp8+win8+wpa81, dnxcore50 (yes LambdaParser can be used with .NET Core CLR)
* any number of variables (provided as dictionary or by callback delegate)
* supports all arithmetic operations (+, -, *, /, %) and conditions (==, !=, >, <, >=, <=), conditional (ternary) operator ( ? : )
* access object properties, call methods and indexers, invoke delegates
* dynamic typed variables: performs automatic runtime type conversions to match method signature or arithmetic operations
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
(see unit tests for more expression examples)
