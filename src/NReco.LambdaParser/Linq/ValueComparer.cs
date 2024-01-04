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
using System.Linq;
using System.Text;
using System.Reflection;

namespace NReco.Linq {

	/// <summary>
	/// Generic "by value" comparer that uses IComparable and can compare arrays/lists.
	/// </summary>
	public class ValueComparer : IComparer, IComparer<object>, IValueComparer {

		internal readonly static ValueComparer _Instance = new ValueComparer();
		
		public static IValueComparer Instance {
			get {
				return _Instance;
			}
		}

		/// <summary>
		/// Gets or sets format provider used for Convert.ChangeType (InvariantCulture by default).
		/// </summary>
		public IFormatProvider FormatProvider { get; set; }

		/// <summary>
		/// Determines how ValueComparer handles comparison exceptions (by default is false: convert exceptions are thrown).
		/// </summary>
		public bool SuppressErrors { get; set; }

		/// <summary>
		/// Determines how ValueComparer handles comparison with nulls (default is "MinValue" mode).
		/// </summary>
		public NullComparisonMode NullComparison { get; set; }

		public ValueComparer() {
			FormatProvider = System.Globalization.CultureInfo.InvariantCulture;
			SuppressErrors = false;
		}

		private bool IsAssignableFrom(Type a, Type b) {
			return a.GetTypeInfo().IsAssignableFrom(b.GetTypeInfo() );
		}

		int IComparer.Compare(object a, object b) {
			var res = CompareInternal(a, b);
			if (!res.HasValue)
				throw new ArgumentException(String.Format("Cannot compare {0} and {1}", a.GetType(), b.GetType()));
			return res.Value;
		}

		int IComparer<object>.Compare(object a, object b) {
			return ((IComparer)this).Compare(a, b);
		}

		public int? Compare(object a, object b) {
			try {
				if (NullComparison == NullComparisonMode.Sql)
					if (a == null || b == null)
						return null;
				return CompareInternal(a, b);
			} catch (Exception ex) {
				if (SuppressErrors)
					return null;
				throw;
			}
		}

		protected virtual int? CompareInternal(object a, object b) {
			if (a == null && b == null)
				return 0;
			if (a == null && b != null)
				return -1;
			if (a != null && b == null)
				return 1;

			if ((a is IList) && (b is IList)) {
				IList aList = (IList)a;
				IList bList = (IList)b;
				if (aList.Count < bList.Count)
					return -1;
				if (aList.Count > bList.Count)
					return +1;
				for (int i = 0; i < aList.Count; i++) {
					int? r = Compare(aList[i], bList[i]);
					if (!r.HasValue || r != 0)
						return r;
				}
				// lists are equal
				return 0;
			}
			// test for quick compare if a type is assignable from b
			if (a is IComparable) {
				var aComp = (IComparable)a;
				// quick compare if types are fully compatible
				if (IsAssignableFrom( a.GetType(), b.GetType() ))
					return aComp.CompareTo(b);
			}
			if (b is IComparable) {
				var bComp = (IComparable)b;
				// quick compare if types are fully compatible
				if (IsAssignableFrom( b.GetType(), a.GetType() ))
					return -bComp.CompareTo(a);
			}

			// try to convert b to a and then compare
			if (a is IComparable) {
				var aComp = (IComparable)a;
				var bConverted = Convert.ChangeType(b, a.GetType(), FormatProvider);
				return aComp.CompareTo(bConverted);
			}
			// try to convert a to b and then compare
			if (b is IComparable) {
				var bComp = (IComparable)b;
				var aConverted =  Convert.ChangeType(a, b.GetType(), FormatProvider);
				return -bComp.CompareTo(aConverted);
			}

			return null;
		}

		public enum NullComparisonMode {

			/// <summary>
			/// Null compared as "MinValue" (affects less-than and greater-than comparisons). 
			/// </summary>
			/// <remarks>This is default behaviour expected for <see cref="IComparer"/> and described in MSDN.</remarks>
			MinValue = 0,

			/// <summary>
			/// Null cannot be compared to any other value (even if it is null). This is SQL-like nulls handling.
			/// </summary>
			Sql = 1,
		}

	}

}
