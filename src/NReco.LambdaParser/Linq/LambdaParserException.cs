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
using System.Linq;
using System.Text;

namespace NReco.Linq {
	
	/// <summary>
	/// The exception that is thrown when lambda expression parse error occurs
	/// </summary>
	public class LambdaParserException : Exception {

		/// <summary>
		/// Lambda expression
		/// </summary>
		public string Expression { get; private set; }

		/// <summary>
		/// Parser position where syntax error occurs 
		/// </summary>
		public int Index { get; private set; }

		public LambdaParserException(string expr, int idx, string msg)
			: base( String.Format("{0} at {1}: {2}", msg, idx, expr) ) {
			Expression = expr;
			Index = idx;
		}
	}
}
