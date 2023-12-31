#region License
/*
 * NReco Lambda Parser (http://www.nrecosite.com/)
 * Copyright 2014-2017 Vitaliy Fedorchenko
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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NReco.Linq {

	/// <summary>
	/// Exposes a method that compares two objects.
	/// </summary>
	/// <remarks>
	/// Unlike <see cref="System.Collections.IComparer"/> this interface allows to return null as comparison result 
	/// for case when values cannot be compared without throwing an exception.
	/// </remarks>
	public interface IValueComparer {

		/// <summary>
		/// Compares two objects and returns a value indicating whether one is less than, equal to, or greater than the other.
		/// </summary>
		/// <returns>A signed integer that indicates the relative values of x and y or null if values cannot be compared.</returns>
		int? Compare(object x, object y);
	}

}
