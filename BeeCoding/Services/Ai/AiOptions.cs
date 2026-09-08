namespace BeeCoding.Services.Ai;

public class AiOptions
{
    /// <summary>Master switch. Also needs <see cref="ApiKey"/> to be non-empty.</summary>
    public bool Enabled { get; set; }

    /// <summary>Bearer token for the OpenAI-compatible chat-completions endpoint.</summary>
    public string ApiKey { get; set; } = "";

    public string BaseUrl { get; set; } = "https://integrate.api.nvidia.com/v1/chat/completions";
    public string Model { get; set; } = "deepseek-ai/deepseek-v4-flash-0731";

    public double Temperature { get; set; } = 0.3;
    public int MaxTokens { get; set; } = 700;
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>Ceiling for <c>POST /api/ai/generate-problem</c> — writing a whole problem
    /// (statement + reference solution + N inputs) takes far longer than a hint.</summary>
    public int GenerateTimeoutSeconds { get; set; } = 180;

    /// <summary>Pass <c>chat_template_kwargs.thinking</c> to the model (slower, more thorough).</summary>
    public bool Thinking { get; set; }

    /// <summary>Reply language when the request doesn't specify one: "id" or "en".</summary>
    public string DefaultReplyLanguage { get; set; } = "id";

    /// <summary>Minimum seconds between a user's hint requests.</summary>
    public int RateLimitSeconds { get; set; } = 8;

    /// <summary>Student code is truncated to this many chars before being sent.</summary>
    public int MaxCodeChars { get; set; } = 16_000;
}
