using System.Globalization;
using System.Text;

namespace Echo_Windows.Core;

public static class SonioxRequestBuilder
{
    private const string EconomicsContextText = "This recording may be an economics lecture. Carefully distinguish microeconomic and microeconomics, which refer to individual consumers, firms, markets, and incentives, from macroeconomic and macroeconomics, which refer to economy-wide growth, inflation, unemployment, GDP, and monetary or fiscal policy. In calculus and economics, derivative, partial derivative, first derivative, second derivative, and derivative of a function refer to calculus concepts and must not be confused with duty or duties, which mean a tax or obligation. Never substitute one term for the other.";

    private static readonly string[] EconomicsTerms =
    [
        "microeconomic", "macroeconomic", "microeconomics", "macroeconomics",
        "micro economic", "macro economic", "micro-economic", "macro-economic",
        "microeconomic policy", "macroeconomic policy", "explicit cost", "implicit cost",
        "explicit costs", "implicit costs", "margin", "marginal", "marginal benefit",
        "marginal cost", "marginal benefits", "marginal costs", "incremental cost",
        "derivative", "derivatives", "partial derivative", "first derivative", "second derivative",
        "derivative of", "take the derivative", "with respect to", "differentiate", "hand", "hands",
        "on the other hand", "on the one hand", "one hand", "other hand", "right hand", "left hand", "hand side"
    ];

    private static readonly (string Source, string Target)[] EconomicsTranslationTerms =
    [
        ("microeconomic", "微观经济学"), ("macroeconomic", "宏观经济学"),
        ("microeconomics", "微观经济学"), ("macroeconomics", "宏观经济学"),
        ("micro economic", "微观经济学"), ("macro economic", "宏观经济学"),
        ("micro-economic", "微观经济学"), ("macro-economic", "宏观经济学"),
        ("explicit cost", "显性成本"), ("implicit cost", "隐性成本"),
        ("explicit costs", "显性成本"), ("implicit costs", "隐性成本"),
        ("margin", "边际"), ("marginal", "边际"), ("marginal benefit", "边际收益"),
        ("marginal cost", "边际成本"), ("marginal benefits", "边际收益"),
        ("marginal costs", "边际成本"), ("incremental cost", "增量成本"),
        ("derivative", "导数"), ("derivatives", "导数"), ("partial derivative", "偏导数"),
        ("first derivative", "一阶导数"), ("second derivative", "二阶导数"),
        ("hand", "手"), ("hands", "手")
    ];

    public static Dictionary<string, object> Build(Preferences config, TranscriptSegmentationSettings segmentation)
    {
        segmentation = segmentation.Copy().Validate();
        var terms = EconomicsTerms.Concat(NormalizeCorrectionTerms(config.CorrectionTerms)).ToArray();
        var request = new Dictionary<string, object>
        {
            ["model"] = EchoServiceModels.SonioxRealtime,
            ["audio_format"] = "pcm_s16le",
            ["sample_rate"] = 16000,
            ["num_channels"] = 1,
            ["enable_endpoint_detection"] = true,
            ["max_endpoint_delay_ms"] = segmentation.SonioxMaxEndpointDelayMilliseconds,
            ["endpoint_sensitivity"] = segmentation.SonioxEndpointSensitivity,
            ["endpoint_latency_adjustment_level"] = segmentation.SonioxEndpointLatencyAdjustmentLevel,
            ["enable_language_identification"] = true,
            ["enable_speaker_diarization"] = config.Speakers,
            ["context"] = new Dictionary<string, object>
            {
                ["general"] = new[]
                {
                    new Dictionary<string, string> { ["key"] = "domain", ["value"] = "economics and finance" },
                    new Dictionary<string, string> { ["key"] = "topic", ["value"] = "economic research, markets, and financial analysis" }
                },
                ["text"] = EconomicsContextText,
                ["terms"] = terms,
                ["translation_terms"] = EconomicsTranslationTerms.Select(term =>
                    new Dictionary<string, string> { ["source"] = term.Source, ["target"] = term.Target }).ToArray()
            }
        };
        if (!string.IsNullOrWhiteSpace(config.SourceLanguage))
        {
            request["language_hints"] = new[] { config.SourceLanguage };
            request["language_hints_strict"] = config.Strict;
        }
        if (config.Translate) request["translation"] = new { type = "one_way", target_language = config.TargetLanguage };
        return request;
    }

    public static string[] NormalizeCorrectionTerms(string? correctionTerms) => (correctionTerms ?? "")
        .Split('\n')
        .Select(term => term.Trim())
        .Where(term => term.Length > 0)
        .Select(term => PrefixTextElements(term, 80))
        .Take(100)
        .ToArray();

    private static string PrefixTextElements(string value, int maximum)
    {
        var result = new StringBuilder(Math.Min(value.Length, maximum));
        var elements = StringInfo.GetTextElementEnumerator(value);
        int count = 0;
        while (count < maximum && elements.MoveNext())
        {
            result.Append((string)elements.Current!);
            count++;
        }
        return result.ToString();
    }
}
