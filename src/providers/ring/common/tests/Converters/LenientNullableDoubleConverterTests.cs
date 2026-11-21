using System.Text.Json;
using System.Text.Json.Serialization;

using VideoForensics.Providers.Ring.Converters;

using Xunit;

namespace VideoForensics.Providers.Ring.Common.Tests.Converters
{
    public class LenientNullableDoubleConverterTests
    {
        private sealed class Holder
        {
            [JsonConverter(typeof(LenientNullableDoubleConverter))]
            [JsonPropertyName("similarity")]
            public double? Similarity { get; set; }
        }

        private static double? ReadSimilarity(string value)
        {
            return JsonSerializer.Deserialize<Holder>("{\"similarity\":" + value + "}")!.Similarity;
        }

        [Fact]
        public void LenientNullableDouble_Number_IsKept()
        {
            Assert.Equal(0.91, ReadSimilarity("0.91"));
        }

        [Fact]
        public void LenientNullableDouble_NumericString_IsParsed()
        {
            Assert.Equal(0.5, ReadSimilarity("\"0.5\""));
        }

        [Fact]
        public void LenientNullableDouble_EmptyString_IsNull()
        {
            Assert.Null(ReadSimilarity("\"\""));
        }

        [Fact]
        public void LenientNullableDouble_NonNumericString_IsNull()
        {
            Assert.Null(ReadSimilarity("\"n/a\""));
        }

        [Fact]
        public void LenientNullableDouble_Null_IsNull()
        {
            Assert.Null(ReadSimilarity("null"));
        }

        [Fact]
        public void LenientNullableDouble_Object_IsNullInsteadOfThrowing()
        {
            Assert.Null(ReadSimilarity("{\"x\":1}"));
        }
    }
}
