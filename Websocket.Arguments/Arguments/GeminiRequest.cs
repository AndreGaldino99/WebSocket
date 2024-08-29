using Newtonsoft.Json;

namespace Websocket.Arguments;

public class ContentRequest(List<PartRequest>? parts)
{
    [JsonProperty("parts")]
    public List<PartRequest>? Parts { get; set; } = parts;
}

public class PartRequest(string? text)
{
    [JsonProperty("text")]
    public string? Text { get; set; } = text;
}

public class GeminiRequest(List<ContentRequest>? contents)
{
    [JsonProperty("contents")]
    public List<ContentRequest>? Contents { get; set; } = contents;
}
