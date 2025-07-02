using System.ComponentModel;

namespace HoopGameNight.Core.Enums
{
    public enum GameStatus
    {
        [Description("Scheduled")]
        Scheduled = 1,

        [Description("Live")]
        Live = 2,

        [Description("Final")]
        Final = 3,

        [Description("Postponed")]
        Postponed = 4,

        [Description("Cancelled")]
        Cancelled = 5
    }

    public enum PlayerPosition
    {
        [Description("Point Guard")]
        PG = 1,

        [Description("Shooting Guard")]
        SG = 2,

        [Description("Small Forward")]
        SF = 3,

        [Description("Power Forward")]
        PF = 4,

        [Description("Center")]
        C = 5,

        [Description("Guard")]
        G = 6,

        [Description("Forward")]
        F = 7
    }

    public enum Conference
    {
        [Description("Eastern Conference")]
        East = 1,

        [Description("Western Conference")]
        West = 2
    }

    public enum ApiVersion
    {
        V1 = 1,
        V2 = 2
    }
}