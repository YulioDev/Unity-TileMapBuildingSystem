using System;
using TMBS.Core.Input;
using TMBS.Core.Intents;
using UnityEngine;

namespace TMBS.Unity.Input
{
    internal static class InputPlaneProjection
    {
        public static bool TryResolveWorldPoint(
            Vector2 screenPos,
            Func<Camera> cameraProvider,
            Func<Plane> planeProvider,
            out Vector3 worldPoint)
        {
            var camera = cameraProvider != null ? cameraProvider() : null;
            return TryResolveWorldPoint(screenPos, camera, planeProvider, out worldPoint);
        }

        public static bool TryResolveWorldPoint(
            Vector2 screenPos,
            Camera camera,
            Func<Plane> planeProvider,
            out Vector3 worldPoint)
        {
            if (camera == null)
            {
                worldPoint = Vector3.zero;
                return false;
            }

            var screenPoint = new Vector3(screenPos.x, screenPos.y, 0f);
            var ray = camera.ScreenPointToRay(screenPoint);
            var plane = planeProvider != null
                ? planeProvider()
                : new Plane(Vector3.back, Vector3.zero);

            if (plane.Raycast(ray, out float enter))
            {
                worldPoint = ray.GetPoint(enter);
                return true;
            }

            worldPoint = camera.ScreenToWorldPoint(screenPoint);
            worldPoint.z = 0f;
            return true;
        }
    }

    public sealed class LegacyMouseAdapter : IUpdatableInput
    {
        private readonly Func<Camera> _cameraProvider;
        private readonly Func<Plane> _constructionPlaneProvider;
        private bool _isActive;
        private bool _isDragging;
        private bool _dragAlternate;

        public LegacyMouseAdapter(Func<Camera> cameraProvider, Func<Plane> constructionPlaneProvider = null)
        {
            _cameraProvider = cameraProvider ?? (() => Camera.main);
            _constructionPlaneProvider = constructionPlaneProvider ?? (() => new Plane(Vector3.back, Vector3.zero));
        }

        public InputCapabilities Capabilities => new InputCapabilities(
            hasPoint: true,
            hasConfirm: true,
            hasCancel: true,
            hasDrag: true,
            hasUndo: true,
            hasRedo: true,
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

            if (!InputPlaneProjection.TryResolveWorldPoint(
                    UnityEngine.Input.mousePosition,
                    _cameraProvider,
                    _constructionPlaneProvider,
                    out Vector3 mouseWorldPos))
                return;

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

            if (!isCtrl && UnityEngine.Input.GetKeyDown(KeyCode.R))
            {
                BuildIntentRaised?.Invoke(new BuildIntent(BuildIntentType.Rotate, mouseWorldPos, false));
                return;
            }

            if (!isCtrl && UnityEngine.Input.GetKeyDown(KeyCode.F))
            {
                BuildIntentRaised?.Invoke(new BuildIntent(BuildIntentType.Mirror, mouseWorldPos, false));
                return;
            }

            if (UnityEngine.Input.GetMouseButtonDown(1))
            {
                if (_isDragging)
                {
                    BuildIntentRaised?.Invoke(new BuildIntent(BuildIntentType.DragEnd, mouseWorldPos, _dragAlternate));
                }
                _isDragging = false;
                _dragAlternate = false;
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
