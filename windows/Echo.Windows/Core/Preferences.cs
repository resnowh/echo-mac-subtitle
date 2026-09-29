using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Echo_Windows.Core;

public sealed class Preferences
{
    public string SonioxModel { get; set; } = "stt-rt-v5";
    public string DeepSeekModel { get; set; } = "deepseek-v4-flash";
    public string SourceLanguage { get; set; } = "en";
    public string TargetLanguage { get; set; } = "zh";
    public bool Translate { get; set; } = true;
    public bool Strict { get; set; }
    public bool Speakers { get; set; } = true;
    public string SonioxSecret { get; set; } = "";
    public string DeepSeekSecret { get; set; } = "";
    public string Theme { get; set; } = "Default";
    public static string Protect(string value) => value.Length == 0 ? "" : Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value), null, DataProtectionScope.CurrentUser));
    public static string Unprotect(string value) => value.Length == 0 ? "" : Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(value), null, DataProtectionScope.CurrentUser));
    public static Preferences Load()
    {
        string file = Path.Combine(TranscriptFiles.Root, "settings.json");
        return File.Exists(file) ? JsonSerializer.Deserialize<Preferences>(File.ReadAllText(file), TranscriptFiles.Json) ?? new() : new();
    }
    public void Save() => TranscriptFiles.AtomicWrite(Path.Combine(TranscriptFiles.Root, "settings.json"), JsonSerializer.Serialize(this, TranscriptFiles.Json));
}
