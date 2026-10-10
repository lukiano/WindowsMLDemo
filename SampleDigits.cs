using System.IO;

namespace WindowsMlDemo;

public static class SampleDigits
{
    public static readonly byte[] Pixels = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Samples", "digits.bin"));
    public static readonly byte[] Labels = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Samples", "labels.bin"));

    public static float[] Get(int index)
    {
        if (Pixels.Length != Labels.Length * 784 || Labels.Any(label => label > 9))
            throw new InvalidDataException("Invalid bundled MNIST samples.");
        if (index < 0 || index >= Labels.Length) throw new ArgumentOutOfRangeException(nameof(index));
        return Pixels.Skip(index * 784).Take(784).Select(b => b / 255f).ToArray();
    }
}
