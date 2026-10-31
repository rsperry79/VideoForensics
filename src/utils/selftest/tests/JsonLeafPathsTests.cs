using System.Text.Json;

using Xunit;

namespace VideoForensics.Utils.SelfTest.Tests
{
    public class JsonLeafPathsTests
    {
        private static Dictionary<string, LeafKind> Collect(string json)
        {
            var paths = new Dictionary<string, LeafKind>(StringComparer.Ordinal);
            using var doc = JsonDocument.Parse(json);
            JsonLeafPaths.Collect(doc.RootElement, paths);
            return paths;
        }

        private static IReadOnlyDictionary<string, LeafKind> Merge(params string[] samples)
        {
            var set = new EndpointLeafSet();
            foreach (string sample in samples)
            {
                using var doc = JsonDocument.Parse(sample);
                set.Add(doc.RootElement);
            }

            return set.Leaves;
        }

        [Fact]
        public void Collect_ArrayOfObjects_NormalizesIndexesToBrackets()
        {
            var paths = Collect("""{"a":{"b":1},"c":[{"d":true},{"d":false}]}""");

            Assert.Equal(["a.b", "c[].d"], paths.Keys.OrderBy(k => k, StringComparer.Ordinal));
            Assert.Equal(LeafKind.Scalar, paths["c[].d"]);
        }

        [Fact]
        public void Collect_EmptyArrayAndEmptyObject_AreLeaves()
        {
            var paths = Collect("""{"a":[],"b":{}}""");

            Assert.Equal(LeafKind.EmptyArray, paths["a[]"]);
            Assert.Equal(LeafKind.EmptyObject, paths["b{}"]);
        }

        [Fact]
        public void Collect_ArrayOfScalars_IsOneBracketLeaf()
        {
            var paths = Collect("""{"a":[1,2,3],"b":["x"]}""");

            Assert.Equal(LeafKind.Scalar, paths["a[]"]);
            Assert.Equal(LeafKind.Scalar, paths["b[]"]);
            Assert.Equal(2, paths.Count);
        }

        [Fact]
        public void Collect_NullValue_IsNullLeaf()
        {
            var paths = Collect("""{"a":null,"b":{"c":null}}""");

            Assert.Equal(LeafKind.Null, paths["a"]);
            Assert.Equal(LeafKind.Null, paths["b.c"]);
        }

        [Fact]
        public void Collect_RootArray_StartsWithBrackets()
        {
            Assert.Equal(["[].id"], Collect("""[{"id":1},{"id":2}]""").Keys);
            Assert.Equal(LeafKind.EmptyArray, Collect("[]")["[]"]);
            Assert.Equal(LeafKind.EmptyObject, Collect("{}")["{}"]);
        }

        [Fact]
        public void Collect_ArrayWithNullAndScalar_MergesKindsOnOnePath()
        {
            var paths = Collect("""{"a":[1,null]}""");

            Assert.Equal(LeafKind.Scalar | LeafKind.Null, paths["a[]"]);
        }

        [Fact]
        public void Leaves_NullInOneSampleObjectInAnother_DropsTheNullPath()
        {
            var leaves = Merge("""{"cv":null}""", """{"cv":[{"type":"x"}]}""");

            Assert.False(leaves.ContainsKey("cv"));
            Assert.Equal(LeafKind.Scalar, leaves["cv[].type"]);
        }

        [Fact]
        public void Leaves_NullInEverySample_KeepsTheNullPath()
        {
            var leaves = Merge("""{"cv":null}""", """{"cv":null}""");

            Assert.Equal(LeafKind.Null, leaves["cv"]);
        }

        [Fact]
        public void Leaves_EmptyArrayInOneSampleElementsInAnother_DropsTheEmptyMarker()
        {
            var leaves = Merge("""{"a":[]}""", """{"a":[{"id":1}]}""");

            Assert.False(leaves.ContainsKey("a[]"));
            Assert.True(leaves.ContainsKey("a[].id"));
        }

        [Fact]
        public void Leaves_EmptyObjectInOneSamplePropertiesInAnother_DropsTheEmptyMarker()
        {
            var leaves = Merge("""{"h":{}}""", """{"h":{"x":1}}""");

            Assert.False(leaves.ContainsKey("h{}"));
            Assert.True(leaves.ContainsKey("h.x"));
        }

        [Fact]
        public void Leaves_EmptyArrayAndScalarArray_KeepsScalarKindOnly()
        {
            var leaves = Merge("""{"a":[]}""", """{"a":[1]}""");

            Assert.Equal(LeafKind.Scalar, leaves["a[]"]);
        }
    }
}
