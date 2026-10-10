using System.IO;
using System.Windows;

namespace WindowsMlDemo;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Contains("--ui-test"))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            string report = Path.Combine(AppContext.BaseDirectory, "ui-test.txt");
            try
            {
                var window = new MainWindow();
                await window.RunUiSelfTest();
                window.Close();
                await File.WriteAllTextAsync(report, "PASS: XAML layout, sample recognition, TensorRT/DirectML/CPU switching, clear, ink preprocessing, and drawing inference.");
                Shutdown(0);
            }
            catch (Exception ex)
            {
                await File.WriteAllTextAsync(report, ex.ToString());
                Shutdown(1);
            }
            return;
        }
        if (e.Args.Contains("--self-test"))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            string report = Path.Combine(AppContext.BaseDirectory, "self-test.txt");
            try
            {
                var provider = e.Args.Contains("--rtx") ? InferenceProvider.TensorRtRtx
                    : e.Args.Contains("--gpu") ? InferenceProvider.DirectML : InferenceProvider.Cpu;
                string result = await Task.Run(() => SelfTest.RunAsync(provider));
                await File.WriteAllTextAsync(report, result);
                Shutdown(0);
            }
            catch (Exception ex)
            {
                await File.WriteAllTextAsync(report, ex.ToString());
                Shutdown(1);
            }
            return;
        }
        new MainWindow().Show();
    }
}
