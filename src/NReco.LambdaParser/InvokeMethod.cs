#region License
/*
 * NReco Lambda Parser (http://www.nrecosite.com/)
 * Copyright 2014-2016 Vitaliy Fedorchenko
 * Distributed under the LGPL licence
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

namespace NReco {
	
	/// <summary>
	/// Invoke object's method that is most compatible with provided arguments
	/// </summary>
	internal class InvokeMethod {

		public object TargetObject { get; set; }

		public string MethodName { get; set; }

		public InvokeMethod(object o, string methodName) {
			TargetObject = o;
			MethodName = methodName;
		}

		protected MethodInfo FindMethod(Type[] argTypes) {
			if (TargetObject is Type) {
				// static method
				#if NET40
				return ((Type)TargetObject).GetMethod(MethodName, BindingFlags.Static | BindingFlags.Public);
				#else
				return ((Type)TargetObject).GetRuntimeMethod(MethodName, argTypes);
				#endif
			}
			#if NET40
			return TargetObject.GetType().GetMethod(MethodName, argTypes);
			#else
			return TargetObject.GetType().GetRuntimeMethod(MethodName, argTypes);
			#endif
		}

		protected IEnumerable<MethodInfo> GetAllMethods() {
			if (TargetObject is Type) {
				#if NET40
				return ((Type)TargetObject).GetMethods(BindingFlags.Static | BindingFlags.Public);
				#else
				return ((Type)TargetObject).GetRuntimeMethods();
				#endif
			}
			#if NET40
			return TargetObject.GetType().GetMethods();
			#else
			return TargetObject.GetType().GetRuntimeMethods();
			#endif
		}

		public object Invoke(object[] args) {
			Type[] argTypes = new Type[args.Length];
			for (int i = 0; i < argTypes.Length; i++)
				argTypes[i] = args[i] != null ? args[i].GetType() : typeof(object);

			// strict matching first
			MethodInfo targetMethodInfo = FindMethod(argTypes);
			// fuzzy matching
			if (targetMethodInfo==null) {
				var methods = GetAllMethods();

				foreach (var m in methods)
					if (m.Name==MethodName &&
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
						(TargetObject is Type ? (Type)TargetObject : TargetObject.GetType()).FullName+"."+MethodName );
			}
			object[] argValues = PrepareActualValues(targetMethodInfo.GetParameters(),args);
			object res = null;
			try {
				res = targetMethodInfo.Invoke( TargetObject is Type ? null : TargetObject, argValues);
			} catch (TargetInvocationException tiEx) {
				if (tiEx.InnerException!=null)
					throw new Exception(tiEx.InnerException.Message, tiEx.InnerException);
				else {
					throw;
				}
			}
			return res;
		}

		private bool IsInstanceOfType(Type t, object val) {
			#if NET40 
			return t.IsInstanceOfType(val);
			#else
			return val!=null && t.GetTypeInfo().IsAssignableFrom(val.GetType().GetTypeInfo());
			#endif
		}

		protected bool CheckParamsCompatibility(ParameterInfo[] paramsInfo, Type[] types, object[] values) {
			for (int i=0; i<paramsInfo.Length; i++) {
				Type paramType = paramsInfo[i].ParameterType;
				var val = values[i];
				if (IsInstanceOfType(paramType, val))
					continue;
				// null and reference types
				if (val==null && 
					#if NET40
					!paramType.IsValueType
					#else 
					!paramType.GetTypeInfo().IsValueType
					#endif
				)
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


		protected object[] PrepareActualValues(ParameterInfo[] paramsInfo, object[] values) {
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
							MethodName, i, values[i].GetType(), paramsInfo[i].ParameterType));
				}
			}
			return res;
		}


	}

}
