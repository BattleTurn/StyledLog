using UnityEngine;

namespace Colorful.ScriptableObjects
{
    public abstract class StringBuilderSO : ScriptableObject
    {
        public abstract string GetStringBuilderAppends(params object[] parameters);
    }
}