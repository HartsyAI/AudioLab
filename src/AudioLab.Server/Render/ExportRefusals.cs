namespace AudioLab.Server.Render;

/// <summary>The words a client reads for an <see cref="ExportRefusal"/>.</summary>
public static class ExportRefusals
{
    /// <summary>The reason as the <c>reason</c> member of the answer.</summary>
    public static string Code(ExportRefusal refusal)
    {
        return refusal switch
        {
            ExportRefusal.BadOptions => "bad_options",
            ExportRefusal.Busy => "busy",
            ExportRefusal.MissingMedia => "missing_media",
            ExportRefusal.TooLarge => "too_large",
            ExportRefusal.TimedOut => "timed_out",
            ExportRefusal.Empty => "empty",
            _ => throw new ArgumentOutOfRangeException(nameof(refusal)),
        };
    }

    /// <summary>The HTTP status: 400 for a wrong request, 429 for busy and 422 for a project that cannot be exported as asked.</summary>
    public static int Status(ExportRefusal refusal)
    {
        return refusal switch
        {
            ExportRefusal.BadOptions => StatusCodes.Status400BadRequest,
            ExportRefusal.Busy => StatusCodes.Status429TooManyRequests,
            _ => StatusCodes.Status422UnprocessableEntity,
        };
    }
}
