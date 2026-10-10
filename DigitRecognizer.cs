using System.Diagnostics;
using System.IO;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.Windows.AI.MachineLearning;

namespace WindowsMlDemo;

public sealed record Prediction(int Digit, double[] Probabilities, double ElapsedMilliseconds);
public enum InferenceProvider { Cpu, DirectML, TensorRtRtx }

/// <summary>Windows ML ships the ONNX Runtime APIs used here; no standalone ORT package is needed.</summary>
public sealed class DigitRecognizer : IDisposable
{
    private readonly InferenceSession session;
    private readonly string inputName;

    public string ProviderDescription { get; }

    public static async Task<DigitRecognizer> CreateAsync(InferenceProvider provider, bool profile = false)
    {
        OrtEpDevice? device = null;
        if (provider == InferenceProvider.TensorRtRtx)
        {
            if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 26100))
                throw new PlatformNotSupportedException("TensorRT for RTX requires Windows 11 24H2 or later.");
            var catalog = ExecutionProviderCatalog.GetDefault();
            var nvidia = catalog.FindAllProviders().FirstOrDefault(p =>
                p.Name.Equals("NvTensorRtRtxExecutionProvider", StringComparison.OrdinalIgnoreCase));
            if (nvidia is null)
                throw new InvalidOperationException("Windows ML did not find a compatible NVIDIA TensorRT for RTX provider. Check the GPU driver.");
            // Acquire only NVIDIA's provider and add its package to this process's dependency graph.
            var ready = await nvidia.EnsureReadyAsync();
            if (ready.Status != ExecutionProviderReadyResultState.Success)
                throw new InvalidOperationException($"TensorRT for RTX preparation failed: {ready.Status}.");
            if (!nvidia.TryRegister())
                throw new InvalidOperationException("TensorRT for RTX could not be registered with Windows ML.");
            device = OrtEnv.Instance().GetEpDevices().FirstOrDefault(d =>
                d.EpName.Equals("NvTensorRtRtxExecutionProvider", StringComparison.OrdinalIgnoreCase)
                && d.HardwareDevice.Type == OrtHardwareDeviceType.GPU);
            if (device is null)
                throw new InvalidOperationException("TensorRT for RTX registered, but no compatible GPU device is available.");
        }
        return new DigitRecognizer(provider, device, profile);
    }

    private DigitRecognizer(InferenceProvider provider, OrtEpDevice? device, bool profile)
    {
        using var options = new SessionOptions();
        ProviderDescription = provider.ToString();
        if (profile)
        {
            options.EnableProfiling = true;
            options.ProfileOutputPathPrefix = Path.Combine(AppContext.BaseDirectory, "inference-profile");
        }
        if (provider == InferenceProvider.DirectML)
        {
            // DirectML requires sequential execution and disabled memory patterns.
            options.ExecutionMode = ExecutionMode.ORT_SEQUENTIAL;
            options.EnableMemoryPattern = false;
            options.AppendExecutionProvider_DML(0);
            ProviderDescription = "DirectML GPU";
        }
        else if (provider == InferenceProvider.TensorRtRtx)
        {
            options.AppendExecutionProvider(OrtEnv.Instance(), new[] { device! }, new Dictionary<string, string>());
            ProviderDescription = $"TensorRT for RTX ({device!.EpName})";
        }
        // CPU is the default. DirectML can also delegate unsupported operators to CPU.
        session = new InferenceSession(Path.Combine(AppContext.BaseDirectory, "Models", "mnist-12.onnx"), options);
        inputName = session.InputMetadata.Keys.Single();
        var metadata = session.InputMetadata[inputName];
        if (metadata.ElementType != typeof(float) || !metadata.Dimensions.SequenceEqual(new[] { 1, 1, 28, 28 }))
        {
            session.Dispose();
            throw new InvalidDataException("The bundled model must accept float32 [1,1,28,28].");
        }
    }

    public Prediction Predict(float[] pixels)
    {
        if (pixels.Length != 784 || pixels.Any(p => !float.IsFinite(p) || p < 0 || p > 1))
            throw new ArgumentException("Expected 784 finite grayscale pixels in [0,1].", nameof(pixels));

        var tensor = new DenseTensor<float>(pixels, new[] { 1, 1, 28, 28 });
        var input = NamedOnnxValue.CreateFromTensor(inputName, tensor);
        var timer = Stopwatch.StartNew();
        using var output = session.Run(new[] { input });
        timer.Stop();
        var logits = output.Single().AsTensor<float>().ToArray();
        if (logits.Length != 10 || logits.Any(x => !float.IsFinite(x)))
            throw new InvalidDataException("Expected ten finite class scores from the model.");

        // Stable softmax: model outputs are logits, not probabilities.
        double max = logits.Max();
        var probabilities = logits.Select(x => Math.Exp(x - max)).ToArray();
        double total = probabilities.Sum();
        for (int i = 0; i < probabilities.Length; i++) probabilities[i] /= total;
        int digit = Array.IndexOf(probabilities, probabilities.Max());
        return new Prediction(digit, probabilities, timer.Elapsed.TotalMilliseconds);
    }

    public void Dispose() => session.Dispose();
    public string EndProfiling() => session.EndProfiling();
}
