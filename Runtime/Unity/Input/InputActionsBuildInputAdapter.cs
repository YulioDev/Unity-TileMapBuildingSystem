#if ENABLE_INPUT_SYSTEM
using System;
using TMBS.Core.Input;
using TMBS.Core.Intents;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TMBS.Unity.Input
{
    /// <summary>
    /// IBuildInputAdapter implementation that reads from Unity Input System
    /// InputActionReferences and translates them into BuildIntents.
    /// Mirrors the drag lifecycle logic of LegacyMouseBuildInputAdapter.
    /// </summary>
    public sealed class InputActionsBuildInputAdapter : ITickableInputAdapter
    {
        private readonly Func<Camera> _cameraProvider;
        private readonly Func<Plane> _constructionPlaneProvider;

        private readonly InputActionReference _pointRef;
        private readonly InputActionReference _dragRef;
        private readonly InputActionReference _cancelRef;
        private readonly InputActionReference _undoRef;
        private readonly InputActionReference _redoRef;
        private readonly InputActionReference _alternateRef;

        private bool _isActive;
        private bool _isDragging;
        private bool _dragAlternate;

        public InputActionsBuildInputAdapter(
            Func<Camera> cameraProvider,
            Func<Plane> constructionPlaneProvider,
            InputActionReference pointRef,
            InputActionReference dragRef,
            InputActionReference cancelRef,
            InputActionReference undoRef,
            InputActionReference redoRef,
            InputActionReference alternateRef)
        {
            _cameraProvider = cameraProvider ?? (() => Camera.main);
            _constructionPlaneProvider = constructionPlaneProvider
                ?? (() => new Plane(Vector3.back, Vector3.zero));

            _pointRef = pointRef;
            _dragRef = dragRef;
            _cancelRef = cancelRef;
            _undoRef = undoRef;
            _redoRef = redoRef;
            _alternateRef = alternateRef;
        }

        public InputCapabilities Capabilities => new InputCapabilities(
            hasPoint:              _pointRef?.action != null,
            hasConfirm:            _dragRef?.action != null,
            hasCancel:             _cancelRef?.action != null,
            hasDrag:               _dragRef?.action != null,
            hasUndo:               _undoRef?.action != null,
            hasRedo:               _redoRef?.action != null,
            hasPipette:            false,
            hasAlternateModifier:  _alternateRef?.action != null);

        public event Action<BuildIntent> BuildIntentRaised;

        public void Enable()
        {
            _isActive = true;
            EnableAction(_pointRef);
            EnableAction(_dragRef);
            EnableAction(_cancelRef);
            EnableAction(_undoRef);
            EnableAction(_redoRef);
            EnableAction(_alternateRef);
        }

        public void Disable()
        {
            _isActive = false;
            _isDragging = false;
            _dragAlternate = false;
        }

        public void Tick(float deltaTime)
        {
            if (!_isActive) return;

            var camera = _cameraProvider();
            if (camera == null) return;

            Vector3 mouseWorldPos = ResolveWorldPosition(camera);
            bool isAlternate = IsActionPressed(_alternateRef);

            // Undo
            if (WasActionPerformed(_undoRef))
            {
                BuildIntentRaised?.Invoke(
                    new BuildIntent(BuildIntentType.Undo, mouseWorldPos, false));
                return;
            }

            // Redo
            if (WasActionPerformed(_redoRef))
            {
                BuildIntentRaised?.Invoke(
                    new BuildIntent(BuildIntentType.Redo, mouseWorldPos, false));
                return;
            }

            // Cancel
            if (WasActionPerformed(_cancelRef))
            {
                if (_isDragging)
                {
                    BuildIntentRaised?.Invoke(
                        new BuildIntent(BuildIntentType.DragEnd, mouseWorldPos, _dragAlternate));
                }
                _isDragging = false;
                _dragAlternate = false;
                BuildIntentRaised?.Invoke(
                    new BuildIntent(BuildIntentType.Cancel, mouseWorldPos, false));
                return;
            }

            // Drag lifecycle (mirrors LegacyMouseBuildInputAdapter)
            if (WasActionPerformed(_dragRef))
            {
                _isDragging = true;
                _dragAlternate = isAlternate;
                BuildIntentRaised?.Invoke(
                    new BuildIntent(BuildIntentType.DragStart, mouseWorldPos, _dragAlternate));
            }
            else if (IsActionPressed(_dragRef) && _isDragging)
            {
                BuildIntentRaised?.Invoke(
                    new BuildIntent(BuildIntentType.DragUpdate, mouseWorldPos, _dragAlternate));
            }
            else if (WasActionReleased(_dragRef) && _isDragging)
            {
                _isDragging = false;
                BuildIntentRaised?.Invoke(
                    new BuildIntent(BuildIntentType.DragEnd, mouseWorldPos, _dragAlternate));
                BuildIntentRaised?.Invoke(
                    new BuildIntent(BuildIntentType.Confirm, mouseWorldPos, _dragAlternate));
                _dragAlternate = false;
            }
            else if (!_isDragging)
            {
                BuildIntentRaised?.Invoke(
                    new BuildIntent(BuildIntentType.PointMove, mouseWorldPos, isAlternate));
            }
        }

        private Vector3 ResolveWorldPosition(Camera camera)
        {
            Vector2 screenPos = _pointRef?.action?.ReadValue<Vector2>()
                                ?? Vector2.zero;

            var ray = camera.ScreenPointToRay(new Vector3(screenPos.x, screenPos.y, 0f));
            var plane = _constructionPlaneProvider();

            if (plane.Raycast(ray, out var enter))
                return ray.GetPoint(enter);

            Vector3 fallback = camera.ScreenToWorldPoint(
                new Vector3(screenPos.x, screenPos.y, 0f));
            fallback.z = 0f;
            return fallback;
        }

        private static void EnableAction(InputActionReference actionRef)
        {
            actionRef?.action?.Enable();
        }

        private static bool WasActionPerformed(InputActionReference actionRef)
        {
            return actionRef?.action?.WasPerformedThisFrame() ?? false;
        }

        private static bool WasActionReleased(InputActionReference actionRef)
        {
            return actionRef?.action?.WasReleasedThisFrame() ?? false;
        }

        private static bool IsActionPressed(InputActionReference actionRef)
        {
            return actionRef?.action?.IsPressed() ?? false;
        }
    }
}
#endif
