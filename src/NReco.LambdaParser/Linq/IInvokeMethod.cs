using System;
using System.Collections.Generic;
using System.Text;

namespace NReco.Linq {

	/// <summary>
	/// Exposes a method that allows the invoke of a method within an object
	/// </summary>
	/// <remarks>
	/// Interface to allow different implimentations of invoke method with different capabilities.
	/// ensures backwards compatibility and behavour.
	/// </remarks>
	public interface IInvokeMethod {

		/// <summary>
		/// Invokes a method within an object (targetobject), given a set of arguments / parameters passed to method
		/// </summary>
		/// <returns>An object reference to the return value of the method</returns>
		object Invoke(object targetObject, string methodName, object[] args);

	}
}
