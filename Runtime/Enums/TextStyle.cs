using System;

namespace BattleTurn.StyledLog
{
    [Flags]
    public enum TextStyle
    {
        None = 0,
        Bold = 1 << 0,
        Underline = 1 << 1,
        Strikethrough = 1 << 2
    }
}