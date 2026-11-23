namespace AudioLab.Audio.Tests;

public sealed class RealFftTests
{
    [Fact]
    public void ItMatchesTheDefinitionOfTheTransformAndTurnsBack()
    {
        const int Size = 16;
        Random random = new(7);
        float[] input = [.. Enumerable.Range(0, Size).Select(_ => (float)((random.NextDouble() * 2) - 1))];
        RealFft fft = new(Size);
        float[] real = new float[fft.Bins];
        float[] imaginary = new float[fft.Bins];

        fft.Forward(input, real, imaginary);

        for (int k = 0; k < fft.Bins; k++)
        {
            double expectedReal = 0;
            double expectedImaginary = 0;
            for (int n = 0; n < Size; n++)
            {
                expectedReal += input[n] * Math.Cos(2 * Math.PI * k * n / Size);
                expectedImaginary -= input[n] * Math.Sin(2 * Math.PI * k * n / Size);
            }
            Assert.Equal(expectedReal, real[k], 4);
            Assert.Equal(expectedImaginary, imaginary[k], 4);
        }
        float[] back = new float[Size];
        fft.Inverse(real, imaginary, back);
        for (int n = 0; n < Size; n++)
        {
            Assert.Equal(input[n], back[n], 5);
        }
    }
}
