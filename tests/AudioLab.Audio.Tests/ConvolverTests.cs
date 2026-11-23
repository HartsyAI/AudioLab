namespace AudioLab.Audio.Tests;

public sealed class ConvolverTests
{
    private static float[] Noise(int length, int seed)
    {
        Random random = new(seed);
        return [.. Enumerable.Range(0, length).Select(_ => (float)((random.NextDouble() * 2) - 1))];
    }

    private static float[] Direct(float[] input, float[] impulse)
    {
        float[] output = new float[input.Length];
        for (int n = 0; n < input.Length; n++)
        {
            double sum = 0;
            for (int k = 0; k < impulse.Length && k <= n; k++)
            {
                sum += (double)impulse[k] * input[n - k];
            }
            output[n] = (float)sum;
        }
        return output;
    }

    private static float[] Convolve(float[] input, float[] impulse, params int[] chunks)
    {
        PartitionedConvolver convolver = new(impulse);
        float[] output = [.. input];
        int position = 0;
        for (int turn = 0; position < output.Length; turn++)
        {
            int count = Math.Min(chunks[turn % chunks.Length], output.Length - position);
            convolver.Process(output.AsSpan(position, count));
            position += count;
        }
        return output;
    }

    [Theory]
    [InlineData(1)]
    [InlineData(300)]
    [InlineData(512)]
    [InlineData(513)]
    [InlineData(3000)]
    [InlineData(4096)]
    [InlineData(4097)]
    [InlineData(9000)]
    public void ItMatchesADirectConvolutionAtEveryLengthOfImpulseAcrossTheBlockEdges(int taps)
    {
        float[] impulse = Noise(taps, 1);
        float[] input = Noise(12000, 2);

        float[] expected = Direct(input, impulse);
        float[] actual = Convolve(input, impulse, 700, 1, 333);

        float worst = expected.Zip(actual, (e, a) => Math.Abs(e - a)).Max();
        Assert.True(worst < 2e-4, $"worst error {worst}");
    }

    [Fact]
    public void TheFirstSampleOfTheImpulseActsOnTheSampleThatArrivesWithNoDelay()
    {
        float[] output = Convolve([1, 0, 0, 0], [0.5f, 0.25f], 4);

        Assert.Equal([0.5f, 0.25f, 0, 0], output);
    }

    [Fact]
    public void TheOutputDoesNotDependOnHowTheInputIsCut()
    {
        float[] impulse = Noise(9000, 3);
        float[] input = Noise(14000, 4);

        float[] whole = Convolve(input, impulse, 14000);

        Assert.Equal(whole, Convolve(input, impulse, 1));
        Assert.Equal(whole, Convolve(input, impulse, 511, 2, 1025, 4099));
    }

    [Fact]
    public void ResetForgetsTheInput()
    {
        float[] impulse = Noise(1500, 5);
        PartitionedConvolver convolver = new(impulse);
        float[] first = Noise(2000, 6);
        float[] again = [.. first];
        convolver.Process(first);
        convolver.Reset();
        convolver.Process(again);

        Assert.Equal(first, again);
    }
}
