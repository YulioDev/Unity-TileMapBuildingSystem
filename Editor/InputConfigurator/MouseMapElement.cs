using System;
using System.Collections.Generic;
using TMBS.Runtime.Config;
using UnityEngine;
using UnityEngine.UIElements;

namespace TMBS.Editor.InputConfigurator
{
    public sealed class MouseMapElement : VisualElement
    {
        private sealed class MouseButtonVisual
        {
            public int ButtonIndex;
            public string DefaultLabel;
            public Button Button;
            public Label TitleLabel;
            public Label BadgeLabel;
        }

        private readonly List<MouseButtonVisual> _buttons = new List<MouseButtonVisual>(5);
        private readonly VisualElement _shell;
        private readonly Label _hintLabel;
        private bool _isReadOnly;

        public event Action<int> MouseButtonClicked;

        public MouseMapElement(bool compact = false)
        {
            AddToClassList("tmbs-mouse-box");

            var title = new Label("Mouse");
            title.style.fontSize = 11;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 4;
            Add(title);

            _shell = new VisualElement();
            _shell.AddToClassList("tmbs-mouse-shell");
            if (compact)
            {
                _shell.style.width = 96;
                _shell.style.height = 132;
            }
            _shell.generateVisualContent += DrawMouseSilhouette;
            Add(_shell);

            // Top row: LMB, MMB (Wheel), RMB
            var topRow = new VisualElement();
            topRow.AddToClassList("tmbs-mouse-top-row");
            if (compact) topRow.style.height = 52;
            _shell.Add(topRow);

            var lmb = CreateMouseButton(0, "LMB", compact);
            lmb.style.flexGrow = 1.4f;
            lmb.style.flexBasis = 0;
            lmb.style.marginRight = 3;
            lmb.style.borderTopLeftRadius = 18;
            topRow.Add(lmb);

            var mmb = CreateMouseButton(2, "MMB", compact);
            mmb.style.flexGrow = 0.85f;
            mmb.style.flexBasis = 0;
            mmb.style.marginRight = 3;
            mmb.style.borderTopLeftRadius = 8;
            mmb.style.borderTopRightRadius = 8;
            mmb.style.borderBottomLeftRadius = 8;
            mmb.style.borderBottomRightRadius = 8;
            topRow.Add(mmb);

            var rmb = CreateMouseButton(1, "RMB", compact);
            rmb.style.flexGrow = 1.4f;
            rmb.style.flexBasis = 0;
            rmb.style.borderTopRightRadius = 18;
            topRow.Add(rmb);

            // Side buttons row: Mouse 4 (Back) & Mouse 5 (Forward)
            var sideRow = new VisualElement();
            sideRow.AddToClassList("tmbs-mouse-side-row");
            if (compact) sideRow.style.height = 24;
            _shell.Add(sideRow);

            var m4 = CreateMouseButton(3, "M4", compact);
            m4.style.flexGrow = 1;
            m4.style.flexBasis = 0;
            m4.style.marginRight = 3;
            m4.tooltip = "Mouse Button 4 (Back)";
            sideRow.Add(m4);

            var m5 = CreateMouseButton(4, "M5", compact);
            m5.style.flexGrow = 1;
            m5.style.flexBasis = 0;
            m5.tooltip = "Mouse Button 5 (Forward)";
            sideRow.Add(m5);

            // Palm rest area
            var palm = new VisualElement();
            palm.style.flexGrow = 1;
            palm.style.justifyContent = JustificationCenter();
            palm.style.alignItems = Align.Center;
            var palmText = new Label("Body");
            palmText.style.fontSize = 9;
            palmText.style.opacity = 0.45f;
            palm.Add(palmText);
            _shell.Add(palm);

            _hintLabel = new Label("LMB / RMB / MMB / M4 / M5");
            _hintLabel.style.fontSize = 9;
            _hintLabel.style.opacity = 0.65f;
            _hintLabel.style.marginTop = 2;
            if (compact)
                _hintLabel.style.display = DisplayStyle.None;
            Add(_hintLabel);
        }

        private static Justify JustificationCenter() => Justify.Center;

        private Button CreateMouseButton(int buttonIndex, string labelText, bool compact)
        {
            var btn = new Button(() =>
            {
                if (!_isReadOnly)
                    MouseButtonClicked?.Invoke(buttonIndex);
            });
            btn.AddToClassList("tmbs-mouse-btn");

            var titleLabel = new Label(labelText);
            titleLabel.AddToClassList("tmbs-mouse-btn-label");
            if (compact) titleLabel.style.fontSize = 8;
            btn.Add(titleLabel);

            var badgeLabel = new Label(string.Empty);
            badgeLabel.AddToClassList("tmbs-mouse-btn-action");
            if (compact) badgeLabel.style.display = DisplayStyle.None;
            btn.Add(badgeLabel);

            _buttons.Add(new MouseButtonVisual
            {
                ButtonIndex = buttonIndex,
                DefaultLabel = labelText,
                Button = btn,
                TitleLabel = titleLabel,
                BadgeLabel = badgeLabel
            });

            return btn;
        }

        private static void DrawMouseSilhouette(MeshGenerationContext mgc)
        {
            var rect = mgc.visualElement.contentRect;
            if (rect.width <= 4f || rect.height <= 4f)
                return;

            var painter = mgc.painter2D;
            float r = Mathf.Min(rect.width * 0.35f, 36f);
            float x0 = 1f;
            float y0 = 1f;
            float x1 = rect.width - 1f;
            float y1 = rect.height - 1f;

            painter.lineWidth = 1.6f;
            painter.strokeColor = new Color(0.42f, 0.42f, 0.42f, 0.55f);
            painter.fillColor = new Color(0.20f, 0.20f, 0.20f, 0.30f);

            painter.BeginPath();
            painter.MoveTo(new Vector2(x0 + r, y0));
            painter.LineTo(new Vector2(x1 - r, y0));
            painter.QuadraticCurveTo(new Vector2(x1, y0), new Vector2(x1, y0 + r));
            painter.LineTo(new Vector2(x1, y1 - r));
            painter.QuadraticCurveTo(new Vector2(x1, y1), new Vector2(x1 - r, y1));
            painter.LineTo(new Vector2(x0 + r, y1));
            painter.QuadraticCurveTo(new Vector2(x0, y1), new Vector2(x0, y1 - r));
            painter.LineTo(new Vector2(x0, y0 + r));
            painter.QuadraticCurveTo(new Vector2(x0, y0), new Vector2(x0 + r, y0));
            painter.ClosePath();
            painter.Fill();
            painter.Stroke();
        }

        public void SetReadOnly(bool readOnly)
        {
            _isReadOnly = readOnly;
        }

        public void UpdateBindings(TmbsLegacyBindings bindings)
        {
            var matchedActions = new List<TmbsInputActionId>(4);
            var tooltipLines = new List<string>(4);

            for (int i = 0; i < _buttons.Count; i++)
            {
                var mv = _buttons[i];
                matchedActions.Clear();
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
                            if (!kb.IsBound || kb.source != TmbsBindingSource.MouseButton)
                                continue;

                            if (kb.mouseButton == mv.ButtonIndex)
                            {
                                if (!matchedActions.Contains(actionId))
                                    matchedActions.Add(actionId);
                                tooltipLines.Add($"{meta.DisplayName}: {kb.ToDisplayString()}");
                            }
                        }
                    }
                }

                if (matchedActions.Count > 0)
                {
                    var color = TmbsActionCatalog.Get(matchedActions[0]).AccentColor;
                    mv.Button.style.backgroundColor = new Color(color.r, color.g, color.b, 0.18f);
                    mv.Button.style.borderTopColor = color;
                    mv.Button.style.borderBottomColor = StyleKeyword.Null;
                    mv.Button.style.borderLeftColor = StyleKeyword.Null;
                    mv.Button.style.borderRightColor = StyleKeyword.Null;
                    mv.BadgeLabel.text = matchedActions.Count == 1
                        ? TmbsActionCatalog.Get(matchedActions[0]).BadgeText
                        : $"{matchedActions.Count}×";
                    mv.BadgeLabel.style.color = color;
                }
                else
                {
                    mv.Button.style.backgroundColor = StyleKeyword.Null;
                    mv.Button.style.borderTopColor = StyleKeyword.Null;
                    mv.Button.style.borderBottomColor = StyleKeyword.Null;
                    mv.Button.style.borderLeftColor = StyleKeyword.Null;
                    mv.Button.style.borderRightColor = StyleKeyword.Null;
                    mv.BadgeLabel.text = string.Empty;
                    mv.BadgeLabel.style.color = StyleKeyword.Null;
                }

                mv.Button.tooltip = tooltipLines.Count > 0
                    ? string.Join("\n", tooltipLines)
                    : TmbsKeyBinding.FormatMouseButton(mv.ButtonIndex);
            }
        }

        public void UpdatePressedState(Func<int, bool> isMouseHeld)
        {
            for (int i = 0; i < _buttons.Count; i++)
            {
                var mv = _buttons[i];
                bool pressed = isMouseHeld != null && isMouseHeld(mv.ButtonIndex);
                mv.Button.EnableInClassList("tmbs-mouse-btn--pressed", pressed);
            }
        }
    }
}
