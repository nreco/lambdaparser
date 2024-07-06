#region License
/*
 * NReco Lambda Parser (http://www.nrecosite.com/)
 * Copyright 2014 Vitaliy Fedorchenko
 * Distributed under the MIT license
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
#endregion

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Reflection;
using System.ComponentModel;
using System.Globalization;

namespace NReco.Linq {
	
	/// <summary>
	/// Runtime parser for string expressions (formulas, method calls etc) into LINQ expression tree or lambda delegate.
	/// </summary>
	public class LambdaParser {

		static readonly char[] delimiters = new char[] {
			'(', ')', '[', ']', '?', ':', ';', '.', ',', '=', '<', '>', '!', '&', '|', '*', '/', '%', '+','-', '{', '}'};
		static readonly char[] specialNameChars = new char[] {
			'_' };
		const char charQuote = '"';
		static readonly string[] mulOps = new[] {"*", "/", "%" };
		static readonly string[] addOps = new[] { "+", "-" };
		static readonly string[] eqOps = new[] { "==", "!=", "<", ">", "<=", ">=" };

		readonly IDictionary<string, CompiledExpression> CachedExpressions = new ConcurrentDictionary<string, CompiledExpression>();

		/// <summary>
		/// Gets or sets whether LambdaParser should use the cache for parsed expressions.
		/// </summary>
		public bool UseCache { get; set; }

		/// <summary>
		/// Allows usage of "=" for equality comparison (in addition to "=="). False by default.
		/// </summary>
		public bool AllowSingleEqualSign { get; set; }

		/// <summary>
		/// Allows usage of "var" assignments that may go before resulting expression. False by default.
		/// </summary>
		/// <example><code>var test = "test"; test+" works!"</code></example>
		public bool AllowVars { get; set; }

		/// <summary>
		/// Gets value comparer used by the parser for comparison operators.
		/// </summary>
		public IValueComparer Comparer { get; private set; }

		/// <summary>
		/// Gets invoke method class used by the parser for invoking methods.
		/// </summary>
		public IInvokeMethod Invoker { get; private set; }

		LambdaParameterWrapperContext _LambdaParamCtx = null;
		LambdaParameterWrapperContext LambdaParamCtx => _LambdaParamCtx ??
			(_LambdaParamCtx = new LambdaParameterWrapperContext(Comparer, Invoker));

		public LambdaParser() {
			UseCache = true;
			AllowSingleEqualSign = false;
			AllowVars = false;
			Comparer = ValueComparer.Instance;
			Invoker = InvokeMethod.Instance;
		}

		public LambdaParser(IValueComparer valueComparer) : this() {
			Comparer = valueComparer;
		}

		public LambdaParser(IInvokeMethod invokeMethod) : this() {
			Invoker = invokeMethod;
		}

		public LambdaParser(IValueComparer valueComparer, IInvokeMethod invokeMethod) : this() {
			Comparer = valueComparer;
			Invoker = invokeMethod;
		}

		internal class ExtractParamsVisitor : ExpressionVisitor {
			internal List<ParameterExpression> ParamsList;
			public ExtractParamsVisitor() {
				ParamsList = new List<ParameterExpression>();
			}

			public override Expression Visit(Expression node) {
				if (node != null && node.NodeType == ExpressionType.Parameter) {
					var paramExpr = (ParameterExpression)node;
					if (paramExpr.Name!=null) // local vars don't have names
						ParamsList.Add(paramExpr);
				}
				return base.Visit(node);
			}
		}

		public static ParameterExpression[] GetExpressionParameters(Expression expr) {
			var paramsVisitor = new ExtractParamsVisitor();
			paramsVisitor.Visit(expr);
			return paramsVisitor.ParamsList.ToArray();
		}

		public object Eval(string expr, IDictionary<string, object> vars) {
			return Eval(expr, (varName) => {
				object val = null;
				vars.TryGetValue(varName, out val);
				return val;
			});
		}

		public object Eval(string expr, Func<string,object> getVarValue) {
			CompiledExpression compiledExpr = null;
			if (UseCache) {
				CachedExpressions.TryGetValue(expr, out compiledExpr);
			}

			if (compiledExpr == null) {
				var linqExpr = Parse(expr);
				compiledExpr = new CompiledExpression() {
					Parameters = GetExpressionParameters(linqExpr)
				};
				var lambdaExpr = Expression.Lambda(linqExpr, compiledExpr.Parameters);
				compiledExpr.Lambda = lambdaExpr.Compile();

				if (UseCache)
					CachedExpressions[expr] = compiledExpr;
			}

			var valuesList = new List<object>();
			foreach (var paramExpr in compiledExpr.Parameters) {
				valuesList.Add( new LambdaParameterWrapper( getVarValue(paramExpr.Name), LambdaParamCtx) );
			}

			var lambdaRes = compiledExpr.Lambda.DynamicInvoke(valuesList.ToArray());
			if (lambdaRes is LambdaParameterWrapper)
				lambdaRes = ((LambdaParameterWrapper)lambdaRes).Value;
			return lambdaRes;
		}


		public Expression Parse(string expr) {
			ParseResult parseResult;
			if (AllowVars) {
				var assigns = new List<Expression>();
				var varExprs = new List<ParameterExpression>();
				var vars = new Variables(false);
				int start = 0;
				do {
					var assignResult = ParseVar(expr, start, vars);
					if (assignResult.Expr == null)
						break;
					assigns.Add(assignResult.Expr);
					varExprs.Add(assignResult.VarExpr);
					vars.Define(assignResult.VarName, assignResult.VarExpr);
					start = assignResult.End;
				} while (true);
				parseResult = ParseConditional(expr, start, vars);
				if (assigns.Count>0) {
					assigns.Add(parseResult.Expr); // last entry is a result
					parseResult.Expr = Expression.Block(varExprs, assigns);
				}
			} else {
				parseResult = ParseConditional(expr, 0, new Variables());
			}
			var lastLexem = ReadLexem(expr, parseResult.End);
			if (lastLexem.Type != LexemType.Stop)
				throw new LambdaParserException(expr, parseResult.End, "Invalid expression");
			return parseResult.Expr;
		}

		protected Lexem ReadLexem(string s, int startIdx) {
			var lexem = new Lexem();
			lexem.Type = LexemType.Unknown;
			lexem.Expr = s;
			lexem.Start = startIdx;
			lexem.End = startIdx;
			while (lexem.End < s.Length) {
				if (Array.IndexOf(delimiters, s[lexem.End]) >= 0) {
					if (lexem.Type == LexemType.Unknown) {
						lexem.End++;
						lexem.Type = LexemType.Delimiter;
						return lexem;
					}
					if (lexem.Type != LexemType.StringConstant && (lexem.Type != LexemType.NumberConstant || s[lexem.End] != '.'))
						return lexem; // stop
				} else if (Char.IsSeparator(s[lexem.End])) {
					if (lexem.Type != LexemType.StringConstant && lexem.Type != LexemType.Unknown)
						return lexem; // stop
				} else if (Char.IsLetter(s[lexem.End])) {
					if (lexem.Type == LexemType.Unknown)
						lexem.Type = LexemType.Name;
				} else if (Char.IsDigit(s[lexem.End])) {
					if (lexem.Type == LexemType.Unknown)
						lexem.Type = LexemType.NumberConstant;
				} else if (Array.IndexOf(specialNameChars, s[lexem.End]) >= 0) {
					if (lexem.Type == LexemType.Unknown || lexem.Type==LexemType.Name) {
						lexem.Type = LexemType.Name;
					} else if (lexem.Type!=LexemType.StringConstant)
						return lexem;
				} else if (s[lexem.End] == charQuote) {
					if (lexem.Type == LexemType.Unknown)
						lexem.Type = LexemType.StringConstant;
					else {
						if (lexem.Type == LexemType.StringConstant) {
							// check for "" combination
							if (((lexem.End + 1) >= s.Length || s[lexem.End + 1] != charQuote)) {
								lexem.End++;
								return lexem;
							} else
								if ((lexem.End + 1) < s.Length)
									lexem.End++; // skip next quote
						} else {
							return lexem;
						}
					}
				} else if (Char.IsControl(s[lexem.End]) && lexem.Type != LexemType.Unknown && lexem.Type != LexemType.StringConstant)
					return lexem;

				// goto next char
				lexem.End++;
			}

			if (lexem.Type == LexemType.Unknown) {
				lexem.Type = LexemType.Stop;
				return lexem;
			}
			if (lexem.Type == LexemType.StringConstant)
				throw new LambdaParserException(s, startIdx, "Unterminated string constant");
			return lexem;
		}


		static readonly ConstructorInfo LambdaParameterWrapperConstructor =
			typeof(LambdaParameterWrapper).GetConstructor(new[] { typeof(object), typeof(LambdaParameterWrapperContext) });

		protected ParseVarResult ParseVar(string expr, int start, Variables vars) {
			var varLexem = ReadLexem(expr, start);
			if (varLexem.Type==LexemType.Name && varLexem.GetValue()=="var") {
				var varNameLexem = ReadLexem(expr, varLexem.End);
				if (varNameLexem.Type != LexemType.Name)
					throw new LambdaParserException(expr, varLexem.End, "Expected variable name");
				var varName = varNameLexem.GetValue();
				var eqLexem = ReadLexem(expr, varNameLexem.End);
				if (eqLexem.Type!=LexemType.Delimiter || eqLexem.GetValue() != "=")
					throw new LambdaParserException(expr, varNameLexem.End, "Expected '='");
				var varValExpr = ParseConditional(expr, eqLexem.End, vars);
				var varEndLexem = ReadLexem(expr, varValExpr.End);
				if (varEndLexem.Type!=LexemType.Delimiter || varEndLexem.GetValue()!=";")
					throw new LambdaParserException(expr, varValExpr.End, "Expected ';'");
				var varParamExpr = Expression.Variable(typeof(LambdaParameterWrapper), null);
				return new ParseVarResult() {
					VarName = varName,
					VarExpr = varParamExpr,
					Expr = Expression.Assign(
						varParamExpr,
						Expression.New(
							LambdaParameterWrapperConstructor,
							Expression.TypeAs(varValExpr.Expr, typeof(object)),
							Expression.Constant(LambdaParamCtx, typeof(LambdaParameterWrapperContext))
						)),
					End = varEndLexem.End
				};
			}
			return new ParseVarResult() {
				Expr = null,
				End = start
			};
		}

		protected ParseResult ParseConditional(string expr, int start, Variables vars) {
			var testExpr = ParseOr(expr, start, vars);
 			var ifLexem = ReadLexem(expr, testExpr.End);
			if (ifLexem.Type == LexemType.Delimiter && ifLexem.GetValue() == "?") {
				// read positive expr
				var positiveOp = ParseOr(expr, ifLexem.End, vars);
				var positiveOpExpr = Expression.New(LambdaParameterWrapperConstructor, 
						Expression.Convert(positiveOp.Expr,typeof(object)),
						Expression.Constant(LambdaParamCtx, typeof(LambdaParameterWrapperContext)));

				var elseLexem = ReadLexem(expr, positiveOp.End);
				if (elseLexem.Type == LexemType.Delimiter && elseLexem.GetValue() == ":") {
					var negativeOp = ParseConditional(expr, elseLexem.End, vars);
					var negativeOpExpr = Expression.New(LambdaParameterWrapperConstructor, 
							Expression.Convert( negativeOp.Expr, typeof(object)),
							Expression.Constant(LambdaParamCtx, typeof(LambdaParameterWrapperContext)));
					return new ParseResult() {
						End = negativeOp.End,
						Expr = Expression.Condition( Expression.IsTrue( testExpr.Expr ), positiveOpExpr, negativeOpExpr)
					};

				} else {
					throw new LambdaParserException(expr, positiveOp.End, "Expected ':'");
				}
			}
			return testExpr;
		}

		Expression WrapLambdaParameterIsTrueIfNeeded(Expression expr) {
			if (expr.Type == typeof(LambdaParameterWrapper))
				return Expression.Property(expr, "IsTrue");
			return expr;
		}

		protected ParseResult ParseOr(string expr, int start, Variables vars) {
			var firstOp = ParseAnd(expr, start, vars);
			do {
				var opLexem = ReadLexem(expr, firstOp.End);
				var isOr = false;
				if (opLexem.Type == LexemType.Name && opLexem.GetValue() == "or") {
					isOr = true;
				} else if (opLexem.Type == LexemType.Delimiter && opLexem.GetValue() == "|") {
					opLexem = ReadLexem(expr, opLexem.End);
					if (opLexem.Type == LexemType.Delimiter && opLexem.GetValue() == "|")
						isOr = true;
				}

				if (isOr) {
					var secondOp = ParseOr(expr, opLexem.End, vars);
					firstOp = new ParseResult() {
						End = secondOp.End,
						Expr = Expression.OrElse( 
							WrapLambdaParameterIsTrueIfNeeded(firstOp.Expr), 
							WrapLambdaParameterIsTrueIfNeeded(secondOp.Expr) )
					};
				} else
					break;
			} while (true);
			return firstOp;
		}

		protected ParseResult ParseAnd(string expr, int start, Variables vars) {
			var firstOp = ParseEq(expr, start, vars);
			do {
				var opLexem = ReadLexem(expr, firstOp.End);
				var isAnd = false;
				if (opLexem.Type == LexemType.Name && opLexem.GetValue() == "and") {
					isAnd = true;
				} else if (opLexem.Type == LexemType.Delimiter && opLexem.GetValue() == "&") {
					opLexem = ReadLexem(expr, opLexem.End);
					if (opLexem.Type == LexemType.Delimiter && opLexem.GetValue() == "&")
						isAnd = true;
				}

				if (isAnd) {
					var secondOp = ParseAnd(expr, opLexem.End, vars);
					firstOp = new ParseResult() {
						End = secondOp.End,
						Expr = Expression.AndAlso(
									WrapLambdaParameterIsTrueIfNeeded(firstOp.Expr),
									WrapLambdaParameterIsTrueIfNeeded(secondOp.Expr))
					};
				} else
					break;
			} while (true);
			return firstOp;
		}

		protected ParseResult ParseEq(string expr, int start, Variables vars) {
			var firstOp = ParseAdditive(expr, start, vars);
			do {
				var opLexem = ReadLexem(expr, firstOp.End);
				if (opLexem.Type == LexemType.Delimiter) {
					var nextOpLexem = ReadLexem(expr, opLexem.End);
					if (nextOpLexem.Type == LexemType.Delimiter) {
						var opVal = opLexem.GetValue() + nextOpLexem.GetValue();
						if (eqOps.Contains(opVal)) {
							var secondOp = ParseAdditive(expr, nextOpLexem.End, vars);

							switch (opVal) {
								case "==":
									firstOp = new ParseResult() {
										End = secondOp.End,
										Expr = Expression.Equal(firstOp.Expr, secondOp.Expr)
									};
									continue;
								case "<>":
								case "!=":
									firstOp = new ParseResult() {
										End = secondOp.End,
										Expr = Expression.NotEqual(firstOp.Expr, secondOp.Expr)
									};
									continue;
								case ">=":
									firstOp = new ParseResult() {
										End = secondOp.End,
										Expr = Expression.GreaterThanOrEqual(firstOp.Expr, secondOp.Expr)
									};
									continue;
								case "<=":
									firstOp = new ParseResult() {
										End = secondOp.End,
										Expr = Expression.LessThanOrEqual(firstOp.Expr, secondOp.Expr)
									};
									continue;

							}
						}

					}

					if (opLexem.GetValue() == ">" || opLexem.GetValue() == "<" 
						|| (AllowSingleEqualSign && opLexem.GetValue() == "=") ) {
						var secondOp = ParseAdditive(expr, opLexem.End, vars);
						switch (opLexem.GetValue()) {
							case ">":
								firstOp = new ParseResult() {
									End = secondOp.End,
									Expr = Expression.GreaterThan(firstOp.Expr, secondOp.Expr)
								};
								continue;
							case "<":
								firstOp = new ParseResult() {
									End = secondOp.End,
									Expr = Expression.LessThan(firstOp.Expr, secondOp.Expr)
								};
								continue;
							case "=":
								firstOp = new ParseResult() {
									End = secondOp.End,
									Expr = Expression.Equal(firstOp.Expr, secondOp.Expr)
								};
								continue;
						}
					}

				}
				break;
			} while (true);
			return firstOp;
		}


		protected ParseResult ParseAdditive(string expr, int start, Variables vars) {
			var firstOp = ParseMultiplicative(expr, start, vars);
			do {
				var opLexem = ReadLexem(expr, firstOp.End);
				if (opLexem.Type == LexemType.Delimiter && addOps.Contains(opLexem.GetValue())) {
					var secondOp = ParseMultiplicative(expr, opLexem.End, vars);
					var res = new ParseResult() { End = secondOp.End };
					switch (opLexem.GetValue()) {
						case "+":
							res.Expr = Expression.Add(firstOp.Expr, secondOp.Expr);
							break;
						case "-":
							res.Expr = Expression.Subtract(firstOp.Expr, secondOp.Expr);
							break;
					}
					firstOp = res;
					continue;
				}
				break;
			} while (true);
			return firstOp;
		}

		protected ParseResult ParseMultiplicative(string expr, int start, Variables vars) {
			var firstOp = ParseUnary(expr, start, vars);
			do {
				var opLexem = ReadLexem(expr, firstOp.End);
				if (opLexem.Type == LexemType.Delimiter && mulOps.Contains(opLexem.GetValue())) {
					var secondOp = ParseUnary(expr, opLexem.End, vars);
					var res = new ParseResult() { End = secondOp.End };
					switch (opLexem.GetValue()) {
						case "*":
							res.Expr = Expression.Multiply(firstOp.Expr, secondOp.Expr);
							break;
						case "/":
							res.Expr = Expression.Divide(firstOp.Expr, secondOp.Expr);
							break;
						case "%":
							res.Expr = Expression.Modulo(firstOp.Expr, secondOp.Expr);
							break;
					}
					firstOp = res;
					continue;
				}
				break;
			} while (true);
			return firstOp;
		}

		protected ParseResult ParseUnary(string expr, int start, Variables vars) {
			var opLexem = ReadLexem(expr, start);
			if (opLexem.Type == LexemType.Delimiter) {
				switch (opLexem.GetValue()) {
					case "-": {
						var operand = ParsePrimary(expr, opLexem.End, vars);
						operand.Expr = Expression.Negate(operand.Expr);
						return operand;
					}
					case "!": {
						var operand = ParsePrimary(expr, opLexem.End, vars);
						operand.Expr = Expression.Not(operand.Expr);
						return operand;
					}
				}
			}
			return ParsePrimary(expr, start, vars);
		}

		private static MethodInfo GetLambdaParameterWrapperMethod(string methodName) {
			return typeof(LambdaParameterWrapper).GetTypeInfo().DeclaredMethods.Where(m=>m.Name==methodName).First();
		}

		static readonly MethodInfo InvokeMethodMI = GetLambdaParameterWrapperMethod("InvokeMethod");

		static readonly MethodInfo InvokeDelegateMI = GetLambdaParameterWrapperMethod("InvokeDelegate");

		static readonly MethodInfo InvokePropertyOrFieldMI = GetLambdaParameterWrapperMethod("InvokePropertyOrField");

		static readonly MethodInfo InvokeIndexerMI = GetLambdaParameterWrapperMethod("InvokeIndexer");

		static readonly MethodInfo CreateDictionaryMI = GetLambdaParameterWrapperMethod("CreateDictionary");

		protected ParseResult ParsePrimary(string expr, int start, Variables vars) {
			var val = ParseValue(expr, start, vars);
			do {
				var lexem = ReadLexem(expr, val.End);
				if (lexem.Type==LexemType.Delimiter) {
					if (lexem.GetValue() == ".") { // member or method
						var memberLexem = ReadLexem(expr, lexem.End);
						if (memberLexem.Type == LexemType.Name) {
							var openCallLexem = ReadLexem(expr, memberLexem.End);
							if (openCallLexem.Type == LexemType.Delimiter && openCallLexem.GetValue() == "(") {
								var methodParams = new List<Expression>();
								var paramsEnd = ReadCallArguments(expr, openCallLexem.End, ")", methodParams, vars);
								var paramsExpr = Expression.NewArrayInit(typeof(object), methodParams);
								val = new ParseResult() {
									End = paramsEnd,
									Expr = Expression.Call(
										Expression.Constant(new LambdaParameterWrapper(null, LambdaParamCtx)),
										InvokeMethodMI, 
										val.Expr, 
										Expression.Constant(memberLexem.GetValue()),
										paramsExpr)
								};
								continue;
							} else {
								// member
								val = new ParseResult() {
									End = memberLexem.End,
									Expr = Expression.Call(
										Expression.Constant(new LambdaParameterWrapper(null, LambdaParamCtx)),
										InvokePropertyOrFieldMI,
										val.Expr, Expression.Constant(memberLexem.GetValue()))
								};
								continue;
							}
						}
					} else if (lexem.GetValue()=="[") {
						var indexerParams = new List<Expression>();
						var paramsEnd = ReadCallArguments(expr, lexem.End, "]", indexerParams, vars);
						val = new ParseResult() {
							End = paramsEnd,
							Expr = Expression.Call(
								Expression.Constant(new LambdaParameterWrapper(null, LambdaParamCtx)),
								InvokeIndexerMI, val.Expr, 
								Expression.NewArrayInit(typeof(object), indexerParams)
							)
						};
						continue;
					} else if (lexem.GetValue()=="(") {
						var methodParams = new List<Expression>();
						var paramsEnd = ReadCallArguments(expr, lexem.End, ")", methodParams, vars);
						var paramsExpr = Expression.NewArrayInit(typeof(object), methodParams);
						val = new ParseResult() {
							End = paramsEnd,
							Expr = Expression.Call(
								Expression.Constant(new LambdaParameterWrapper(null, LambdaParamCtx)),
								InvokeDelegateMI, 
								val.Expr, paramsExpr)
						};
						continue;
					}
				}
				break;
			} while (true);
			return val;
		}

		protected int ReadCallArguments(string expr, int start, string endLexem, List<Expression> args, Variables vars) {
			var end = start;
			do {
				var lexem = ReadLexem(expr, end);
				if (lexem.Type == LexemType.Delimiter) {
					if (lexem.GetValue() == endLexem) {
						return lexem.End;
					} else if (lexem.GetValue() == ",") {
						if (args.Count == 0) {
							throw new LambdaParserException(expr, lexem.Start, "Expected method call parameter");
						}
						end = lexem.End;
					}
				}
				// read parameter
				var paramExpr = ParseConditional(expr, end, vars);
				var argExpr = paramExpr.Expr;
				if (!(argExpr is ConstantExpression constExpr && constExpr.Value is LambdaParameterWrapper)) {
					// result may be a primitive type like bool
					argExpr = Expression.Convert(argExpr, typeof(object));
				}
				args.Add(argExpr);
				end = paramExpr.End;
			} while (true);
		}

		protected ParseResult ParseValue(string expr, int start, Variables vars) {
			var lexem = ReadLexem(expr, start);

			if (lexem.Type == LexemType.Delimiter && lexem.GetValue() == "(") {
				var groupRes = ParseConditional(expr, lexem.End, vars);
				var endLexem = ReadLexem(expr, groupRes.End);
				if (endLexem.Type != LexemType.Delimiter || endLexem.GetValue() != ")")
					throw new LambdaParserException(expr, endLexem.Start, "Expected ')'");
				groupRes.End = endLexem.End;
				return groupRes;
			} else if (lexem.Type == LexemType.NumberConstant) {
				decimal numConst;
				if (!Decimal.TryParse(lexem.GetValue(), NumberStyles.Any, CultureInfo.InvariantCulture, out numConst)) {
					throw new Exception(String.Format("Invalid number: {0}", lexem.GetValue())); 
				}
				return new ParseResult() { 
					End = lexem.End, 
					Expr = Expression.Constant(new LambdaParameterWrapper( numConst, LambdaParamCtx) ) };
			} else if (lexem.Type == LexemType.StringConstant) {
				return new ParseResult() { 
					End = lexem.End, 
					Expr = Expression.Constant( new LambdaParameterWrapper( lexem.GetValue(), LambdaParamCtx) ) };
			} else if (lexem.Type == LexemType.Name) {
				// check for predefined constants
				var val = lexem.GetValue();
				switch (val) {
					case "true":
						return new ParseResult() { End = lexem.End, Expr = Expression.Constant(new LambdaParameterWrapper(true, LambdaParamCtx) ) };
					case "false":
						return new ParseResult() { End = lexem.End, Expr = Expression.Constant(new LambdaParameterWrapper(false, LambdaParamCtx) ) };
					case "null":
						return new ParseResult() { End = lexem.End, Expr = Expression.Constant(new LambdaParameterWrapper(null, LambdaParamCtx)) };
					case "new":
						return ReadNewInstance(expr, lexem.End, vars);
				}

				// todo 
				var localVarExpr = vars.Get(val);
				return new ParseResult() { 
					End = lexem.End, 
					Expr = localVarExpr!=null ? localVarExpr : Expression.Parameter(typeof(LambdaParameterWrapper), val) };
			}
			throw new LambdaParserException(expr, start, "Expected value");
		}

		protected ParseResult ReadNewInstance(string expr, int start, Variables vars) {
			var nextLexem = ReadLexem(expr, start);
			if (nextLexem.Type == LexemType.Delimiter && nextLexem.GetValue() == "[") {
				nextLexem = ReadLexem(expr, nextLexem.End);
				if (!(nextLexem.Type == LexemType.Delimiter && nextLexem.GetValue() == "]"))
					throw new LambdaParserException(expr, nextLexem.Start, "Expected ']'");

				nextLexem = ReadLexem(expr, nextLexem.End);
				if (!(nextLexem.Type == LexemType.Delimiter && nextLexem.GetValue() == "{"))
					throw new LambdaParserException(expr, nextLexem.Start, "Expected '{'");

				var arrayArgs = new List<Expression>();
				var end = ReadCallArguments(expr, nextLexem.End, "}", arrayArgs, vars);
				var newArrExpr = Expression.NewArrayInit(typeof(object), arrayArgs );
				return new ParseResult() { 
					End = end,
					Expr = Expression.New(LambdaParameterWrapperConstructor, 
						Expression.Convert(newArrExpr, typeof(object)),
						Expression.Constant(LambdaParamCtx, typeof(LambdaParameterWrapperContext)))
				};
			}
			if (nextLexem.Type == LexemType.Name && nextLexem.GetValue().ToLower() == "dictionary") {
				nextLexem = ReadLexem(expr, nextLexem.End);
				if (!(nextLexem.Type == LexemType.Delimiter && nextLexem.GetValue() == "{"))
					throw new LambdaParserException(expr, nextLexem.Start, "Expected '{'");

				var dictionaryKeys = new List<Expression>();
				var dictionaryValues = new List<Expression>();
				do {
					nextLexem = ReadLexem(expr, nextLexem.End);
					if (!(nextLexem.Type == LexemType.Delimiter && nextLexem.GetValue() == "{"))
						throw new LambdaParserException(expr, nextLexem.Start, "Expected '{'");
					var entryArgs = new List<Expression>();
					var end = ReadCallArguments(expr, nextLexem.End, "}", entryArgs, vars);
					if (entryArgs.Count!=2)
						throw new LambdaParserException(expr, nextLexem.Start, "Dictionary entry should have exactly 2 arguments");
					
					dictionaryKeys.Add( entryArgs[0] );
					dictionaryValues.Add( entryArgs[1] );
					
					nextLexem = ReadLexem(expr, end);
				} while (nextLexem.Type == LexemType.Delimiter && nextLexem.GetValue() == ",");

				if (!(nextLexem.Type == LexemType.Delimiter && nextLexem.GetValue() == "}"))
					throw new LambdaParserException(expr, nextLexem.Start, "Expected '}'");

				var newKeysArrExpr = Expression.NewArrayInit(typeof(object), dictionaryKeys );
				var newValuesArrExpr = Expression.NewArrayInit(typeof(object), dictionaryValues );

				return new ParseResult() {
					End = nextLexem.End,
					Expr = Expression.Call(
						Expression.Constant(new LambdaParameterWrapper(null, LambdaParamCtx)),
						CreateDictionaryMI,
						newKeysArrExpr, newValuesArrExpr)
				};
			}
			throw new LambdaParserException(expr, start, "Unknown new instance initializer");
		}

		protected enum LexemType {
			Unknown,
			Name,
			Delimiter,
			StringConstant,
			NumberConstant,
			Stop
		}

		protected struct Lexem {
			public LexemType Type;
			public int Start;
			public int End;
			public string Expr;

			string rawValue;

			public string GetValue() {
				if (rawValue==null) {
					rawValue = Expr.Substring(Start, End-Start).Trim();
					if (Type==LexemType.StringConstant) {
						rawValue = rawValue.Substring(1, rawValue.Length-2).Replace( "\"\"", "\"" ); 
					}
				}
				return rawValue;
			}
		}

		protected struct ParseResult {
			public Expression Expr;
			public int End;
		}

		protected struct ParseVarResult {
			public Expression Expr;
			public int End;
			public string VarName;
			public ParameterExpression VarExpr;
		}

		public class CompiledExpression {
			public Delegate Lambda;
			public ParameterExpression[] Parameters;
		}

		protected class Variables {

			Dictionary<string, ParameterExpression> NameToVarExpr;

			public Variables(bool zeroVars = true) {
				if (!zeroVars)
					NameToVarExpr = new Dictionary<string, ParameterExpression>();
			}

			public void Define(string name, ParameterExpression varExpr) {
				NameToVarExpr[name] = varExpr;
			}

			public ParameterExpression Get(string name) {
				if (NameToVarExpr!=null && NameToVarExpr.TryGetValue(name, out var expr)) {
					return expr;
				}
				return null;
			}
		} 
	}
}
