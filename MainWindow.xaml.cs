using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WindowsMlDemo;

public partial class MainWindow : Window
{
    private DigitRecognizer? recognizer;
    private InferenceProvider sessionProvider;
    private bool busy;
    private float[]? samplePixels;
    private int nextSample;

    public MainWindow()
    {
        InitializeComponent();
        Drawing.DefaultDrawingAttributes = new DrawingAttributes
        {
            Color = Colors.White, Width = 18, Height = 18, FitToCurve = true,
            IgnorePressure = true, StylusTip = StylusTip.Ellipse
        };
        Closing += OnClosing;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        // Avoid disposing a native inference session while a background run owns it.
        if (busy) { e.Cancel = true; return; }
        recognizer?.Dispose();
    }

    private async void Recognize_Click(object sender, RoutedEventArgs e) => await RecognizeAsync();

    private async Task RecognizeAsync()
    {
        if (busy) return;
        if (samplePixels is null && Drawing.Strokes.Count == 0)
        {
            Status.Text = "Draw a digit or select Sample first.";
            return;
        }
        var provider = Provider.SelectedIndex switch
        {
            0 => InferenceProvider.TensorRtRtx,
            1 => InferenceProvider.DirectML,
            _ => InferenceProvider.Cpu
        };
        SetBusy(true);
        Status.Text = provider == InferenceProvider.TensorRtRtx
            ? "Preparing TensorRT for RTX (first use may download the provider and compile the model)…"
            : "Loading model and running inference…";
        try
        {
            // Capture WPF graphics on the UI thread; native inference runs in the background.
            float[] pixels = samplePixels ?? DigitPreprocessor.FromStrokes(Drawing.Strokes);
            Prediction result = await Task.Run(async () =>
            {
                if (recognizer is null || sessionProvider != provider)
                {
                    recognizer?.Dispose();
                    recognizer = null;
                    recognizer = await DigitRecognizer.CreateAsync(provider);
                    sessionProvider = provider;
                }
                return recognizer.Predict(pixels);
            });
            Digit.Text = result.Digit.ToString();
            Confidence.Text = $"Model score: {result.Probabilities[result.Digit]:P1}";
            Scores.ItemsSource = result.Probabilities.Select((p, i) => new { Digit = i, Percent = p * 100, Label = $"{p:P1}" }).ToArray();
            Status.Text = $"{recognizer!.ProviderDescription} · inference {result.ElapsedMilliseconds:F2} ms · session reused · CPU operator fallback allowed";
        }
        catch (Exception ex)
        {
            ResetResult();
            Status.Text = $"{provider} inference failed. Select CPU or DirectML to retry. {ex.Message}";
        }
        finally { SetBusy(false); }
    }

    private void SetBusy(bool value)
    {
        busy = value;
        Provider.IsEnabled = RecognizeButton.IsEnabled = ClearButton.IsEnabled = SampleButton.IsEnabled = Drawing.IsEnabled = !value;
    }

    private void ResetResult()
    {
        Digit.Text = "—";
        Confidence.Text = "Draw a large, centered digit.";
        Scores.ItemsSource = null;
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        Drawing.Strokes.Clear();
        Drawing.Children.Clear();
        samplePixels = null;
        ResetResult();
        Status.Text = "Canvas cleared. Draw one digit, 0–9.";
    }

    private void Drawing_StrokeCollected(object sender, InkCanvasStrokeCollectedEventArgs e)
    {
        Drawing.Children.Clear();
        samplePixels = null;
        ResetResult();
        Status.Text = "Digit drawn. Select Recognize to run the model.";
    }

    private void Sample_Click(object sender, RoutedEventArgs e)
    {
        Drawing.Strokes.Clear();
        Drawing.Children.Clear();
        int index = nextSample++ % SampleDigits.Labels.Length;
        samplePixels = SampleDigits.Get(index);
        byte[] bgra = new byte[784 * 4];
        for (int i = 0; i < 784; i++)
        {
            byte value = (byte)Math.Round(samplePixels[i] * 255);
            bgra[i * 4] = bgra[i * 4 + 1] = bgra[i * 4 + 2] = value;
            bgra[i * 4 + 3] = 255;
        }
        var bitmap = BitmapSource.Create(28, 28, 96, 96, PixelFormats.Bgra32, null, bgra, 28 * 4);
        bitmap.Freeze();
        var image = new Image { Source = bitmap, Width = 280, Height = 280, IsHitTestVisible = false };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
        Drawing.Children.Add(image);
        ResetResult();
        Status.Text = $"MNIST sample {index + 1}/20 · expected digit {SampleDigits.Labels[index]} · select Recognize";
    }

    internal async Task RunUiSelfTest()
    {
        // Lay out the actual XAML without opening a visible test window.
        var content = (FrameworkElement)Content;
        content.Measure(new Size(804, 680));
        content.Arrange(new Rect(0, 0, 804, 680));
        content.UpdateLayout();
        Sample_Click(this, new RoutedEventArgs());
        await RecognizeAsync();
        if (Digit.Text != "7" || Scores.ItemsSource is null || busy)
            throw new InvalidOperationException("Sample button / UI recognition failed.");
        foreach (int selection in new[] { 1, 2, 0 })
        {
            Provider.SelectedIndex = selection;
            await RecognizeAsync();
            if (Digit.Text != "7") throw new InvalidOperationException($"Provider switching failed at selection {selection}.");
        }
        content.UpdateLayout();
        await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        var bitmap = new RenderTargetBitmap(804, 680, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(content);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(Path.Combine(AppContext.BaseDirectory, "ui-preview.png"))) encoder.Save(stream);

        Clear_Click(this, new RoutedEventArgs());
        if (Drawing.Children.Count != 0 || samplePixels is not null || Scores.ItemsSource is not null)
            throw new InvalidOperationException("Clear button failed.");
        Drawing.Strokes.Add(new Stroke(new System.Windows.Input.StylusPointCollection
        {
            new System.Windows.Input.StylusPoint(140, 60), new System.Windows.Input.StylusPoint(140, 220)
        }, Drawing.DefaultDrawingAttributes.Clone()));
        var pixels = DigitPreprocessor.FromStrokes(Drawing.Strokes);
        if (pixels.Length != 784 || pixels.Sum() <= 0 || pixels.Any(p => !float.IsFinite(p) || p < 0 || p > 1))
            throw new InvalidOperationException("Ink preprocessing failed.");
        await RecognizeAsync();
        if (Digit.Text == "—") throw new InvalidOperationException("Drawing inference failed.");
        recognizer?.Dispose();
        recognizer = null;
    }
}
