# Windows ML Digit Lab

A small C# / WPF Windows desktop application that recognizes handwritten digits locally using the current **Windows ML** library. Draw with your mouse or pen, select **Recognize**, and inspect the ten class scores. **Sample** cycles through 20 bundled MNIST test images.

## Run

Double-click `publish\WindowsMlDemo.exe`. This x64 build bundles .NET and the Windows ML native runtime, so no separate .NET installation or model download is required. Keep the complete publish folder together.

To build from source, install the .NET 10 SDK, then run from PowerShell:

```powershell
cd H:\Projects\WindowsMlDemo
.\run.ps1
```

You can also open `WindowsMlDemo.sln` in a Visual Studio version that supports .NET 10 and has the .NET desktop development workload.

The default is **NVIDIA TensorRT for RTX**. Select **Sample**, then **Recognize** to try it. First use may download and install NVIDIA's shared execution provider through Windows ML, then compile the model. The UI stays responsive and shows preparation status. This option requires Windows 11 24H2 (build 26100) or later, a compatible RTX GPU (Microsoft lists RTX 30-series and later), and a supported NVIDIA driver. Provider availability is checked against Windows ML's catalog and registered GPU devices.

CPU and DirectML remain available in the dropdown. These modes support x64 Windows 10 version 2004 (build 19041) or later. DirectML requires a compatible DirectX 12 GPU and driver. Initial package restore and first-time TensorRT provider acquisition require internet access; model inference uses the bundled model locally. Provider setup failures are shown explicitly rather than silently switching the selected mode to CPU.

## How Windows ML is used

`WindowsMlDemo.csproj` references **Microsoft.Windows.AI.MachineLearning 2.4.89**. Windows ML supplies ONNX Runtime's C# API through the `Microsoft.ML.OnnxRuntime` namespace, along with native `onnxruntime.dll` and `DirectML.dll`. This follows Microsoft's current Windows ML deployment path rather than the older `Windows.AI.MachineLearning` WinRT APIs.

1. `DigitPreprocessor.cs` renders white ink on black, fits it into a 20 x 20 box, downsamples to 28 x 28, centers by intensity, and normalizes pixels to [0,1]. Bundled samples already have MNIST preprocessing and bypass this drawing step.
2. `DigitRecognizer.cs` opens the bundled `Models\mnist-12.onnx` and reuses its inference session until you change providers.
3. The input is a float32 tensor with shape **[1,1,28,28]**. The model returns ten logits, converted to class scores with stable softmax.
4. TensorRT mode locates NVIDIA's provider using `ExecutionProviderCatalog.FindAllProviders()`, awaits `EnsureReadyAsync()`, calls `TryRegister()`, and explicitly selects the first registered NVIDIA GPU using `OrtEnv.GetEpDevices()` and `SessionOptions.AppendExecutionProvider()`. CPU mode uses the default provider. DirectML mode appends adapter 0, disables memory patterns, and uses sequential execution. Unsupported operators may run on CPU in GPU modes.
5. Native inference runs on a background thread. Output collections and sessions are disposed explicitly.

Scores are model outputs, not calibrated certainty. Handwriting that differs from MNIST can produce confident errors. This is an inference example; it does not train a model, install vendor NPU providers, or download anything at app startup. The tiny model is useful for understanding the API, not benchmarking GPU performance.

## Validate

```powershell
.\test.ps1        # CPU
.\test.ps1 -Gpu   # DirectML GPU adapter 0
.\test.ps1 -Rtx   # TensorRT for RTX; acquires NVIDIA's provider if necessary
.\test.ps1 -Ui    # XAML layout, sample/clear flow, and drawn-ink preprocessing
```

The self-test checks 20 known images, verifies probability normalization, and rejects malformed inputs. It exits nonzero if fewer than 18 samples match their labels or another check fails. It records ONNX Runtime profiling, reports the providers that actually executed nodes, and requires NVIDIA node execution in RTX mode; an entirely CPU fallback cannot pass the RTX check. Reports and profiling JSON are saved beside the executable. These checks do not validate mouse/pen interaction or recognition accuracy for arbitrary handwriting.

Verified on the RTX 5090 creation machine: Release build with .NET SDK **10.0.401**, zero warnings/errors; TensorRT for RTX **20/20**, CPU **20/20**, and DirectML **20/20**. RTX profiling records `NvTensorRTRTXExecutionProvider` node execution. The UI check renders the real XAML into `ui-preview.png` beside the executable and checks sample recognition, switching across all three providers, clearing, ink preprocessing, and drawing inference without opening a test window.

To rebuild the portable folder:

```powershell
dotnet publish WindowsMlDemo.csproj -c Release -r win-x64 --self-contained true -o publish
```

## Model and samples

- Model: [ONNX Model Zoo MNIST](https://github.com/onnx/models/tree/main/validated/vision/classification/mnist), `mnist-12.onnx` (MIT; see `Models\LICENSE.txt`).
- Model SHA-256: `5C688690F8BACF667D4C2074AF5AD0646CA328D7AB03ECCF944A65B320171BDD`.
- Samples: first 20 images and labels from the MNIST test set, hosted by the [CVDF mirror](https://github.com/cvdfoundation/mnist). `Samples\digits.bin` contains 20 consecutive 784-byte grayscale images; `Samples\labels.bin` contains their 20 one-byte labels. See `THIRD-PARTY-NOTICES.md` for attribution.

Reference documentation:

- [Install and deploy Windows ML](https://learn.microsoft.com/en-us/windows/ai/new-windows-ml/distributing-your-app)
- [Use ONNX APIs in Windows ML](https://learn.microsoft.com/en-us/windows/ai/new-windows-ml/use-onnx-apis)
- [Run ONNX models](https://learn.microsoft.com/en-us/windows/ai/new-windows-ml/run-onnx-models)
- [Register Windows ML execution providers](https://learn.microsoft.com/en-us/windows/ai/new-windows-ml/register-execution-providers)
- [NVIDIA TensorRT RTX execution provider](https://onnxruntime.ai/docs/execution-providers/TensorRTRTX-ExecutionProvider.html)
