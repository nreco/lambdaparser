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

namespace NReco {
	
	/// <summary>
	/// Invoke object's method that is most compatible with provided arguments
	/// </summary>
	internal class InvokeMethod {

		//public object TargetObject { get; set; }

		//public string MethodName { get; set; }

		//public InvokeMethod(object o, string methodName) {
		//	TargetObject = o;
		//	MethodName = methodName;
		//}

		protected MethodInfo FindMethod(object TargetObject, string MethodName, Type[] argTypes) {
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

		protected IEnumerable<MethodInfo> GetAllMethods(object TargetObject) {
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


		private int OptionalParameterCount(ParameterInfo[] parameters) {
			var cnt = 0;
			for (int i = parameters.Length - 1; i >= 0; i--) {
				if (parameters[i].IsOptional) cnt++; else break;
			}
			return cnt;
		}


        public object Invoke(object TargetObject, string MethodName, object[] args) {
			Type[] argTypes = new Type[args.Length];
			for (int i = 0; i < argTypes.Length; i++)
				argTypes[i] = args[i] != null ? args[i].GetType() : typeof(object);

			// strict matching first
			MethodInfo targetMethodInfo = FindMethod(TargetObject, MethodName, argTypes);
			// fuzzy matching
			if (targetMethodInfo==null) {
				var methods = GetAllMethods(TargetObject);
				foreach (var m in methods) {
					if (m.Name == MethodName) {
						var para = m.GetParameters();
						var paracnt = para.Length;
						var optcnt = OptionalParameterCount(para);
						var mincnt = paracnt - optcnt;
						var hasParamArray = para[para.Length - 1].GetCustomAttribute<ParamArrayAttribute>() != null;
						if ((args.Length >= mincnt && (args.Length <= paracnt || hasParamArray)) &&	CheckParamsCompatibility(para, argTypes, args)) {
							targetMethodInfo = m;
							break;
						}
					}
				}	
			}
			if (targetMethodInfo == null) {
				string[] argTypeNames = new string[argTypes.Length];
				for (int i=0; i<argTypeNames.Length; i++)
					argTypeNames[i] = argTypes[i].Name;
				string argTypeNamesStr = String.Join(",",argTypeNames);
				throw new MissingMemberException(
						(TargetObject is Type ? (Type)TargetObject : TargetObject.GetType()).FullName+"."+MethodName);
			}
			object[] argValues = PrepareActualValues(MethodName,targetMethodInfo.GetParameters(),args);
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

		internal static bool IsInstanceOfType(Type t, object val) {
			#if NET40 
			return t.IsInstanceOfType(val);
			#else
			return val!=null && t.GetTypeInfo().IsAssignableFrom(val.GetType().GetTypeInfo());
			#endif
		}


		private bool CheckParamValueCompatibility(Type paramType, object val)
		{
			if (IsInstanceOfType(paramType, val))
				return true;
			// null and reference types
			if (val == null &&
				#if NET40
					!paramType.IsValueType
				#else
					!paramType.GetTypeInfo().IsValueType
				#endif
				)
				return true;
			// possible autocast between generic/non-generic common types
			try {
				Convert.ChangeType(val, paramType, System.Globalization.CultureInfo.InvariantCulture);
				return true;
			}
			catch { }
			//if (ConvertManager.CanChangeType(types[i],paramType))
			//	continue;
			// incompatible parameter
			return false;
		}

		protected bool CheckParamsCompatibility(ParameterInfo[] paramsInfo, Type[] types, object[] values) {
			var valueslen = values.Length;
			for (int i=0; i<paramsInfo.Length; i++) {
				ParameterInfo paramInfo = paramsInfo[i];
				bool isParamArray = paramInfo.GetCustomAttribute<ParamArrayAttribute>() != null;
				Type paramType = isParamArray ? paramInfo.ParameterType.GetElementType() : paramInfo.ParameterType;				
				if (i<valueslen) {
					if (isParamArray) {
						//ParamArray is always last parameter, so check all remaining values
						for (int j = i; j < valueslen; j++)	{
							if (!CheckParamValueCompatibility(paramType, values[j])) return false;
						}
					}
					else {
						if (!CheckParamValueCompatibility(paramType, values[i])) return false;
					}
                }
				else {
					if (!paramsInfo[i].IsOptional) return false;
				}
			}
			return true;
		}

		private object PrepareActualValue(Type paramType, object value) {
			if (value == null || IsInstanceOfType(paramType, value)) {
				return value;
			}
			return Convert.ChangeType(value, paramType, System.Globalization.CultureInfo.InvariantCulture);
		}

		protected object[] PrepareActualValues(string MethodName, ParameterInfo[] paramsInfo, object[] values) {
			object[] res = new object[paramsInfo.Length];
			var valueslen = values.Length;
			for (int i=0; i<paramsInfo.Length; i++) {
				ParameterInfo paramInfo = paramsInfo[i];
				bool isParamArray = paramInfo.GetCustomAttribute<ParamArrayAttribute>() != null;
				Type paramType = isParamArray ? paramInfo.ParameterType.GetElementType() : paramInfo.ParameterType;
				if (i < valueslen) {
					try {
						if (isParamArray) {
							//ParamArray is always last parameter, so prepare all remaining values into object array
							object[] Params = (object[])Activator.CreateInstance(paramInfo.ParameterType, new object[] { valueslen - i });
							int pc = 0;
							for (int j = i; j < valueslen; j++)	{
								Params[pc] = PrepareActualValue(paramType, values[j]);
								pc++;
							}
							res[i] = Params;
						}
						else {
							res[i] = PrepareActualValue(paramType, values[i]);
						}
					}
					catch (Exception) {
						throw new InvalidCastException(
							String.Format("Invoke method '{0}': cannot convert argument #{1} from {2} to {3}",
								MethodName, i, values[i].GetType(), paramsInfo[i].ParameterType));
					}			
				}
				else {
					if (paramsInfo[i].IsOptional) {
						res[i] = paramsInfo[i].DefaultValue;
					}
					else throw new ArgumentException($"Invoke method '{MethodName}': Missing argument {paramsInfo[i].Name} not optional");
				}
			}
			return res;
		}


	}

}
