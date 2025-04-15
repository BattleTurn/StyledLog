using UnityEngine;

namespace Colorful.ScriptableObjects
{
    public abstract class StringBuilderSO : ScriptableObject
    {
        public abstract Debug.StringBuilderAppendDelegate GetStringBuilderAppends();
    }
}