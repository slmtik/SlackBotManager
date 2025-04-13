using System.Text.Json;
using System.Text.Json.Serialization;

namespace Slack;

class SlackStringBooleanConverter : JsonConverter<bool>
{
    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String && bool.TryParse(reader.GetString(), out var result))
        {
            return result;
        }

        if (reader.TokenType == JsonTokenType.True) return true;
        if (reader.TokenType == JsonTokenType.False) return false;

        throw new JsonException($"Unable to convert {reader.TokenType} to bool");
    }

    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
    {
        writer.WriteBooleanValue(value);
    }
}
