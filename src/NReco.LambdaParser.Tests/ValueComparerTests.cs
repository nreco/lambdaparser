using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace NReco.Linq.Tests {

	[TestFixture]
	public class ValueComparerTests {

		[Test]
		public void DefaultBehaviour() {

			var cmp = ValueComparer.Instance;

			Assert.AreEqual(cmp.Compare(1, 1M), 0);
			Assert.AreEqual(cmp.Compare("Test", "Test"), 0);
			Assert.AreEqual(cmp.Compare(new object[] { 1, "A", 3 }, new object[] { 1, "A", 3 }), 0);
			Assert.AreEqual(cmp.Compare(true, "True"), 0);
			Assert.AreEqual(cmp.Compare(true, 1), 0);
			Assert.AreEqual(cmp.Compare(false, 0), 0);

			Assert.AreEqual(cmp.Compare(1, 2), -1);
			Assert.AreEqual(cmp.Compare(2, 1), 1);
			Assert.AreEqual(cmp.Compare("a", "b"), -1);

			Assert.AreEqual(cmp.Compare(new [] { 1, 2 }, new [] { 1, 2, 3 }), -1);
			Assert.AreEqual(cmp.Compare(new[] { 1, 4 }, new[] { 1, 3 }), 1);

			Assert.Throws<FormatException>(() => { Assert.AreEqual(cmp.Compare(5, "a").HasValue, false); });
			Assert.AreEqual(cmp.Compare(5, null), 1);
			Assert.AreEqual(cmp.Compare(null, 0), -1);
			Assert.AreEqual(cmp.Compare(null, null), 0);  // two nulls are equal like in C#
		}

		[Test]
		public void SuppressErrorsBehaviour() {
			var cmp = new ValueComparer();
			cmp.SuppressErrors = true;

			Assert.AreEqual(cmp.Compare(5, "a").HasValue, false);
		}

		[Test]
		public void SqlNullComparisonBehaviour() {
			var cmp = new ValueComparer();
			cmp.NullComparison = ValueComparer.NullComparisonMode.Sql;

			Assert.AreEqual(cmp.Compare(null, 0).HasValue, false);
			Assert.AreEqual(cmp.Compare("A", null).HasValue, false);
			Assert.AreEqual(cmp.Compare(null, null).HasValue, false);
		}

	}
}
