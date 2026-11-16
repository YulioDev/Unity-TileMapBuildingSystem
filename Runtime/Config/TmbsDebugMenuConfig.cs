using System;
using UnityEngine;

namespace TMBS.Runtime.Config
{
    [Serializable]
    public sealed class TmbsDebugMenuConfig
    {
        [Tooltip("Enables or disables the debug menu overlay and shortcut handling.")]
        public bool enabled = true;

        [Tooltip("Requires holding the Control key for the toggle shortcut (Ctrl + key).")]
        public bool requireControl = true;

        [Tooltip("Key used to toggle the debug menu (defaults to Slash '/').")]
        public KeyCode toggleKey = KeyCode.Slash;

        [Min(1)]
        [Tooltip("Work units added per manual work step.")]
        public int defaultWorkIncrement = 1;

        [Min(1)]
        [Tooltip("Resource units delivered per manual resource step.")]
        public int defaultResourceIncrement = 1;

        [Range(0.05f, 2.0f)]
        [Tooltip("Interval in seconds between automatic worker steps.")]
        public float autoTickInterval = 0.2f;
    }
}
