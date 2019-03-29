using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace NReco.Linq.Tests {

	public class ValueComparerTests {

		[Fact]
		public void DefaultBehaviour() {

			var cmp = ValueComparer.Instance;

			Assert.Equal(cmp.Compare(1, 1M), 0);
			Assert.Equal(cmp.Compare("Test", "Test"), 0);
			Assert.Equal(cmp.Compare(new object[] { 1, "A", 3 }, new object[] { 1, "A", 3 }), 0);
			Assert.Equal(cmp.Compare(true, "True"), 0);
			Assert.Equal(cmp.Compare(true, 1), 0);
			Assert.Equal(cmp.Compare(false, 0), 0);

			Assert.Equal(cmp.Compare(1, 2), -1);
			Assert.Equal(cmp.Compare(2, 1), 1);
			Assert.Equal(cmp.Compare("a", "b"), -1);

			Assert.Equal(cmp.Compare(new [] { 1, 2 }, new [] { 1, 2, 3 }), -1);
			Assert.Equal(cmp.Compare(new[] { 1, 4 }, new[] { 1, 3 }), 1);

			Assert.Throws<FormatException>(() => { Assert.Equal(cmp.Compare(5, "a").HasValue, false); });
			Assert.Equal(cmp.Compare(5, null), 1);
			Assert.Equal(cmp.Compare(null, 0), -1);
			Assert.Equal(cmp.Compare(null, null), 0);  // two nulls are equal like in C#

			Assert.Equal(cmp.Compare(new DateTime(), new DateTime().AddDays(1)), -1);
			Assert.Equal(cmp.Compare(new DateTime().AddDays(1), new DateTime()), 1);
			Assert.Equal(cmp.Compare(new DateTime(), new DateTime()), 0);

			Assert.Equal(cmp.Compare(new TimeSpan(), new TimeSpan()), 0);
			Assert.Equal(cmp.Compare(new TimeSpan(), new TimeSpan(0,0,1)), -1);
			Assert.Equal(cmp.Compare(new TimeSpan(0,0,1), new TimeSpan()), 1);
		}

		[Fact]
		public void SuppressErrorsBehaviour() {
			var cmp = new ValueComparer();
			cmp.SuppressErrors = true;

			Assert.Equal(cmp.Compare(5, "a").HasValue, false);
		}

		[Fact]
		public void SqlNullComparisonBehaviour() {
			var cmp = new ValueComparer();
			cmp.NullComparison = ValueComparer.NullComparisonMode.Sql;

			Assert.Equal(cmp.Compare(null, 0).HasValue, false);
			Assert.Equal(cmp.Compare("A", null).HasValue, false);
			Assert.Equal(cmp.Compare(null, null).HasValue, false);
		}

	}
}
