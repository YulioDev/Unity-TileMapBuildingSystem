using System;
using TMBS.Core.Input;
using TMBS.Core.Intents;
using UnityEngine;

namespace TMBS.Unity.Input
{
    public sealed class LegacyMouseBuildInputAdapter : ITickableInputAdapter
    {
        private readonly Func<Camera> _cameraProvider;
        private readonly System.Func<UnityEngine.Plane> _constructionPlaneProvider;
        private bool _isActive;
        private bool _isDragging;
        private bool _dragAlternate;

        public LegacyMouseBuildInputAdapter(Func<Camera> cameraProvider, System.Func<UnityEngine.Plane> constructionPlaneProvider = null)
        {
            _cameraProvider = cameraProvider ?? (() => Camera.main);
            _constructionPlaneProvider = constructionPlaneProvider ?? (() => new UnityEngine.Plane(Vector3.back, Vector3.zero));
        }

        public InputCapabilities Capabilities => new InputCapabilities(
            hasPoint: true,
            hasConfirm: true,
            hasCancel: true,
            hasDrag: true,
            hasUndo: true,
            hasRedo: true,
            hasPipette: false,
            hasAlternateModifier: true);

        public event Action<BuildIntent> BuildIntentRaised;

        public void Enable()
        {
            _isActive = true;
        }

        public void Disable()
        {
            _isActive = false;
            _isDragging = false;
            _dragAlternate = false;
        }

        public void Tick(float deltaTime)
        {
            if (!_isActive)
                return;

            var camera = _cameraProvider();

            if (camera == null)
                return;

            Vector3 mouseWorldPos;

            var ray = camera.ScreenPointToRay(UnityEngine.Input.mousePosition);
            var plane = _constructionPlaneProvider();
            if (plane.Raycast(ray, out var enter))
            {
                mouseWorldPos = ray.GetPoint(enter);
            }
            else
            {
                // Fallback to previous behavior if raycast fails
                mouseWorldPos = camera.ScreenToWorldPoint(UnityEngine.Input.mousePosition);
                mouseWorldPos.z = 0f;
            }

            bool isAlternate =
                UnityEngine.Input.GetKey(KeyCode.LeftShift) ||
                UnityEngine.Input.GetKey(KeyCode.RightShift);

            bool isCtrl =
                UnityEngine.Input.GetKey(KeyCode.LeftControl) ||
                UnityEngine.Input.GetKey(KeyCode.RightControl);

            if (isCtrl && UnityEngine.Input.GetKeyDown(KeyCode.Z))
            {
                if (isAlternate)
                    BuildIntentRaised?.Invoke(new BuildIntent(BuildIntentType.Redo, mouseWorldPos, false));
                else
                    BuildIntentRaised?.Invoke(new BuildIntent(BuildIntentType.Undo, mouseWorldPos, false));

                return;
            }

            if (isCtrl && UnityEngine.Input.GetKeyDown(KeyCode.Y))
            {
                BuildIntentRaised?.Invoke(new BuildIntent(BuildIntentType.Redo, mouseWorldPos, false));
                return;
            }

            if (UnityEngine.Input.GetMouseButtonDown(1))
            {
                _isDragging = false;
                BuildIntentRaised?.Invoke(new BuildIntent(BuildIntentType.Cancel, mouseWorldPos, false));
                return;
            }

            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                _isDragging = true;
                _dragAlternate = isAlternate;
                BuildIntentRaised?.Invoke(new BuildIntent(BuildIntentType.DragStart, mouseWorldPos, _dragAlternate));
            }
            else if (UnityEngine.Input.GetMouseButton(0) && _isDragging)
            {
                BuildIntentRaised?.Invoke(new BuildIntent(BuildIntentType.DragUpdate, mouseWorldPos, _dragAlternate));
            }
            else if (UnityEngine.Input.GetMouseButtonUp(0) && _isDragging)
            {
                _isDragging = false;
                BuildIntentRaised?.Invoke(new BuildIntent(BuildIntentType.DragEnd, mouseWorldPos, _dragAlternate));
                BuildIntentRaised?.Invoke(new BuildIntent(BuildIntentType.Confirm, mouseWorldPos, _dragAlternate));
                _dragAlternate = false;
            }
            else if (!_isDragging)
            {
                BuildIntentRaised?.Invoke(new BuildIntent(BuildIntentType.PointMove, mouseWorldPos, isAlternate));
            }
        }
    }
}