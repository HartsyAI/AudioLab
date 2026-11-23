namespace AudioLab.Audio;

/// <summary>What a track is in a render: part of the whole mix, or one of the parts of a stem.</summary>
internal enum StemRole
{
    /// <summary>The render is the whole mix.</summary>
    Whole,

    /// <summary>The track the stem is of.</summary>
    Target,

    /// <summary>A track that sends into the bus the stem is of; only that send is heard.</summary>
    Sender,

    /// <summary>A track that has no part in the stem.</summary>
    Left,
}
