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
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Globalization;

namespace NReco.Linq {

	/// <summary>
	/// This is wrapper that makes runtime types conversions real.
	/// </summary>
	internal sealed class LambdaParameterWrapper : IComparable, ILambdaValue {
		object _Value;
		LambdaParameterWrapperContext Ctx;

		public object Value {
			get { return _Value; }
		}

		public LambdaParameterWrapper(object val, LambdaParameterWrapperContext ctx) {
			Ctx = ctx;
			if (val is LambdaParameterWrapper)
				_Value = ((LambdaParameterWrapper)val).Value; // unwrap
			else if (val is object[]) {
				var objArr = (object[])val;
				for (int i=0; i<objArr.Length; i++)
					if (objArr[i] is LambdaParameterWrapper)
						objArr[i] = ((LambdaParameterWrapper)objArr[i]).Value;
				_Value = val;
			} else {
				_Value = val;
			}
		}

		public int CompareTo(object obj) {
			var objResolved = obj is LambdaParameterWrapper ? ((LambdaParameterWrapper)obj).Value : obj;
			var cmpRes = Ctx.Cmp.Compare(Value, objResolved);
			if (!cmpRes.HasValue)
				throw new ArgumentException();
			return cmpRes.Value;
		}

		public bool IsTrue {
			get {
				return Ctx.Cmp.Compare(Value, true)==0;
			}
		}

		public LambdaParameterWrapper CreateDictionary(object[] keys, object[] values) {			
			if (keys.Length!=values.Length)
				throw new ArgumentException();
			var d = new Dictionary<object,object>();
			for (int i = 0; i < keys.Length; i++) { 
				var k = keys[i];
				var v = values[i];
				// unwrap
				if (k is LambdaParameterWrapper)
					k = ((LambdaParameterWrapper)k).Value;
				if (v is LambdaParameterWrapper)
					v = ((LambdaParameterWrapper)v).Value;
				d[k] = v;
			}
			return new LambdaParameterWrapper(d, Ctx);
		}

		public LambdaParameterWrapper InvokeMethod(object obj, string methodName, object[] args) {
			if (obj is LambdaParameterWrapper)
				obj = ((LambdaParameterWrapper)obj).Value;

			if (obj == null)
				throw new NullReferenceException(String.Format("Method {0} target is null", methodName));

			var argsResolved = new object[args.Length];
			for (int i = 0; i < args.Length; i++)
				argsResolved[i] = args[i] is LambdaParameterWrapper ? ((LambdaParameterWrapper)args[i]).Value : args[i];

			var res = Ctx.Inv.Invoke(obj,methodName,argsResolved);
			return new LambdaParameterWrapper(res, Ctx);
		}

		public LambdaParameterWrapper InvokeDelegate(object obj, object[] args) {
			if (obj is LambdaParameterWrapper)
				obj = ((LambdaParameterWrapper)obj).Value;
			if (obj == null)
				throw new NullReferenceException("Delegate is null");
			if (!(obj is Delegate))
				throw new NullReferenceException(String.Format("{0} is not a delegate", obj.GetType()));
			var deleg = (Delegate)obj;

			var delegParams = deleg.GetMethodInfo().GetParameters();
			if (delegParams.Length != args.Length)
				throw new TargetParameterCountException(
					String.Format("Target delegate expects {0} parameters", delegParams.Length));

			var resolvedArgs = new object[args.Length];
			for (int i = 0; i < resolvedArgs.Length; i++) {
				var argObj = args[i] is LambdaParameterWrapper ? ((LambdaParameterWrapper)args[i]).Value : args[i];
				//Static method call below, what should we do with this? Perhaps we should move it somewhere else
				if (!NReco.Linq.InvokeMethod.IsInstanceOfType(delegParams[i].ParameterType, argObj))
					argObj = Convert.ChangeType(argObj, delegParams[i].ParameterType, CultureInfo.InvariantCulture);
				resolvedArgs[i] = argObj;
			}
			return new LambdaParameterWrapper( deleg.DynamicInvoke(resolvedArgs), Ctx );
		}

		public LambdaParameterWrapper InvokePropertyOrField(object obj, string propertyName) {
			if (obj is LambdaParameterWrapper)
				obj = ((LambdaParameterWrapper)obj).Value;

			if (obj == null)
				throw new NullReferenceException(String.Format("Property or field {0} target is null", propertyName));
            
			PropertyInfo prop;
			try {
				prop = obj.GetType().GetRuntimeProperty(propertyName);
			}
			//Below covers an issue caused by properties declared in base classes with different signitures
			//in these cases an AmbiguousMatchException is thrown
			//if this happens then we look for the first match by propertyName since this
			//seems to be the one from the decendant class.
			catch (System.Reflection.AmbiguousMatchException) {
				prop = obj.GetType().GetRuntimeProperties().FirstOrDefault(rp=>rp.Name == propertyName);
			}

			if (prop != null) {
				var propVal = prop.GetValue(obj, null);
				return new LambdaParameterWrapper(propVal, Ctx);
			}
			var fld = obj.GetType().GetRuntimeField(propertyName);
			if (fld != null) {
				var fldVal = fld.GetValue(obj);
				return new LambdaParameterWrapper(fldVal, Ctx);
			}
			throw new MissingMemberException(obj.GetType().ToString()+"."+propertyName);
		}

		public LambdaParameterWrapper InvokeIndexer(object obj, object[] args) {
			if (obj == null)
				throw new NullReferenceException(String.Format("Indexer target is null"));
			if (obj is LambdaParameterWrapper)
				obj = ((LambdaParameterWrapper)obj).Value;

			var argsResolved = new object[args.Length];
			for (int i = 0; i < args.Length; i++)
				argsResolved[i] = args[i] is LambdaParameterWrapper ? ((LambdaParameterWrapper)args[i]).Value : args[i];

			if (obj is Array) {
				var objArr = (Array)obj;
				if (objArr.Rank != args.Length) {
					throw new RankException(String.Format("Array rank ({0}) doesn't match number of indicies ({1})",
						objArr.Rank, args.Length));
				}
				var indicies = new int[argsResolved.Length];
				for (int i = 0; i < argsResolved.Length; i++)
					indicies[i] = Convert.ToInt32(argsResolved[i]);

				var res = objArr.GetValue(indicies);
				return new LambdaParameterWrapper(res, Ctx);
			} else {
				// indexer method
				var res = Ctx.Inv.Invoke(obj,"get_Item",argsResolved);
				return new LambdaParameterWrapper(res, Ctx);
			}
		}

		public static LambdaParameterWrapper operator +(LambdaParameterWrapper c1, LambdaParameterWrapper c2) {
			if (c1.Value is string || c2.Value is string) {
				return new LambdaParameterWrapper( Convert.ToString(c1.Value) + Convert.ToString(c2.Value), c1.Ctx);
			} 

			if (c1.Value is TimeSpan c1TimeSpan && c2.Value is DateTime c2DateTime) 
			{
				return new LambdaParameterWrapper(c2DateTime.Add(c1TimeSpan), c1.Ctx);
			}

			if (c1.Value is DateTime c1DateTime && c2.Value is TimeSpan c2TimeSpan) 
			{
				return new LambdaParameterWrapper(c1DateTime.Add(c2TimeSpan), c1.Ctx);
			}

			if (c1.Value is TimeSpan c1ts && c2.Value is TimeSpan c2ts)
			{
				return new LambdaParameterWrapper(c1ts + c2ts, c1.Ctx);
			}

			var c1decimal = Convert.ToDecimal(c1.Value, CultureInfo.InvariantCulture);
			var c2decimal = Convert.ToDecimal(c2.Value,  CultureInfo.InvariantCulture);
			return new LambdaParameterWrapper(c1decimal + c2decimal, c1.Ctx);
		}

		public static LambdaParameterWrapper operator -(LambdaParameterWrapper c1, LambdaParameterWrapper c2) {
			if (c1.Value is TimeSpan c1ts && c2.Value is TimeSpan c2ts)
			{
				return new LambdaParameterWrapper(c1ts - c2ts, c1.Ctx);
			}

			if (c1.Value is DateTime c1dt && c2.Value is DateTime c2dt)
			{
				return new LambdaParameterWrapper(c1dt - c2dt, c1.Ctx);
			}

			if (c1.Value is DateTime c1DateTime && c2.Value is TimeSpan c2TimeSpan) 
			{
				return new LambdaParameterWrapper(c1DateTime.Add(c2TimeSpan.Negate()), c1.Ctx);
			}

			var c1decimal = Convert.ToDecimal(c1.Value, CultureInfo.InvariantCulture);
			var c2decimal = Convert.ToDecimal(c2.Value, CultureInfo.InvariantCulture);
			return new LambdaParameterWrapper(c1decimal - c2decimal, c1.Ctx);
		}

		public static LambdaParameterWrapper operator -(LambdaParameterWrapper c1) {
			if(c1.Value is TimeSpan ts)
			{
				return new LambdaParameterWrapper(ts.Negate(), c1.Ctx);
			}

			var c1decimal = Convert.ToDecimal(c1.Value, CultureInfo.InvariantCulture);
			return new LambdaParameterWrapper(-c1decimal, c1.Ctx);
		}

		public static LambdaParameterWrapper operator *(LambdaParameterWrapper c1, LambdaParameterWrapper c2) {
			var c1decimal = Convert.ToDecimal(c1.Value, CultureInfo.InvariantCulture);
			var c2decimal = Convert.ToDecimal(c2.Value, CultureInfo.InvariantCulture);
			return new LambdaParameterWrapper(c1decimal * c2decimal, c1.Ctx);
		}

		public static LambdaParameterWrapper operator /(LambdaParameterWrapper c1, LambdaParameterWrapper c2) {
			var c1decimal = Convert.ToDecimal(c1.Value, CultureInfo.InvariantCulture);
			var c2decimal = Convert.ToDecimal(c2.Value, CultureInfo.InvariantCulture);
			return new LambdaParameterWrapper(c1decimal / c2decimal, c1.Ctx);
		}

		public static LambdaParameterWrapper operator %(LambdaParameterWrapper c1, LambdaParameterWrapper c2) {
			var c1decimal = Convert.ToDecimal(c1.Value, CultureInfo.InvariantCulture);
			var c2decimal = Convert.ToDecimal(c2.Value, CultureInfo.InvariantCulture);
			return new LambdaParameterWrapper(c1decimal % c2decimal, c1.Ctx);
		}

		public static bool operator ==(LambdaParameterWrapper c1, LambdaParameterWrapper c2) {
			return c1.Ctx.Cmp.Compare(c1.Value, c2.Value)==0;
		}
		public static bool operator ==(LambdaParameterWrapper c1, bool c2) {
			return c1.Ctx.Cmp.Compare(c1.Value, c2)==0;
		}
		public static bool operator ==(bool c1, LambdaParameterWrapper c2) {
			return c2.Ctx.Cmp.Compare(c1, c2.Value)==0;
		}

		public static bool operator !=(LambdaParameterWrapper c1, LambdaParameterWrapper c2) {
			return c1.Ctx.Cmp.Compare(c1.Value, c2.Value)!=0;
		}
		public static bool operator !=(LambdaParameterWrapper c1, bool c2) {
			return c1.Ctx.Cmp.Compare(c1.Value, c2)!=0;
		}
		public static bool operator !=(bool c1, LambdaParameterWrapper c2) {
			return c2.Ctx.Cmp.Compare(c1, c2.Value)!=0;
		}

		public static bool operator >(LambdaParameterWrapper c1, LambdaParameterWrapper c2) {
			return c1.Ctx.Cmp.Compare(c1.Value, c2.Value)>0;
		}
		public static bool operator <(LambdaParameterWrapper c1, LambdaParameterWrapper c2) {
			return c1.Ctx.Cmp.Compare(c1.Value, c2.Value) < 0;
		}

		public static bool operator >=(LambdaParameterWrapper c1, LambdaParameterWrapper c2) {
			return c1.Ctx.Cmp.Compare(c1.Value, c2.Value)>= 0;
		}
		public static bool operator <=(LambdaParameterWrapper c1, LambdaParameterWrapper c2) {
			return c1.Ctx.Cmp.Compare(c1.Value, c2.Value)<=0;
		}

		public static LambdaParameterWrapper operator !(LambdaParameterWrapper c1) {
			var c1bool = c1.Ctx.Cmp.Compare(c1.Value, true)==0;
			return new LambdaParameterWrapper( !c1bool, c1.Ctx);
		}

		public static bool operator true(LambdaParameterWrapper x) {
			return x.IsTrue;
		}

		public static bool operator false(LambdaParameterWrapper x) {
			return !x.IsTrue;
		}

	}

	internal sealed class LambdaParameterWrapperContext {
		internal IValueComparer Cmp { get; private set; }
		internal IInvokeMethod Inv { get; private set; }

		internal LambdaParameterWrapperContext(IValueComparer cmp, IInvokeMethod inv) {
			Cmp = cmp;
			Inv = inv;
		}

	}


}
