
using UnityEngine;

namespace BattleTurn.StyledLog.Editor
{
    internal sealed class Entry
    {
        public LogType type;
        public string tag;
        public string rich;   // message (rich text without <font>)
        public Font font;     // per-row Unity Font
        public string stack;  // raw stacktrace
        public int count = 1; // collapse count
    }
}