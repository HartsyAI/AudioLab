using AudioLab.Core.Model;

namespace AudioLab.Web.Session;

/// <summary>The lane a recorded pass writes, the mode it records in, and the control's value when no one holds it.</summary>
/// <param name="Track">The track whose lane is written.</param>
/// <param name="Parameter">The lane's parameter: volume or pan.</param>
/// <param name="Mode">The mode the pass records in; only touch, latch and write record.</param>
/// <param name="Resting">The value the control has when it is not held, read on each sample.</param>
public readonly record struct AutomationTarget(TrackId Track, AutomationParameter Parameter, AutomationMode Mode, Func<double> Resting);
