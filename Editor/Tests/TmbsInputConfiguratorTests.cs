using System;
using System.Collections.Generic;
using NUnit.Framework;
using TMBS.Core.Intents;
using TMBS.Runtime.Config;
using TMBS.Unity.Input;
using UnityEditor;
using UnityEngine;

namespace TMBS.Editor.InputConfigurator.Tests
{
    [TestFixture]
    public sealed class TmbsInputConfiguratorTests
    {
        private sealed class FakeLegacyInputSource : ILegacyInputSource
        {
            public bool IsAvailable { get; set; } = true;
            public Vector2 MousePosition { get; set; } = new Vector2(100f, 100f);

            public readonly HashSet<KeyCode> KeysHeld = new HashSet<KeyCode>();
            public readonly HashSet<KeyCode> KeysDown = new HashSet<KeyCode>();
            public readonly HashSet<KeyCode> KeysUp = new HashSet<KeyCode>();

            public readonly HashSet<int> MouseHeld = new HashSet<int>();
            public readonly HashSet<int> MouseDown = new HashSet<int>();
            public readonly HashSet<int> MouseUp = new HashSet<int>();

            public void ClearFrameTransitions()
            {
                KeysDown.Clear();
                KeysUp.Clear();
                MouseDown.Clear();
                MouseUp.Clear();
            }

            public bool GetKey(KeyCode key) => KeysHeld.Contains(key);
            public bool GetKeyDown(KeyCode key) => KeysDown.Contains(key);
            public bool GetKeyUp(KeyCode key) => KeysUp.Contains(key);
            public bool GetMouseButton(int button) => MouseHeld.Contains(button);
            public bool GetMouseButtonDown(int button) => MouseDown.Contains(button);
            public bool GetMouseButtonUp(int button) => MouseUp.Contains(button);
        }

        private GameObject _camGo;
        private Camera _camera;

        [SetUp]
        public void SetUp()
        {
            _camGo = new GameObject("TMBS_TestCamera");
            _camGo.transform.position = new Vector3(0f, 0f, -10f);
            _camera = _camGo.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 5f;
        }

        [TearDown]
        public void TearDown()
        {
            if (_camGo != null)
                UnityEngine.Object.DestroyImmediate(_camGo);
        }

        [Test]
        public void DefaultBindings_MatchOriginalHardcodedKeys()
        {
            var d = TmbsLegacyBindings.CreateDefault();

            Assert.AreEqual(TmbsBindingSource.MouseButton, d.place.primary.source);
            Assert.AreEqual(0, d.place.primary.mouseButton);

            Assert.AreEqual(TmbsBindingSource.MouseButton, d.cancel.primary.source);
            Assert.AreEqual(1, d.cancel.primary.mouseButton);

            Assert.AreEqual(KeyCode.LeftShift, d.eraseModifier.primary.key);
            Assert.AreEqual(KeyCode.RightShift, d.eraseModifier.secondary.key);

            Assert.AreEqual(KeyCode.Z, d.undo.primary.key);
            Assert.AreEqual(TmbsModifiers.Ctrl, d.undo.primary.modifiers);

            Assert.AreEqual(KeyCode.Y, d.redo.primary.key);
            Assert.AreEqual(TmbsModifiers.Ctrl, d.redo.primary.modifiers);
            Assert.AreEqual(KeyCode.Z, d.redo.secondary.key);
            Assert.AreEqual(TmbsModifiers.Ctrl | TmbsModifiers.Shift, d.redo.secondary.modifiers);

            Assert.AreEqual(KeyCode.R, d.rotate.primary.key);
            Assert.AreEqual(TmbsModifiers.None, d.rotate.primary.modifiers);

            Assert.AreEqual(KeyCode.F, d.mirror.primary.key);
            Assert.AreEqual(TmbsModifiers.None, d.mirror.primary.modifiers);

            var adapter = new LegacyMouseAdapter(() => _camera, () => new Plane(Vector3.back, Vector3.zero), d, new FakeLegacyInputSource());
            var caps = adapter.Capabilities;
            Assert.IsTrue(caps.HasPoint);
            Assert.IsTrue(caps.HasConfirm);
            Assert.IsTrue(caps.HasCancel);
            Assert.IsTrue(caps.HasDrag);
            Assert.IsTrue(caps.HasUndo);
            Assert.IsTrue(caps.HasRedo);
            Assert.IsTrue(caps.HasAlternateModifier);
        }

        [Test]
        public void Presets_HaveNoInternalConflicts_AndBindPlaceAndCancel()
        {
            foreach (TmbsInputPresetId id in Enum.GetValues(typeof(TmbsInputPresetId)))
            {
                var bindings = TmbsInputPresets.CreateBindings(id);
                Assert.IsTrue(bindings.place.IsBound, $"Preset {id} must bind Place.");
                Assert.IsTrue(bindings.cancel.IsBound, $"Preset {id} must bind Cancel.");
                Assert.AreEqual(0, bindings.CountConflictingSlots(), $"Preset {id} should have 0 conflicting slots.");
            }
        }

        [Test]
        public void ConflictDetection_IdentifiesSameKeyAndModifiers_IgnoresDifferentModifiers()
        {
            var bindings = TmbsLegacyBindings.CreateDefault();
            Assert.AreEqual(0, bindings.CountConflictingSlots());

            // Ctrl+Z (Undo) vs Ctrl+Shift+Z (Redo) must NOT conflict
            Assert.IsFalse(bindings.HasConflict(TmbsInputActionId.Undo, 0, out _, out _));

            // Make Mirror also Ctrl+Z -> must conflict with Undo
            bindings.SetSlot(TmbsInputActionId.Mirror, 0, TmbsKeyBinding.Key(KeyCode.Z, TmbsModifiers.Ctrl));
            Assert.IsTrue(bindings.HasConflict(TmbsInputActionId.Undo, 0, out var conflictAction, out int conflictSlot));
            Assert.AreEqual(TmbsInputActionId.Mirror, conflictAction);
            Assert.AreEqual(0, conflictSlot);
            Assert.AreEqual(2, bindings.CountConflictingSlots());
        }

        [Test]
        public void LegacyMouseAdapter_SameFrameClickAndRelease_EmitsDragStartDragEndConfirm_FixB1()
        {
            var src = new FakeLegacyInputSource();
            var adapter = new LegacyMouseAdapter(
                () => _camera,
                () => new Plane(Vector3.back, Vector3.zero),
                TmbsLegacyBindings.CreateDefault(),
                src);

            var emitted = new List<BuildIntentType>();
            adapter.BuildIntentRaised += intent => emitted.Add(intent.Type);
            adapter.Enable();

            // Simulate press and release in the exact same frame
            src.MouseDown.Add(0);
            src.MouseUp.Add(0);
            adapter.Tick(0.016f);

            CollectionAssert.AreEqual(
                new[] { BuildIntentType.DragStart, BuildIntentType.DragEnd, BuildIntentType.Confirm },
                emitted);

            // Next frame with no mouse buttons: must resume PointMove (not stuck dragging)
            emitted.Clear();
            src.ClearFrameTransitions();
            adapter.Tick(0.016f);

            CollectionAssert.AreEqual(new[] { BuildIntentType.PointMove }, emitted);
        }

        [Test]
        public void LegacyMouseAdapter_ShortcutDuringDragRelease_DoesNotDropRelease_FixB2()
        {
            var src = new FakeLegacyInputSource();
            var adapter = new LegacyMouseAdapter(
                () => _camera,
                () => new Plane(Vector3.back, Vector3.zero),
                TmbsLegacyBindings.CreateDefault(),
                src);

            var emitted = new List<BuildIntentType>();
            adapter.BuildIntentRaised += intent => emitted.Add(intent.Type);
            adapter.Enable();

            // Frame 1: start drag
            src.MouseDown.Add(0);
            src.MouseHeld.Add(0);
            adapter.Tick(0.016f);
            CollectionAssert.AreEqual(new[] { BuildIntentType.DragStart }, emitted);

            // Frame 2: press R (Rotate) AND release LMB in the same frame
            emitted.Clear();
            src.ClearFrameTransitions();
            src.MouseHeld.Remove(0);
            src.MouseUp.Add(0);
            src.KeysDown.Add(KeyCode.R);
            adapter.Tick(0.016f);

            CollectionAssert.AreEqual(
                new[] { BuildIntentType.Rotate, BuildIntentType.DragEnd, BuildIntentType.Confirm },
                emitted);
        }

        [Test]
        public void LegacyMouseAdapter_ModifierDisambiguation_CtrlShiftZ_FiresRedoOnly()
        {
            var src = new FakeLegacyInputSource();
            var adapter = new LegacyMouseAdapter(
                () => _camera,
                () => new Plane(Vector3.back, Vector3.zero),
                TmbsLegacyBindings.CreateDefault(),
                src);

            var emitted = new List<BuildIntentType>();
            adapter.BuildIntentRaised += intent => emitted.Add(intent.Type);
            adapter.Enable();

            src.KeysHeld.Add(KeyCode.LeftControl);
            src.KeysHeld.Add(KeyCode.LeftShift);
            src.KeysDown.Add(KeyCode.Z);
            adapter.Tick(0.016f);

            Assert.Contains(BuildIntentType.Redo, emitted);
            Assert.IsFalse(emitted.Contains(BuildIntentType.Undo), "Ctrl+Shift+Z must not trigger Undo.");
        }

        [Test]
        public void LegacyMouseAdapter_UnrequestedCtrl_BlocksUnmodifiedRotate_WhileShiftAllowsIt()
        {
            var src = new FakeLegacyInputSource();
            var adapter = new LegacyMouseAdapter(
                () => _camera,
                () => new Plane(Vector3.back, Vector3.zero),
                TmbsLegacyBindings.CreateDefault(),
                src);

            var emitted = new List<BuildIntentType>();
            adapter.BuildIntentRaised += intent => emitted.Add(intent.Type);
            adapter.Enable();

            // Ctrl + R must NOT trigger unmodified Rotate (R)
            src.KeysHeld.Add(KeyCode.LeftControl);
            src.KeysDown.Add(KeyCode.R);
            adapter.Tick(0.016f);
            Assert.IsFalse(emitted.Contains(BuildIntentType.Rotate), "Ctrl+R should not trigger unmodified Rotate.");

            // Shift + R MUST still trigger Rotate (Shift is used for EraseModifier)
            emitted.Clear();
            src.ClearFrameTransitions();
            src.KeysHeld.Clear();
            src.KeysHeld.Add(KeyCode.LeftShift);
            src.KeysDown.Add(KeyCode.R);
            adapter.Tick(0.016f);
            Assert.Contains(BuildIntentType.Rotate, emitted);
        }

#if ENABLE_INPUT_SYSTEM
        [Test]
        public void Generator_CreatesAssetAndAssignsAllEightReferences_AndParserRoundTrips()
        {
            const string tempPath = "Assets/TMBS/Editor/Tests/_TempTestActions.inputactions";
            var tempConfig = ScriptableObject.CreateInstance<TmbsRootConfig>();

            try
            {
                var template = TmbsInputPresets.CreateBindings(TmbsInputPresetId.ClassicPc);
                var asset = TmbsInputActionsGenerator.GenerateAndAssign(tempConfig, tempPath, template);

                Assert.IsNotNull(asset);
                Assert.IsNotNull(tempConfig.input.pointAction);
                Assert.IsNotNull(tempConfig.input.dragAction);
                Assert.IsNotNull(tempConfig.input.cancelAction);
                Assert.IsNotNull(tempConfig.input.undoAction);
                Assert.IsNotNull(tempConfig.input.redoAction);
                Assert.IsNotNull(tempConfig.input.alternateModifierAction);
                Assert.IsNotNull(tempConfig.input.rotateAction);
                Assert.IsNotNull(tempConfig.input.mirrorAction);

                var parsed = InputSystemBindingParser.ParseFromConfig(tempConfig.input);
                Assert.AreEqual(TmbsBindingSource.MouseButton, parsed.place.primary.source);
                Assert.AreEqual(0, parsed.place.primary.mouseButton);
                Assert.AreEqual(1, parsed.cancel.primary.mouseButton);
                Assert.AreEqual(KeyCode.Escape, parsed.cancel.secondary.key);
                Assert.AreEqual(KeyCode.R, parsed.rotate.primary.key);
                Assert.AreEqual(KeyCode.F, parsed.mirror.primary.key);
                Assert.AreEqual(KeyCode.Z, parsed.undo.primary.key);
                Assert.AreEqual(TmbsModifiers.Ctrl, parsed.undo.primary.modifiers);
                Assert.AreEqual(KeyCode.Y, parsed.redo.primary.key);
                Assert.AreEqual(TmbsModifiers.Ctrl, parsed.redo.primary.modifiers);
                Assert.AreEqual(KeyCode.Z, parsed.redo.secondary.key);
                Assert.AreEqual(TmbsModifiers.Ctrl | TmbsModifiers.Shift, parsed.redo.secondary.modifiers);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tempConfig);
                AssetDatabase.DeleteAsset(tempPath);
                AssetDatabase.Refresh();
            }
        }
#endif
    }
}
