using System.Text.Json;

namespace Echo_Windows.Core;

/// <summary>Reads Soniox control fields with the same tolerant type semantics as the Mac handler.</summary>
public static class SonioxResponseControl
{
    public static bool IsServiceError(JsonElement response)
    {
        if (response.ValueKind != JsonValueKind.Object) return false;
        if (response.TryGetProperty("error_code", out _)) return true;
        return response.TryGetProperty("error_message", out var message)
            && message.ValueKind == JsonValueKind.String;
    }

    public static bool IsFinished(JsonElement response) =>
        response.ValueKind == JsonValueKind.Object
        && response.TryGetProperty("finished", out var finished)
        && finished.ValueKind == JsonValueKind.True;
}
