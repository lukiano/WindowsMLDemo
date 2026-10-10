using System.Text;
using System.Text.Json;
using System.IO;

namespace WindowsMlDemo;

public static class SelfTest
{
    public static async Task<string> RunAsync(InferenceProvider provider)
    {
        var report = new StringBuilder($"Windows ML MNIST self-test ({provider})\n");
        using var recognizer = await DigitRecognizer.CreateAsync(provider, profile: true);
        report.AppendLine(recognizer.ProviderDescription);
        int correct = 0;
        for (int i = 0; i < SampleDigits.Labels.Length; i++)
        {
            var result = recognizer.Predict(SampleDigits.Get(i));
            if (Math.Abs(result.Probabilities.Sum() - 1) > 1e-6)
                throw new InvalidOperationException("Softmax probabilities do not sum to one.");
            if (result.Digit == SampleDigits.Labels[i]) correct++;
            report.AppendLine($"Sample {i:00}: expected {SampleDigits.Labels[i]}, predicted {result.Digit}, score {result.Probabilities[result.Digit]:P2}");
        }
        // Allow occasional classification errors; MNIST inference is not perfectly accurate.
        if (correct < 18) throw new InvalidOperationException($"Only {correct}/20 known samples classified correctly.");
        bool rejectedInvalidInput = false;
        try { recognizer.Predict(new float[1]); }
        catch (ArgumentException) { rejectedInvalidInput = true; }
        if (!rejectedInvalidInput) throw new InvalidOperationException("Invalid input was accepted.");
        string profilePath = recognizer.EndProfiling();
        using var profile = JsonDocument.Parse(File.ReadAllText(profilePath));
        var providers = profile.RootElement.EnumerateArray()
            .Where(e => e.TryGetProperty("args", out var args) && args.TryGetProperty("provider", out _))
            .Select(e => e.GetProperty("args").GetProperty("provider").GetString()!).Distinct().ToArray();
        report.AppendLine($"Executed providers: {string.Join(", ", providers)}");
        if (provider == InferenceProvider.TensorRtRtx && !providers.Any(p => p.Equals("NvTensorRtRtxExecutionProvider", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("No TensorRT for RTX node execution was recorded; inference fell back to CPU.");
        report.AppendLine($"PASS: {correct}/20 known digits, output normalization, and invalid-input validation.");
        return report.ToString();
    }
}
