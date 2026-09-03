namespace RootlessWM.Domain;

public readonly record struct MasterStackLayoutOptions(
    double MasterRatio,
    int OuterGap,
    int InnerGap,
    MasterStackLayoutMode Mode = MasterStackLayoutMode.MasterLeft,
    int MasterCount = 1)
{
    public const int MaxMasterCount = 9;
    public const int MaxGap = 100;

    public static MasterStackLayoutOptions Default { get; } = new(0.55, 0, 0);

    public MasterStackLayoutOptions CycleMode()
    {
        return this with
        {
            Mode = Mode switch
            {
                MasterStackLayoutMode.MasterLeft => MasterStackLayoutMode.MasterTop,
                MasterStackLayoutMode.MasterTop => MasterStackLayoutMode.Monocle,
                MasterStackLayoutMode.Monocle => MasterStackLayoutMode.Floating,
                _ => MasterStackLayoutMode.MasterLeft
            }
        };
    }

    public void Validate()
    {
        if (MasterRatio is < 0.05 or > 0.95)
        {
            throw new ArgumentOutOfRangeException(nameof(MasterRatio), "The master ratio must be between 0.05 and 0.95.");
        }

        if (OuterGap < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(OuterGap), "The outer gap cannot be negative.");
        }

        if (InnerGap < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(InnerGap), "The inner gap cannot be negative.");
        }

        if (MasterCount is < 1 or > MaxMasterCount)
        {
            throw new ArgumentOutOfRangeException(nameof(MasterCount), $"The master count must be between 1 and {MaxMasterCount}.");
        }

        if (!Enum.IsDefined(Mode))
        {
            throw new ArgumentOutOfRangeException(nameof(Mode), "The layout mode is not supported.");
        }
    }
}
