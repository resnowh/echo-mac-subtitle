using System.Text.Json;

namespace Echo_Windows.Core;

/// <summary>Mirrors the Mac handler's quiet-timer activity for valid Soniox responses.</summary>
public static class SonioxResponseActivity
{
    public static bool ShouldResetQuietTimer(JsonElement response)
    {
        if (response.ValueKind != JsonValueKind.Object) return false;
        if (response.TryGetProperty("error_message", out var errorMessage)
            && errorMessage.ValueKind == JsonValueKind.String)
            return false;
        return !response.TryGetProperty("finished", out var finished)
            || finished.ValueKind != JsonValueKind.True;
    }
}
