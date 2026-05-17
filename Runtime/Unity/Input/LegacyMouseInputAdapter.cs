using System;
using TMBS.Core.Input;
using TMBS.Core.Intents;
using UnityEngine;

namespace TMBS.Unity.Input
{
    public class LegacyMouseInputAdapter : MonoBehaviour, IBuildInputAdapter
    {
        public InputCapabilities Capabilities => new InputCapabilities(
            hasPoint: true, hasConfirm: true, hasCancel: true, 
            hasDrag: true, hasUndo: false, hasRedo: false, 
            hasPipette: false, hasAlternateModifier: true);

        public event Action<BuildIntent> BuildIntentRaised;

        private bool _isActive;
        private bool _isDragging;
        private bool _dragAlternate;
        private Camera _mainCamera;

        public void Enable() 
        {
            _isActive = true;
            _mainCamera = Camera.main;
        }
        
        public void Disable() 
        {
            _isActive = false;
            _isDragging = false;
            _dragAlternate = false;
        }

        private void Update()
        {
            if (!_isActive || _mainCamera == null) return;

            Vector3 mouseWorldPos = _mainCamera.ScreenToWorldPoint(UnityEngine.Input.mousePosition);
            mouseWorldPos.z = 0f; 

            
            bool isAlternate = UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift);

            
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

