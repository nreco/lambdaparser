using System;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using System.Text;
using NUnit.Framework;
using NReco;
using NReco.Linq;

namespace NReco.Tests.Linq {

	[TestFixture]
	public class LambdaParserTests {

		[Test]
		public void Eval() {
			var lambdaParser = new LambdaParser();

			var varContext = new Dictionary<string,object>();
			varContext["pi"] = 3.14M;
			varContext["one"] = 1M;
			varContext["two"] = 2M;
			varContext["test"] = "test";
			varContext["now"] = DateTime.Now;
			varContext["testObj"] = new TestClass();
			varContext["arr1"] = new double[] { 1.5, 2.5 };

			Assert.AreEqual("st", lambdaParser.Eval("test.Substring(2)", varContext ) );

			Assert.AreEqual(3, lambdaParser.Eval("1+2", varContext) );
			Assert.AreEqual(6, lambdaParser.Eval("1+2+3", varContext));
			Assert.AreEqual("b{0}_", lambdaParser.Eval("\"b{0}_\"", varContext));

			Assert.AreEqual(3, lambdaParser.Eval("(1+(3-1)*4)/3", varContext));
			
			Assert.AreEqual(1, lambdaParser.Eval("one*5*one-(-1+5*5%10)", varContext));

			Assert.AreEqual("ab", lambdaParser.Eval("\"a\"+\"b\"", varContext));

			Assert.AreEqual(4.14, lambdaParser.Eval("pi + 1", varContext) );

			Assert.AreEqual(5.14, lambdaParser.Eval("2 +pi", varContext) );

			Assert.AreEqual(2.14, lambdaParser.Eval("pi + -one", varContext) );

			Assert.AreEqual("test1", lambdaParser.Eval("test + \"1\"", varContext) );

			Assert.AreEqual(1, lambdaParser.Eval("true or false ? 1 : 0", varContext) );

			Assert.AreEqual(true, lambdaParser.Eval("5<=3 ? false : true", varContext));

			Assert.AreEqual(5, lambdaParser.Eval("pi>one && 0<one ? (1+8)/3+1*two : 0", varContext));

			Assert.AreEqual(4, lambdaParser.Eval("pi>0 ? one+two+one : 0", varContext));

			Assert.AreEqual(DateTime.Now.Year, lambdaParser.Eval("now.Year", varContext) );

			Assert.AreEqual(true, lambdaParser.Eval(" (1+testObj.IntProp)==2 ? testObj.FldTrue : false ", varContext));

			Assert.AreEqual("ab2_3", lambdaParser.Eval(" \"a\"+testObj.Format(\"b{0}_{1}\", 2, \"3\".ToString() ).ToString() ", varContext));

			Assert.AreEqual(true, lambdaParser.Eval(" testObj.Hash[\"a\"] == \"1\"", varContext));
			
			Assert.AreEqual(true, lambdaParser.Eval(" (testObj.Hash[\"a\"]-1)==testObj.Hash[\"b\"].Length ", varContext));

			Assert.AreEqual(4.0, lambdaParser.Eval(" arr1[0]+arr1[1] ", varContext));

			Assert.AreEqual(2, lambdaParser.Eval(" (new[]{1,2})[1] ", varContext));

			Assert.AreEqual(true, lambdaParser.Eval(" new[]{ one } == new[] { 1 } ", varContext));

			Assert.AreEqual(3, lambdaParser.Eval(" new dictionary{ {\"a\", 1}, {\"b\", 2}, {\"c\", 3} }.Count ", varContext));

			Assert.AreEqual(2, lambdaParser.Eval(" new dictionary{ {\"a\", 1}, {\"b\", 2}, {\"c\", 3} }[\"b\"] ", varContext));

			var arr = ((Array)lambdaParser.Eval(" new []{ new dictionary{{\"test\",2}}, new[] { one } }", varContext) );
			Assert.AreEqual(2, ((IDictionary)arr.GetValue(0) )["test"] );
			Assert.AreEqual(1, ((Array)arr.GetValue(1) ).GetValue(0) );

			Assert.AreEqual("str", lambdaParser.Eval(" testObj.GetDelegNoParam()() ", varContext));
			Assert.AreEqual("zzz", lambdaParser.Eval(" testObj.GetDelegOneParam()(\"zzz\") ", varContext));

			Assert.AreEqual(false, lambdaParser.Eval("(testObj.FldTrue and false) || (testObj.FldTrue && false)", varContext ) );
			Assert.AreEqual(true, lambdaParser.Eval("false or testObj.FldTrue", varContext ) );
			Assert.AreEqual("True", lambdaParser.Eval("testObj.BoolParam(true)", varContext ) );
		}

		[Test]
		public void EvalCachePerf() {
			var lambdaParser = new LambdaParser();

			var varContext = new Dictionary<string, object>();
			varContext["a"] = 55;
			varContext["b"] = 2;

			var sw = new Stopwatch();
			sw.Start();
			for (int i = 0; i < 10000; i++) {

				Assert.AreEqual(105, lambdaParser.Eval("(a*2 + 100)/b", varContext));
			}
			sw.Stop();
			Console.WriteLine("10000 iterations: {0}", sw.Elapsed);
		}


		public class TestClass {

			public int IntProp { get { return 1; } }

			public string StrProp { get { return "str"; } }

			public bool FldTrue { get { return true; } }

			public IDictionary Hash {
				get {
					return new Hashtable() {
						{"a", 1},
						{"b", ""}
					};
				}
			}

			public string Format(string s, object arg1, int arg2) {
				return String.Format(s, arg1, arg2);
			}

			public string BoolParam(bool flag) {
				return flag.ToString();
			}

			public Func<string, string> GetDelegOneParam() {
				return (s) => {
					return s;
				};
			}

			public Func<string> GetDelegNoParam() {
				return () => {
					return StrProp;
				};
			}


		}

	}
}
