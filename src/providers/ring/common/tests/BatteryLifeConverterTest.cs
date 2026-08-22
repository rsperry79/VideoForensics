using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using VideoForensics.Providers.Ring.Converters;

namespace VideoForensics.Providers.Ring.Tests.Converters;

public class BatteryLifeConverterTest
{
    private string GenerateTestJson(string value) => $"{{ \"TestValue\": {value} }}";
    private BatteryLifeConverter SystemUnderTest => new();

    [Fact]
    public void TestNumericValue()
    {
        var testJson = GenerateTestJson("81");
        var value = ReadValueFromJson(testJson);
        Assert.Equal(81, value);
    }

    [Fact]
    public void TestStringValue()
    {
        var testJson = GenerateTestJson("\"81\"");
        var value = ReadValueFromJson(testJson);
        Assert.Equal(81, value);
    }

    private int? ReadValueFromJson(string json)
    {
        var utf8JsonReader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json), false, new JsonReaderState(new JsonReaderOptions()));
        utf8JsonReader.Read(); // Reads the Start Object
        utf8JsonReader.Read(); // Reads the property
        utf8JsonReader.Read(); // Reads the value
        var type = utf8JsonReader.TokenType switch
        {
            JsonTokenType.String => typeof(string),
            JsonTokenType.Number => typeof(int),
            _ => throw new ArgumentOutOfRangeException()
        };
        var deserializedValue = SystemUnderTest.Read(ref utf8JsonReader, typeof(int?), new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });
        return deserializedValue;
    }
}
