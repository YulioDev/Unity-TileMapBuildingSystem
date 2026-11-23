using System;
using System.Collections.Generic;
using TMBS.Core.Input;
using TMBS.Core.Intents;
using TMBS.Runtime.Config;
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
        private readonly TmbsLegacyBindings _bindings;
        private readonly ILegacyInputSource _inputSource;
        private readonly List<TmbsInputActionId> _shortcutBuffer = new List<TmbsInputActionId>(4);

        private bool _isActive;
        private bool _isDragging;
        private bool _dragAlternate;

        public LegacyMouseAdapter(Func<Camera> cameraProvider, Func<Plane> constructionPlaneProvider = null)
            : this(cameraProvider, constructionPlaneProvider, null, null)
        {
        }

        public LegacyMouseAdapter(
            Func<Camera> cameraProvider,
            Func<Plane> constructionPlaneProvider,
            TmbsLegacyBindings bindings,
            ILegacyInputSource inputSource = null)
        {
            _cameraProvider = cameraProvider ?? (() => Camera.main);
            _constructionPlaneProvider = constructionPlaneProvider ?? (() => new Plane(Vector3.back, Vector3.zero));
            _bindings = bindings ?? TmbsLegacyBindings.CreateDefault();
            _inputSource = inputSource ?? UnityLegacyInputSource.Instance;
        }

        public TmbsLegacyBindings Bindings => _bindings;

        public InputCapabilities Capabilities => new InputCapabilities(
            hasPoint: _inputSource.IsAvailable,
            hasConfirm: _bindings.place.IsBound,
            hasCancel: _bindings.cancel.IsBound,
            hasDrag: _bindings.place.IsBound,
            hasUndo: _bindings.undo.IsBound,
            hasRedo: _bindings.redo.IsBound,
            hasAlternateModifier: _bindings.eraseModifier.IsBound,
            hasRotate: _bindings.rotate.IsBound,
            hasMirror: _bindings.mirror.IsBound);

        public event Action<BuildIntent> BuildIntentRaised;

        public void Enable()
        {
            _isActive = true;
            if (!_inputSource.IsAvailable && _inputSource is UnityLegacyInputSource unitySource)
            {
                unitySource.LogUnavailableOnce();
            }
        }

        public void Disable()
        {
            _isActive = false;
            _isDragging = false;
            _dragAlternate = false;
        }

        public void Tick(float deltaTime)
        {
            if (!_isActive || !_inputSource.IsAvailable)
                return;

            if (!InputPlaneProjection.TryResolveWorldPoint(
                    _inputSource.MousePosition,
                    _cameraProvider,
                    _constructionPlaneProvider,
                    out Vector3 mouseWorldPos))
                return;

            bool isAlternate = LegacyBindingEvaluator.IsModifierBindingHeld(_inputSource, in _bindings.eraseModifier);

            LegacyBindingEvaluator.EvaluateShortcuts(_inputSource, _bindings, _shortcutBuffer);
            for (int i = 0; i < _shortcutBuffer.Count; i++)
            {
                BuildIntentType intentType;
                switch (_shortcutBuffer[i])
                {
                    case TmbsInputActionId.Undo: intentType = BuildIntentType.Undo; break;
                    case TmbsInputActionId.Redo: intentType = BuildIntentType.Redo; break;
                    case TmbsInputActionId.Rotate: intentType = BuildIntentType.Rotate; break;
                    case TmbsInputActionId.Mirror: intentType = BuildIntentType.Mirror; break;
                    default: continue;
                }

                BuildIntentRaised?.Invoke(new BuildIntent(intentType, mouseWorldPos, false));
            }

            if (LegacyBindingEvaluator.WasActionPressed(_inputSource, in _bindings.cancel))
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

            bool placePressed = LegacyBindingEvaluator.WasActionPressed(_inputSource, in _bindings.place);
            bool placeReleased = LegacyBindingEvaluator.WasActionTriggerReleased(_inputSource, in _bindings.place);
            bool placeHeld = LegacyBindingEvaluator.IsActionTriggerHeld(_inputSource, in _bindings.place);

            if (placePressed)
            {
                _isDragging = true;
                _dragAlternate = isAlternate;
                BuildIntentRaised?.Invoke(new BuildIntent(BuildIntentType.DragStart, mouseWorldPos, _dragAlternate));

                if (placeReleased)
                {
                    _isDragging = false;
                    BuildIntentRaised?.Invoke(new BuildIntent(BuildIntentType.DragEnd, mouseWorldPos, _dragAlternate));
                    BuildIntentRaised?.Invoke(new BuildIntent(BuildIntentType.Confirm, mouseWorldPos, _dragAlternate));
                    _dragAlternate = false;
                }
            }
            else if (_isDragging && placeReleased)
            {
                _isDragging = false;
                BuildIntentRaised?.Invoke(new BuildIntent(BuildIntentType.DragEnd, mouseWorldPos, _dragAlternate));
                BuildIntentRaised?.Invoke(new BuildIntent(BuildIntentType.Confirm, mouseWorldPos, _dragAlternate));
                _dragAlternate = false;
            }
            else if (_isDragging && placeHeld)
            {
                BuildIntentRaised?.Invoke(new BuildIntent(BuildIntentType.DragUpdate, mouseWorldPos, _dragAlternate));
            }
            else if (!_isDragging)
            {
                BuildIntentRaised?.Invoke(new BuildIntent(BuildIntentType.PointMove, mouseWorldPos, isAlternate));
            }
        }
    }
}
