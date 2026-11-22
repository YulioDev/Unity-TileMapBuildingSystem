using System;
using System.Collections.Generic;
using TMBS.Runtime.Config;
using UnityEngine;
using UnityEngine.UIElements;

namespace TMBS.Editor.InputConfigurator
{
    public sealed class KeyboardMapElement : VisualElement
    {
        private readonly struct KeySpec
        {
            public readonly KeyCode Code;
            public readonly string Label;
            public readonly float Weight;
            public readonly bool IsSpacer;

            public KeySpec(KeyCode code, string label, float weight = 1f)
            {
                Code = code;
                Label = label;
                Weight = weight;
                IsSpacer = false;
            }

            public static KeySpec Spacer(float weight) => new KeySpec(KeyCode.None, string.Empty, weight, true);

            private KeySpec(KeyCode code, string label, float weight, bool isSpacer)
            {
                Code = code;
                Label = label;
                Weight = weight;
                IsSpacer = isSpacer;
            }
        }

        private sealed class KeyVisual
        {
            public KeyCode Code;
            public Button Button;
            public VisualElement Stripes;
            public Label KeyLabel;
            public Label ActionBadge;
        }

        private static readonly KeySpec[][] MainRows =
        {
            new[]
            {
                new KeySpec(KeyCode.Escape, "Esc", 1.2f),
                KeySpec.Spacer(0.5f),
                new KeySpec(KeyCode.F1, "F1"), new KeySpec(KeyCode.F2, "F2"), new KeySpec(KeyCode.F3, "F3"), new KeySpec(KeyCode.F4, "F4"),
                KeySpec.Spacer(0.4f),
                new KeySpec(KeyCode.F5, "F5"), new KeySpec(KeyCode.F6, "F6"), new KeySpec(KeyCode.F7, "F7"), new KeySpec(KeyCode.F8, "F8"),
                KeySpec.Spacer(0.4f),
                new KeySpec(KeyCode.F9, "F9"), new KeySpec(KeyCode.F10, "F10"), new KeySpec(KeyCode.F11, "F11"), new KeySpec(KeyCode.F12, "F12")
            },
            new[]
            {
                new KeySpec(KeyCode.BackQuote, "`"),
                new KeySpec(KeyCode.Alpha1, "1"), new KeySpec(KeyCode.Alpha2, "2"), new KeySpec(KeyCode.Alpha3, "3"),
                new KeySpec(KeyCode.Alpha4, "4"), new KeySpec(KeyCode.Alpha5, "5"), new KeySpec(KeyCode.Alpha6, "6"),
                new KeySpec(KeyCode.Alpha7, "7"), new KeySpec(KeyCode.Alpha8, "8"), new KeySpec(KeyCode.Alpha9, "9"),
                new KeySpec(KeyCode.Alpha0, "0"),
                new KeySpec(KeyCode.Minus, "-"), new KeySpec(KeyCode.Equals, "="),
                new KeySpec(KeyCode.Backspace, "Back", 2.0f)
            },
            new[]
            {
                new KeySpec(KeyCode.Tab, "Tab", 1.5f),
                new KeySpec(KeyCode.Q, "Q"), new KeySpec(KeyCode.W, "W"), new KeySpec(KeyCode.E, "E"),
                new KeySpec(KeyCode.R, "R"), new KeySpec(KeyCode.T, "T"), new KeySpec(KeyCode.Y, "Y"),
                new KeySpec(KeyCode.U, "U"), new KeySpec(KeyCode.I, "I"), new KeySpec(KeyCode.O, "O"),
                new KeySpec(KeyCode.P, "P"),
                new KeySpec(KeyCode.LeftBracket, "["), new KeySpec(KeyCode.RightBracket, "]"),
                new KeySpec(KeyCode.Backslash, "\\", 1.5f)
            },
            new[]
            {
                new KeySpec(KeyCode.CapsLock, "Caps", 1.75f),
                new KeySpec(KeyCode.A, "A"), new KeySpec(KeyCode.S, "S"), new KeySpec(KeyCode.D, "D"),
                new KeySpec(KeyCode.F, "F"), new KeySpec(KeyCode.G, "G"), new KeySpec(KeyCode.H, "H"),
                new KeySpec(KeyCode.J, "J"), new KeySpec(KeyCode.K, "K"), new KeySpec(KeyCode.L, "L"),
                new KeySpec(KeyCode.Semicolon, ";"), new KeySpec(KeyCode.Quote, "'"),
                new KeySpec(KeyCode.Return, "Enter", 2.25f)
            },
            new[]
            {
                new KeySpec(KeyCode.LeftShift, "LShift", 2.25f),
                new KeySpec(KeyCode.Z, "Z"), new KeySpec(KeyCode.X, "X"), new KeySpec(KeyCode.C, "C"),
                new KeySpec(KeyCode.V, "V"), new KeySpec(KeyCode.B, "B"), new KeySpec(KeyCode.N, "N"),
                new KeySpec(KeyCode.M, "M"),
                new KeySpec(KeyCode.Comma, ","), new KeySpec(KeyCode.Period, "."), new KeySpec(KeyCode.Slash, "/"),
                new KeySpec(KeyCode.RightShift, "RShift", 2.75f)
            },
            new[]
            {
                new KeySpec(KeyCode.LeftControl, "LCtrl", 1.25f),
                new KeySpec(KeyCode.LeftCommand, "Cmd", 1.25f),
                new KeySpec(KeyCode.LeftAlt, "LAlt", 1.25f),
                new KeySpec(KeyCode.Space, "Space", 6.25f),
                new KeySpec(KeyCode.RightAlt, "RAlt", 1.25f),
                new KeySpec(KeyCode.RightCommand, "Cmd", 1.25f),
                new KeySpec(KeyCode.RightControl, "RCtrl", 1.25f)
            }
        };

        private static readonly KeySpec[][] NavRows =
        {
            new[] { new KeySpec(KeyCode.Home, "Home"), new KeySpec(KeyCode.End, "End"), KeySpec.Spacer(1f) },
            new[] { new KeySpec(KeyCode.Insert, "Ins"), new KeySpec(KeyCode.Delete, "Del"), KeySpec.Spacer(1f) },
            new[] { new KeySpec(KeyCode.PageUp, "PgUp"), new KeySpec(KeyCode.PageDown, "PgDn"), KeySpec.Spacer(1f) },
            new[] { KeySpec.Spacer(3f) },
            new[] { KeySpec.Spacer(1f), new KeySpec(KeyCode.UpArrow, "▲"), KeySpec.Spacer(1f) },
            new[] { new KeySpec(KeyCode.LeftArrow, "◀"), new KeySpec(KeyCode.DownArrow, "▼"), new KeySpec(KeyCode.RightArrow, "▶") }
        };

        private readonly List<KeyVisual> _keyVisuals = new List<KeyVisual>(90);
        private readonly VisualElement _assignBanner;
        private readonly Label _assignBannerText;
        private readonly Label _hintLabel;

        private bool _isReadOnly;
        private bool _isAssigning;

        public event Action<KeyCode> KeyClicked;
        public event Action CancelAssignRequested;

        public KeyboardMapElement(bool compact = false)
        {
            AddToClassList("tmbs-keyboard-box");

            _assignBanner = new VisualElement();
            _assignBanner.AddToClassList("tmbs-assign-banner");
            _assignBanner.style.display = DisplayStyle.None;
            Add(_assignBanner);

            _assignBannerText = new Label();
            _assignBannerText.AddToClassList("tmbs-assign-banner-text");
            _assignBanner.Add(_assignBannerText);

            var cancelBtn = new Button(() => CancelAssignRequested?.Invoke()) { text = "Cancel" };
            _assignBanner.Add(cancelBtn);

            var clusterWrap = new VisualElement();
            clusterWrap.AddToClassList("tmbs-kb-main-and-nav");
            Add(clusterWrap);

            var mainCol = new VisualElement();
            mainCol.AddToClassList("tmbs-kb-main");
            clusterWrap.Add(mainCol);

            for (int r = 0; r < MainRows.Length; r++)
            {
                mainCol.Add(BuildRow(MainRows[r], compact));
            }

            var navCol = new VisualElement();
            navCol.AddToClassList("tmbs-kb-nav");
            navCol.style.width = compact ? 54 : 78;
            clusterWrap.Add(navCol);

            for (int r = 0; r < NavRows.Length; r++)
            {
                navCol.Add(BuildRow(NavRows[r], compact));
            }

            _hintLabel = new Label("Click an action slot on the left, then click a key here to bind it.");
            _hintLabel.style.fontSize = 10;
            _hintLabel.style.opacity = 0.72f;
            _hintLabel.style.marginTop = 4;
            if (compact)
                _hintLabel.style.display = DisplayStyle.None;
            Add(_hintLabel);
        }

        private VisualElement BuildRow(KeySpec[] specs, bool compact)
        {
            var row = new VisualElement();
            row.AddToClassList("tmbs-kb-row");

            for (int i = 0; i < specs.Length; i++)
            {
                var spec = specs[i];
                if (spec.IsSpacer)
                {
                    var spacer = new VisualElement();
                    spacer.style.flexGrow = spec.Weight;
                    spacer.style.flexBasis = 0;
                    spacer.style.marginRight = 2;
                    row.Add(spacer);
                    continue;
                }

                var code = spec.Code;
                var btn = new Button(() =>
                {
                    if (!_isReadOnly)
                        KeyClicked?.Invoke(code);
                });
                btn.AddToClassList("tmbs-keycap");
                btn.AddToClassList("tmbs-keycap--clickable");
                btn.style.flexGrow = spec.Weight;
                btn.style.flexBasis = 0;
                if (compact)
                {
                    btn.style.height = 22;
                    btn.style.minWidth = 14;
                }

                var stripes = new VisualElement();
                stripes.AddToClassList("tmbs-keycap-stripes");
                btn.Add(stripes);

                var lbl = new Label(spec.Label);
                lbl.AddToClassList("tmbs-keycap-label");
                if (compact) lbl.style.fontSize = 8;
                btn.Add(lbl);

                var badge = new Label(string.Empty);
                badge.AddToClassList("tmbs-keycap-badge");
                if (compact) badge.style.display = DisplayStyle.None;
                btn.Add(badge);

                _keyVisuals.Add(new KeyVisual
                {
                    Code = code,
                    Button = btn,
                    Stripes = stripes,
                    KeyLabel = lbl,
                    ActionBadge = badge
                });

                row.Add(btn);
            }

            return row;
        }

        public void SetReadOnly(bool readOnly, string customHint = null)
        {
            _isReadOnly = readOnly;
            for (int i = 0; i < _keyVisuals.Count; i++)
            {
                _keyVisuals[i].Button.EnableInClassList("tmbs-keycap--clickable", !readOnly);
            }

            if (!string.IsNullOrEmpty(customHint))
                _hintLabel.text = customHint;
            else
                _hintLabel.text = readOnly
                    ? "Read-only visual preview of active Input System bindings."
                    : "Click an action slot on the left, then click a key here to bind it.";
        }

        public void SetAssigning(TmbsInputActionId? actionId, int slot)
        {
            _isAssigning = actionId.HasValue && !_isReadOnly;
            if (_isAssigning)
            {
                var meta = TmbsActionCatalog.Get(actionId.Value);
                string slotName = slot == 0 ? "Primary" : "Secondary";
                _assignBannerText.text = $"Click any key or mouse button (or press a key) → {meta.DisplayName} ({slotName})";
                _assignBanner.style.display = DisplayStyle.Flex;
            }
            else
            {
                _assignBanner.style.display = DisplayStyle.None;
            }
        }

        public void UpdateBindings(TmbsLegacyBindings bindings)
        {
            var matchedActions = new List<TmbsInputActionId>(4);
            var modifierUses = new List<TmbsInputActionId>(4);
            var tooltipLines = new List<string>(6);

            for (int i = 0; i < _keyVisuals.Count; i++)
            {
                var kv = _keyVisuals[i];
                matchedActions.Clear();
                modifierUses.Clear();
                tooltipLines.Clear();

                if (bindings != null)
                {
                    for (int a = 0; a < TmbsLegacyBindings.AllActionIds.Length; a++)
                    {
                        var actionId = TmbsLegacyBindings.AllActionIds[a];
                        var actionBinding = bindings.Get(actionId);
                        var meta = TmbsActionCatalog.Get(actionId);

                        for (int s = 0; s < 2; s++)
                        {
                            var kb = actionBinding.GetSlot(s);
                            if (!kb.IsBound || kb.source != TmbsBindingSource.Key)
                                continue;

                            if (kb.key == kv.Code)
                            {
                                if (!matchedActions.Contains(actionId))
                                    matchedActions.Add(actionId);
                                tooltipLines.Add($"{meta.DisplayName}: {kb.ToDisplayString()}");
                            }
                            else if (IsKeyForModifierFlag(kv.Code, kb.modifiers))
                            {
                                if (!modifierUses.Contains(actionId))
                                    modifierUses.Add(actionId);
                                tooltipLines.Add($"Modifier for {meta.DisplayName}: {kb.ToDisplayString()}");
                            }
                        }
                    }
                }

                kv.Stripes.Clear();
                if (matchedActions.Count > 0)
                {
                    var primaryColor = TmbsActionCatalog.Get(matchedActions[0]).AccentColor;
                    for (int m = 0; m < matchedActions.Count; m++)
                    {
                        var stripe = new VisualElement();
                        stripe.AddToClassList("tmbs-keycap-stripe");
                        stripe.style.backgroundColor = TmbsActionCatalog.Get(matchedActions[m]).AccentColor;
                        if (m == matchedActions.Count - 1)
                            stripe.style.marginRight = 0;
                        kv.Stripes.Add(stripe);
                    }

                    kv.Button.style.backgroundColor = new Color(primaryColor.r, primaryColor.g, primaryColor.b, 0.18f);
                    kv.Button.style.borderTopColor = primaryColor;
                    kv.Button.style.borderLeftColor = StyleKeyword.Null;
                    kv.Button.style.borderRightColor = StyleKeyword.Null;

                    kv.ActionBadge.text = matchedActions.Count == 1
                        ? TmbsActionCatalog.Get(matchedActions[0]).BadgeText
                        : $"{matchedActions.Count}×";
                }
                else if (modifierUses.Count > 0)
                {
                    var modColor = TmbsActionCatalog.Get(modifierUses[0]).AccentColor;
                    var modStripe = new VisualElement();
                    modStripe.AddToClassList("tmbs-keycap-stripe");
                    modStripe.style.height = 2;
                    modStripe.style.opacity = 0.7f;
                    modStripe.style.marginRight = 0;
                    modStripe.style.backgroundColor = modColor;
                    kv.Stripes.Add(modStripe);

                    kv.Button.style.backgroundColor = StyleKeyword.Null;
                    kv.Button.style.borderTopColor = StyleKeyword.Null;
                    kv.Button.style.borderLeftColor = StyleKeyword.Null;
                    kv.Button.style.borderRightColor = StyleKeyword.Null;
                    kv.ActionBadge.text = "MOD";
                }
                else
                {
                    kv.Button.style.backgroundColor = StyleKeyword.Null;
                    kv.Button.style.borderTopColor = StyleKeyword.Null;
                    kv.Button.style.borderLeftColor = StyleKeyword.Null;
                    kv.Button.style.borderRightColor = StyleKeyword.Null;
                    kv.ActionBadge.text = string.Empty;
                }

                kv.Button.tooltip = tooltipLines.Count > 0
                    ? string.Join("\n", tooltipLines)
                    : TmbsKeyBinding.FormatKeyCode(kv.Code);
            }
        }

        public void UpdatePressedState(Func<KeyCode, bool> isKeyHeld)
        {
            for (int i = 0; i < _keyVisuals.Count; i++)
            {
                var kv = _keyVisuals[i];
                bool pressed = isKeyHeld != null && isKeyHeld(kv.Code);
                kv.Button.EnableInClassList("tmbs-keycap--pressed", pressed);
            }
        }

        private static bool IsKeyForModifierFlag(KeyCode code, TmbsModifiers mods)
        {
            if (mods == TmbsModifiers.None)
                return false;

            switch (code)
            {
                case KeyCode.LeftControl:
                case KeyCode.RightControl:
                    return (mods & TmbsModifiers.Ctrl) != 0;
                case KeyCode.LeftShift:
                case KeyCode.RightShift:
                    return (mods & TmbsModifiers.Shift) != 0;
                case KeyCode.LeftAlt:
                case KeyCode.RightAlt:
                    return (mods & TmbsModifiers.Alt) != 0;
                case KeyCode.LeftCommand:
                case KeyCode.RightCommand:
                    return (mods & TmbsModifiers.Command) != 0;
                default:
                    return false;
            }
        }
    }
}
