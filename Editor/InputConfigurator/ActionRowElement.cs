using System;
using TMBS.Runtime.Config;
using UnityEngine;
using UnityEngine.UIElements;

namespace TMBS.Editor.InputConfigurator
{
    public sealed class ActionRowElement : VisualElement
    {
        private readonly TmbsInputActionId _actionId;
        private readonly Button _primaryBtn;
        private readonly Button _secondaryBtn;
        private readonly VisualElement _primaryChip;
        private readonly VisualElement _secondaryChip;
        private readonly Button _primaryClearBtn;
        private readonly Button _secondaryClearBtn;
        private readonly VisualElement _modBar;
        private readonly Label _modSlotLabel;
        private readonly Button _ctrlPill;
        private readonly Button _shiftPill;
        private readonly Button _altPill;
        private readonly Button _cmdPill;
        private readonly Label _conflictLabel;

        private TmbsActionBinding _currentBinding;
        private int _editingSlot = -1;

        public event Action<TmbsInputActionId, int> AssignRequested;
        public event Action<TmbsInputActionId, int> ClearRequested;
        public event Action<TmbsInputActionId, int, TmbsModifiers> ModifiersChanged;

        public ActionRowElement(TmbsInputActionId actionId)
        {
            _actionId = actionId;
            var meta = TmbsActionCatalog.Get(actionId);

            AddToClassList("tmbs-action-row");
            tooltip = meta.Description;

            var top = new VisualElement();
            top.AddToClassList("tmbs-action-top");
            Add(top);

            var info = new VisualElement();
            info.AddToClassList("tmbs-action-info");
            top.Add(info);

            var dot = new VisualElement();
            dot.AddToClassList("tmbs-action-dot");
            dot.style.backgroundColor = meta.AccentColor;
            info.Add(dot);

            var titleCol = new VisualElement();
            var nameLabel = new Label(meta.DisplayName);
            nameLabel.AddToClassList("tmbs-action-name");
            titleCol.Add(nameLabel);
            info.Add(titleCol);

            var slotsGroup = new VisualElement();
            slotsGroup.AddToClassList("tmbs-slot-group");
            top.Add(slotsGroup);

            CreateSlotChip(0, "Primary binding (click to assign)", out _primaryChip, out _primaryBtn, out _primaryClearBtn);
            CreateSlotChip(1, "Secondary binding (click to assign)", out _secondaryChip, out _secondaryBtn, out _secondaryClearBtn);
            slotsGroup.Add(_primaryChip);
            slotsGroup.Add(_secondaryChip);

            _modBar = new VisualElement();
            _modBar.AddToClassList("tmbs-mod-bar");
            _modBar.style.display = DisplayStyle.None;
            Add(_modBar);

            _modSlotLabel = new Label("Modifiers:");
            _modSlotLabel.style.fontSize = 10;
            _modSlotLabel.style.marginRight = 6;
            _modBar.Add(_modSlotLabel);

            _ctrlPill = CreateModPill("Ctrl", TmbsModifiers.Ctrl);
            _shiftPill = CreateModPill("Shift", TmbsModifiers.Shift);
            _altPill = CreateModPill("Alt", TmbsModifiers.Alt);
            _cmdPill = CreateModPill("Cmd", TmbsModifiers.Command);
            _modBar.Add(_ctrlPill);
            _modBar.Add(_shiftPill);
            _modBar.Add(_altPill);
            _modBar.Add(_cmdPill);

            _conflictLabel = new Label();
            _conflictLabel.AddToClassList("tmbs-conflict-text");
            _conflictLabel.style.display = DisplayStyle.None;
            Add(_conflictLabel);
        }

        private void CreateSlotChip(
            int slot,
            string chipTooltip,
            out VisualElement chip,
            out Button mainBtn,
            out Button clearBtn)
        {
            chip = new VisualElement();
            chip.AddToClassList("tmbs-slot-chip");
            chip.tooltip = chipTooltip;

            mainBtn = new Button(() => AssignRequested?.Invoke(_actionId, slot));
            mainBtn.AddToClassList("tmbs-slot-chip-btn");
            chip.Add(mainBtn);

            clearBtn = new Button(() => ClearRequested?.Invoke(_actionId, slot)) { text = "×" };
            clearBtn.AddToClassList("tmbs-slot-clear-btn");
            clearBtn.tooltip = "Clear this binding";
            chip.Add(clearBtn);
        }

        private Button CreateModPill(string label, TmbsModifiers modFlag)
        {
            var btn = new Button(() => ToggleModifier(modFlag)) { text = label };
            btn.AddToClassList("tmbs-mod-pill");
            return btn;
        }

        private void ToggleModifier(TmbsModifiers modFlag)
        {
            if (_editingSlot < 0 || _editingSlot > 1)
                return;

            var kb = _currentBinding.GetSlot(_editingSlot);
            var nextMods = kb.modifiers ^ modFlag;
            ModifiersChanged?.Invoke(_actionId, _editingSlot, nextMods);
        }

        public void Bind(TmbsLegacyBindings bindings, int assigningSlot)
        {
            _currentBinding = bindings.Get(_actionId);
            _editingSlot = assigningSlot;

            bool primaryConflict = bindings.HasConflict(_actionId, 0, out var c0Act, out _);
            bool secondaryConflict = bindings.HasConflict(_actionId, 1, out var c1Act, out _);

            UpdateSlotVisual(0, _currentBinding.primary, _primaryChip, _primaryBtn, _primaryClearBtn, assigningSlot == 0, primaryConflict);
            UpdateSlotVisual(1, _currentBinding.secondary, _secondaryChip, _secondaryBtn, _secondaryClearBtn, assigningSlot == 1, secondaryConflict);

            EnableInClassList("tmbs-action-row--assigning", assigningSlot >= 0);
            EnableInClassList("tmbs-action-row--conflict", primaryConflict || secondaryConflict);

            if (assigningSlot >= 0)
            {
                var slotBinding = _currentBinding.GetSlot(assigningSlot);
                _modBar.style.display = slotBinding.IsBound ? DisplayStyle.Flex : DisplayStyle.None;
                _modSlotLabel.text = assigningSlot == 0 ? "Primary modifiers:" : "Secondary modifiers:";
                UpdateModPill(_ctrlPill, slotBinding.modifiers, TmbsModifiers.Ctrl);
                UpdateModPill(_shiftPill, slotBinding.modifiers, TmbsModifiers.Shift);
                UpdateModPill(_altPill, slotBinding.modifiers, TmbsModifiers.Alt);
                UpdateModPill(_cmdPill, slotBinding.modifiers, TmbsModifiers.Command);
            }
            else
            {
                _modBar.style.display = DisplayStyle.None;
            }

            if (primaryConflict)
            {
                _conflictLabel.text = $"⚠ Conflict with '{TmbsActionCatalog.Get(c0Act).DisplayName}' ({_currentBinding.primary.ToDisplayString()})";
                _conflictLabel.style.display = DisplayStyle.Flex;
            }
            else if (secondaryConflict)
            {
                _conflictLabel.text = $"⚠ Conflict with '{TmbsActionCatalog.Get(c1Act).DisplayName}' ({_currentBinding.secondary.ToDisplayString()})";
                _conflictLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                _conflictLabel.style.display = DisplayStyle.None;
            }
        }

        private static void UpdateSlotVisual(
            int slot,
            in TmbsKeyBinding kb,
            VisualElement chip,
            Button mainBtn,
            Button clearBtn,
            bool isAssigning,
            bool hasConflict)
        {
            if (isAssigning)
            {
                mainBtn.text = kb.IsBound ? $"[ {kb.ToDisplayString()} ]" : "[ Press / Click… ]";
            }
            else
            {
                mainBtn.text = kb.IsBound ? kb.ToDisplayString() : (slot == 0 ? "Unbound" : "+ Alt");
            }

            mainBtn.style.opacity = (!kb.IsBound && !isAssigning) ? 0.65f : 1f;
            clearBtn.style.display = kb.IsBound ? DisplayStyle.Flex : DisplayStyle.None;

            chip.EnableInClassList("tmbs-slot-chip--assigning", isAssigning);
            chip.EnableInClassList("tmbs-slot-chip--conflict", hasConflict);
        }

        private static void UpdateModPill(Button pill, TmbsModifiers activeMods, TmbsModifiers flag)
        {
            pill.EnableInClassList("tmbs-mod-pill--on", (activeMods & flag) != 0);
        }
    }
}
