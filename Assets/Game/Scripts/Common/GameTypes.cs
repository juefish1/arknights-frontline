namespace ArknightsFrontline.Common
{
    public enum TeamId
    {
        Blue = 0,
        Red = 1
    }

    public enum Altitude
    {
        Ground = 0,
        Air = 1
    }

    public enum UnitKind
    {
        Operator = 0,
        Minion = 1,
        Tower = 2
    }

    public enum MatchState
    {
        Preparing = 0,
        Running = 1,
        Finished = 2
    }

    public enum MatchOutcome
    {
        BlueVictory = 0,
        RedVictory = 1,
        Draw = 2
    }
}
