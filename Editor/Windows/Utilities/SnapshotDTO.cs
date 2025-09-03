using System;
using System.Collections.Generic;

namespace BattleTurn.StyledLog.Editor
{
    [Serializable]
    internal sealed class SnapshotDTO
    {
        public List<EntryDTO> all;
    }
}