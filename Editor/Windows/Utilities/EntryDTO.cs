using System;

namespace BattleTurn.StyledLog.Editor
{
    [Serializable]
    internal struct EntryDTO
    {
        public int type;
        public string tag;
        public string rich;
        public string stack;
        public int count;
    }
}