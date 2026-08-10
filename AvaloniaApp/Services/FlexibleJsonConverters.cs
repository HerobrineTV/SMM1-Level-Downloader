using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SMMDownloader.Avalonia.Services;

public sealed class FlexibleLongConverter : JsonConverter<long>
{
    public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Number when reader.TryGetInt64(out var value) => value,
            JsonTokenType.Number => (long)reader.GetDouble(),
            JsonTokenType.String => ParseString(reader.GetString()),
            JsonTokenType.Null => 0,
            _ => 0
        };
    }

    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }

    private static long ParseString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer)
            ? integer
            : double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                ? (long)number
                : 0;
    }
}

public sealed class FlexibleIntConverter : JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Number when reader.TryGetInt32(out var value) => value,
            JsonTokenType.Number => (int)reader.GetDouble(),
            JsonTokenType.String => ParseString(reader.GetString()),
            JsonTokenType.Null => 0,
            _ => 0
        };
    }

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }

    private static int ParseString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer)
            ? integer
            : double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                ? (int)number
                : 0;
    }
}

public sealed class FlexibleDoubleConverter : JsonConverter<double>
{
    public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Number => reader.GetDouble(),
            JsonTokenType.String => ParseString(reader.GetString()),
            JsonTokenType.Null => 0,
            _ => 0
        };
    }

    public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }

    private static double ParseString(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ||
               !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            ? 0
            : number;
    }
}
