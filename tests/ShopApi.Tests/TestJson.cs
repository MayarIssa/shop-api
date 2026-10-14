using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShopApi.Tests;

public static class TestJson
{
    /// <summary>Matches the API's JSON settings (camelCase, enums as strings).</summary>
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
}
