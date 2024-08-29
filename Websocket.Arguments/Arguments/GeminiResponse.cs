using Newtonsoft.Json;

namespace Websocket.Arguments;

public class CandidateResponse
{
    [JsonProperty("content")]
    public ContentResponse? Content { get; set; }

    [JsonProperty("finishReason")]
    public string? FinishReason { get; set; }

    [JsonProperty("index")]
    public int? Index { get; set; }

    [JsonProperty("safetyRatings")]
    public List<SafetyRatingResponse>? SafetyRatings { get; set; }
}

public class ContentResponse
{
    [JsonProperty("parts")]
    public List<PartResponse>? Parts { get; set; }

    [JsonProperty("role")]
    public string? Role { get; set; }
}

public class PartResponse
{
    [JsonProperty("text")]
    public string? Text { get; set; }
}

public class GeminiResponse
{
    [JsonProperty("candidates")]
    public List<CandidateResponse>? Candidates { get; set; }

    [JsonProperty("usageMetadata")]
    public UsageMetadataResponse? UsageMetadata { get; set; }
}

public class SafetyRatingResponse
{
    [JsonProperty("category")]
    public string? Category { get; set; }

    [JsonProperty("probability")]
    public string? Probability { get; set; }
}

public class UsageMetadataResponse
{
    [JsonProperty("promptTokenCount")]
    public int? PromptTokenCount { get; set; }

    [JsonProperty("candidatesTokenCount")]
    public int? CandidatesTokenCount { get; set; }

    [JsonProperty("totalTokenCount")]
    public int? TotalTokenCount { get; set; }
}
