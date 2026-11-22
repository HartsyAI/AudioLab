namespace AudioLab.Server.Tests;

/// <summary>Thrown from a write observer to stop a write at a chosen stage, as a crash would.</summary>
public sealed class SimulatedCrash : Exception
{
    public SimulatedCrash(string stage)
        : base($"Simulated crash at {stage}.")
    {
    }
}
