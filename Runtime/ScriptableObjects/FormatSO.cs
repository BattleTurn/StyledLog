using UnityEngine;

namespace Colorful.ScriptableObjects
{
    public abstract class FormatSO : ScriptableObject
    {
        public abstract string GetFormat(string message, params object[] parameters);
    }
}