using UnityEngine;

namespace Colorful.ScriptableObjects
{
    public abstract class FormatSO : ScriptableObject
    {
        public abstract Debug.FormatDelegate GetFormat();
    }
}