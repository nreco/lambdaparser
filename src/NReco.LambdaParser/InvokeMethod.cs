#region License
/*
 * NReco Lambda Parser (http://www.nrecosite.com/)
 * Copyright 2014-2016 Vitaliy Fedorchenko
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
using System.Text;
using System.Reflection;

namespace NReco.Linq {
	
	/// <summary>
	/// Invoke object's method that is most compatible with provided arguments
	/// </summary>
	public class InvokeMethod : IInvokeMethod {

		internal readonly static InvokeMethod _Instance = new InvokeMethod();

		public static Linq.IInvokeMethod Instance => _Instance;

		protected MethodInfo FindMethod(object targetObject, string methodName, Type[] argTypes) {
			if (targetObject is Type) {
				// static method
				return ((Type)targetObject).GetRuntimeMethod(methodName, argTypes);
			}
			return targetObject.GetType().GetRuntimeMethod(methodName, argTypes);
		}

		protected IEnumerable<MethodInfo> GetAllMethods(object targetObject) {
			if (targetObject is Type) {
				return ((Type)targetObject).GetRuntimeMethods();
			}
			return targetObject.GetType().GetRuntimeMethods();
		}

		public object Invoke(object targetObject, string methodName, object[] args) {
			Type[] argTypes = new Type[args.Length];
			for (int i = 0; i < argTypes.Length; i++)
				argTypes[i] = args[i] != null ? args[i].GetType() : typeof(object);

			// strict matching first
			MethodInfo targetMethodInfo = FindMethod(targetObject, methodName, argTypes);
			// fuzzy matching
			if (targetMethodInfo==null) {
				var methods = GetAllMethods(targetObject);

				foreach (var m in methods)
					if (m.Name==methodName &&
						m.GetParameters().Length == args.Length &&
						CheckParamsCompatibility(m.GetParameters(), argTypes, args)) {
						targetMethodInfo = m;
						break;
					}
			}
			if (targetMethodInfo == null) {
				string[] argTypeNames = new string[argTypes.Length];
				for (int i=0; i<argTypeNames.Length; i++)
					argTypeNames[i] = argTypes[i].Name;
				string argTypeNamesStr = String.Join(",",argTypeNames);
				throw new MissingMemberException(
						(targetObject is Type ? (Type)targetObject : targetObject.GetType()).FullName+"."+methodName );
			}
			object[] argValues = PrepareActualValues(methodName,targetMethodInfo.GetParameters(),args);
			object res = null;
			try {
				res = targetMethodInfo.Invoke( targetObject is Type ? null : targetObject, argValues);
			} catch (TargetInvocationException tiEx) {
				if (tiEx.InnerException!=null)
					throw new Exception(tiEx.InnerException.Message, tiEx.InnerException);
				else {
					throw;
				}
			}
			return res;
		}

		internal static bool IsInstanceOfType(Type t, object val) {
			return val!=null && t.GetTypeInfo().IsAssignableFrom(val.GetType().GetTypeInfo());
		}

		protected bool CheckParamsCompatibility(ParameterInfo[] paramsInfo, Type[] types, object[] values) {
			for (int i=0; i<paramsInfo.Length; i++) {
				Type paramType = paramsInfo[i].ParameterType;
				var val = values[i];
				if (IsInstanceOfType(paramType, val))
					continue;
				// null and reference types
				if (val==null && !paramType.GetTypeInfo().IsValueType)
					continue;
				// possible autocast between generic/non-generic common types
				try {
					Convert.ChangeType(val, paramType, System.Globalization.CultureInfo.InvariantCulture);
					continue;
				} catch { }
				//if (ConvertManager.CanChangeType(types[i],paramType))
				//	continue;
				// incompatible parameter
				return false;
			}
			return true;
		}


		protected object[] PrepareActualValues(string methodName, ParameterInfo[] paramsInfo, object[] values) {
			object[] res = new object[paramsInfo.Length];
			for (int i=0; i<paramsInfo.Length; i++) {
				if (values[i]==null || IsInstanceOfType( paramsInfo[i].ParameterType, values[i])) {
					res[i] = values[i];
					continue;
				}
				try {
					res[i] = Convert.ChangeType( values[i], paramsInfo[i].ParameterType, System.Globalization.CultureInfo.InvariantCulture );
					continue;
				} catch { 
					throw new InvalidCastException( 
						String.Format("Invoke method '{0}': cannot convert argument #{1} from {2} to {3}",
							methodName, i, values[i].GetType(), paramsInfo[i].ParameterType));
				}
			}
			return res;
		}


	}

}
