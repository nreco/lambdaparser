using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NReco.Linq
{
	partial class LambdaParameterWrapper
	{
		public override string ToString()
		{
			return $"{Value}";
		}

		public static explicit operator decimal(LambdaParameterWrapper lpw)
		{
			return (decimal)lpw.Value;
		}
		public static explicit operator double(LambdaParameterWrapper lpw)
		{
			return (double)lpw.Value;
		}
		public static explicit operator long(LambdaParameterWrapper lpw)
		{
			return (long)lpw.Value;
		}
		public static explicit operator int(LambdaParameterWrapper lpw)
		{
			return (int)lpw.Value;
		}
		public static explicit operator byte(LambdaParameterWrapper lpw)
		{
			return (byte)lpw.Value;
		}
		public static explicit operator char(LambdaParameterWrapper lpw)
		{
			return (char)lpw.Value;
		}
		//public static explicit operator string(LambdaParameterWrapper lpw)
		//{
		//	return (string)lpw.Value;
		//}
		public static implicit operator string(LambdaParameterWrapper lpw)
		{
			return lpw.Value as string;
		}
	}
}
