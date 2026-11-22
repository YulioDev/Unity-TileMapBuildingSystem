using System;
using UnityEngine;

namespace TMBS.Unity.Input
{
    public interface ILegacyInputSource
    {
        bool IsAvailable { get; }
        Vector2 MousePosition { get; }
        bool GetKey(KeyCode key);
        bool GetKeyDown(KeyCode key);
        bool GetKeyUp(KeyCode key);
        bool GetMouseButton(int button);
        bool GetMouseButtonDown(int button);
        bool GetMouseButtonUp(int button);
    }

    public sealed class UnityLegacyInputSource : ILegacyInputSource
    {
        public static readonly UnityLegacyInputSource Instance = new UnityLegacyInputSource();

        private bool _runtimeUnavailable;
        private bool _loggedUnavailableWarning;

        private UnityLegacyInputSource() { }

        public bool IsAvailable
        {
            get
            {
#if ENABLE_LEGACY_INPUT_MANAGER
                return !_runtimeUnavailable;
#else
                return false;
#endif
            }
        }

        public Vector2 MousePosition
        {
            get
            {
#if ENABLE_LEGACY_INPUT_MANAGER
                if (_runtimeUnavailable)
                    return Vector2.zero;
                try
                {
                    return UnityEngine.Input.mousePosition;
                }
                catch (InvalidOperationException)
                {
                    MarkRuntimeUnavailable();
                    return Vector2.zero;
                }
#else
                return Vector2.zero;
#endif
            }
        }

        public bool GetKey(KeyCode key)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (_runtimeUnavailable || key == KeyCode.None)
                return false;
            try
            {
                return UnityEngine.Input.GetKey(key);
            }
            catch (InvalidOperationException)
            {
                MarkRuntimeUnavailable();
                return false;
            }
#else
            return false;
#endif
        }

        public bool GetKeyDown(KeyCode key)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (_runtimeUnavailable || key == KeyCode.None)
                return false;
            try
            {
                return UnityEngine.Input.GetKeyDown(key);
            }
            catch (InvalidOperationException)
            {
                MarkRuntimeUnavailable();
                return false;
            }
#else
            return false;
#endif
        }

        public bool GetKeyUp(KeyCode key)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (_runtimeUnavailable || key == KeyCode.None)
                return false;
            try
            {
                return UnityEngine.Input.GetKeyUp(key);
            }
            catch (InvalidOperationException)
            {
                MarkRuntimeUnavailable();
                return false;
            }
#else
            return false;
#endif
        }

        public bool GetMouseButton(int button)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (_runtimeUnavailable || button < 0)
                return false;
            try
            {
                return UnityEngine.Input.GetMouseButton(button);
            }
            catch (InvalidOperationException)
            {
                MarkRuntimeUnavailable();
                return false;
            }
#else
            return false;
#endif
        }

        public bool GetMouseButtonDown(int button)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (_runtimeUnavailable || button < 0)
                return false;
            try
            {
                return UnityEngine.Input.GetMouseButtonDown(button);
            }
            catch (InvalidOperationException)
            {
                MarkRuntimeUnavailable();
                return false;
            }
#else
            return false;
#endif
        }

        public bool GetMouseButtonUp(int button)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (_runtimeUnavailable || button < 0)
                return false;
            try
            {
                return UnityEngine.Input.GetMouseButtonUp(button);
            }
            catch (InvalidOperationException)
            {
                MarkRuntimeUnavailable();
                return false;
            }
#else
            return false;
#endif
        }

        internal void LogUnavailableOnce()
        {
            if (_loggedUnavailableWarning)
                return;

            _loggedUnavailableWarning = true;
            Debug.LogWarning(
                "TMBS: Legacy Input Manager is not active in Player Settings (Active Input Handling). " +
                "Switch TMBS Input Mode to InputActions or enable Both / Input Manager (Old).");
        }

        private void MarkRuntimeUnavailable()
        {
            _runtimeUnavailable = true;
            LogUnavailableOnce();
        }
    }
}
