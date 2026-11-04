using System.Text.Json;

namespace Echo_Windows.Core;

/// <summary>Mirrors the Mac handler's quiet-timer activity for valid Soniox responses.</summary>
public static class SonioxResponseActivity
{
    public static bool ShouldResetQuietTimer(JsonElement response)
    {
        if (response.ValueKind != JsonValueKind.Object) return false;
        return !SonioxResponseControl.IsServiceError(response)
            && !SonioxResponseControl.IsFinished(response);
    }
}
