using System;
using TMBS.Runtime.Config;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TMBS.Editor.InputConfigurator
{
    public sealed class ModeCardsView : VisualElement
    {
        private sealed class CardEntry
        {
            public TmbsInputMode Mode;
            public VisualElement Card;
            public Label StatusPill;
            public Button FixButton;
        }

        private readonly CardEntry[] _cards = new CardEntry[4];
        private readonly VisualElement _bannerPanel;
        private readonly Label _bannerTitle;
        private readonly Label _bannerBody;
        private readonly Button _bannerActionBtn;

        public event Action<TmbsInputMode> ModeSelected;
        public event Action JumpToControlsRequested;

        public ModeCardsView()
        {
            var header = new Label("1. Choose Input Backend");
            header.AddToClassList("tmbs-section-header");
            Add(header);

            var desc = new Label("Select how TMBS reads player input. You can switch backends at any time without losing bindings.");
            desc.AddToClassList("tmbs-section-desc");
            Add(desc);

            var grid = new VisualElement();
            grid.AddToClassList("tmbs-mode-grid");
            Add(grid);

            _cards[0] = CreateCard(
                TmbsInputMode.Legacy,
                "LEGACY",
                "Legacy Input Manager",
                "Visual key & mouse bindings backed by UnityEngine.Input. Works out of the box with presets.",
                new[] { "Zero Setup", "Mouse + Keys", "Presets" });

            _cards[1] = CreateCard(
                TmbsInputMode.InputActions,
                "INPUT SYSTEM",
                "Input System Package",
                "Uses InputActionReferences and an auto-generatable .inputactions asset for modern Unity projects.",
                new[] { "Action Assets", "Rebindable", "Unity 6" });

            _cards[2] = CreateCard(
                TmbsInputMode.ExternalProvided,
                "EXTERNAL",
                "External / Custom",
                "Inject your own IBuildInputAdapter via SetExternalInputAdapter or IInputAdapterFactory.",
                new[] { "AI / Bots", "Networking", "Custom Tech" });

            _cards[3] = CreateCard(
                TmbsInputMode.None,
                "NONE",
                "None (Disabled)",
                "Disables automatic input polling. Ideal when controlling placement purely through code.",
                new[] { "Scripted", "Read-Only" });

            for (int i = 0; i < _cards.Length; i++)
            {
                grid.Add(_cards[i].Card);
            }

            _bannerPanel = new VisualElement();
            _bannerPanel.AddToClassList("tmbs-card-panel");
            _bannerPanel.style.flexDirection = FlexDirection.Row;
            _bannerPanel.style.alignItems = Align.Center;
            _bannerPanel.style.justifyContent = JustificationBetween();
            Add(_bannerPanel);

            var bannerTextWrap = new VisualElement();
            bannerTextWrap.style.flexShrink = 1;
            bannerTextWrap.style.marginRight = 8;
            _bannerPanel.Add(bannerTextWrap);

            _bannerTitle = new Label();
            _bannerTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            _bannerTitle.style.fontSize = 12;
            bannerTextWrap.Add(_bannerTitle);

            _bannerBody = new Label();
            _bannerBody.style.fontSize = 11;
            _bannerBody.style.whiteSpace = WhiteSpace.Normal;
            _bannerBody.style.opacity = 0.85f;
            bannerTextWrap.Add(_bannerBody);

            _bannerActionBtn = new Button();
            _bannerActionBtn.AddToClassList("tmbs-primary-btn");
            _bannerPanel.Add(_bannerActionBtn);
        }

        private static Justify JustificationBetween() => Justify.SpaceBetween;

        private CardEntry CreateCard(
            TmbsInputMode mode,
            string iconText,
            string titleText,
            string summaryText,
            string[] tags)
        {
            var card = new VisualElement();
            card.AddToClassList("tmbs-mode-card");
            card.RegisterCallback<ClickEvent>(_ => ModeSelected?.Invoke(mode));

            var top = new VisualElement();
            card.Add(top);

            var icon = new Label(iconText);
            icon.AddToClassList("tmbs-mode-card-icon");
            top.Add(icon);

            var title = new Label(titleText);
            title.AddToClassList("tmbs-mode-card-title");
            top.Add(title);

            var summary = new Label(summaryText);
            summary.AddToClassList("tmbs-mode-card-summary");
            top.Add(summary);

            var tagRow = new VisualElement();
            tagRow.AddToClassList("tmbs-tag-row");
            for (int i = 0; i < tags.Length; i++)
            {
                var tag = new Label(tags[i]);
                tag.AddToClassList("tmbs-tag");
                tagRow.Add(tag);
            }
            top.Add(tagRow);

            var footer = new VisualElement();
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.alignItems = Align.Center;
            footer.style.justifyContent = JustificationBetween();
            card.Add(footer);

            var statusPill = new Label();
            statusPill.AddToClassList("tmbs-pill");
            footer.Add(statusPill);

            var fixBtn = new Button(() => SettingsService.OpenProjectSettings("Project/Player"))
            {
                text = "Player Settings"
            };
            fixBtn.style.fontSize = 10;
            fixBtn.style.display = DisplayStyle.None;
            footer.Add(fixBtn);

            return new CardEntry
            {
                Mode = mode,
                Card = card,
                StatusPill = statusPill,
                FixButton = fixBtn
            };
        }

        public void Refresh(TmbsRootConfig config)
        {
            var activeMode = config != null && config.input != null
                ? config.input.mode
                : TmbsInputMode.Legacy;

            for (int i = 0; i < _cards.Length; i++)
            {
                var entry = _cards[i];
                entry.Card.EnableInClassList("tmbs-mode-card--selected", entry.Mode == activeMode);
                UpdateCardAvailability(entry, config);
            }

            UpdateBanner(activeMode, config);
        }

        private static void UpdateCardAvailability(CardEntry entry, TmbsRootConfig config)
        {
            entry.StatusPill.RemoveFromClassList("tmbs-pill--ok");
            entry.StatusPill.RemoveFromClassList("tmbs-pill--warn");
            entry.StatusPill.RemoveFromClassList("tmbs-pill--bad");
            entry.StatusPill.RemoveFromClassList("tmbs-pill--muted");
            entry.FixButton.style.display = DisplayStyle.None;

            switch (entry.Mode)
            {
                case TmbsInputMode.Legacy:
#if ENABLE_LEGACY_INPUT_MANAGER
                    entry.StatusPill.text = "● Available";
                    entry.StatusPill.AddToClassList("tmbs-pill--ok");
#else
                    entry.StatusPill.text = "⚠ Legacy Disabled";
                    entry.StatusPill.AddToClassList("tmbs-pill--warn");
                    entry.FixButton.style.display = DisplayStyle.Flex;
#endif
                    break;

                case TmbsInputMode.InputActions:
#if ENABLE_INPUT_SYSTEM
                    int missing = CountMissingInputSystemRefs(config);
                    if (missing == 0)
                    {
                        entry.StatusPill.text = "● Available (Ready)";
                        entry.StatusPill.AddToClassList("tmbs-pill--ok");
                    }
                    else
                    {
                        entry.StatusPill.text = $"● Available ({missing} unbound)";
                        entry.StatusPill.AddToClassList("tmbs-pill--warn");
                    }
#else
                    entry.StatusPill.text = "⚠ Input System Inactive";
                    entry.StatusPill.AddToClassList("tmbs-pill--warn");
                    entry.FixButton.style.display = DisplayStyle.Flex;
#endif
                    break;

                case TmbsInputMode.ExternalProvided:
                    entry.StatusPill.text = "● Code Required";
                    entry.StatusPill.AddToClassList("tmbs-pill--muted");
                    break;

                case TmbsInputMode.None:
                    entry.StatusPill.text = "● Disabled";
                    entry.StatusPill.AddToClassList("tmbs-pill--muted");
                    break;
            }
        }

        private void UpdateBanner(TmbsInputMode mode, TmbsRootConfig config)
        {
            _bannerActionBtn.clicked -= OnOpenPlayerSettingsClicked;
            _bannerActionBtn.clicked -= OnConfigureControlsClicked;

            switch (mode)
            {
                case TmbsInputMode.Legacy:
#if ENABLE_LEGACY_INPUT_MANAGER
                    var bindings = config?.input?.GetLegacyBindings();
                    int bound = bindings != null ? bindings.CountBoundActions() : 0;
                    _bannerTitle.text = $"Active Mode: Legacy Input Manager ({bound}/7 actions bound)";
                    _bannerBody.text = "Customize key & mouse triggers in the Controls section using presets or by clicking keys on the visual map.";
                    _bannerActionBtn.text = "Edit Legacy Controls ▾";
                    _bannerActionBtn.clicked += OnConfigureControlsClicked;
#else
                    _bannerTitle.text = "Legacy Input Manager is currently disabled in Player Settings";
                    _bannerBody.text = "Set Project Settings ▸ Player ▸ Active Input Handling to 'Input Manager (Old)' or 'Both', or switch to Input System.";
                    _bannerActionBtn.text = "Open Player Settings";
                    _bannerActionBtn.clicked += OnOpenPlayerSettingsClicked;
#endif
                    break;

                case TmbsInputMode.InputActions:
#if ENABLE_INPUT_SYSTEM
                    int missing = CountMissingInputSystemRefs(config);
                    _bannerTitle.text = missing == 0
                        ? "Active Mode: Unity Input System (All 8 actions assigned)"
                        : $"Active Mode: Unity Input System ({missing} action references still unassigned)";
                    _bannerBody.text = missing == 0
                        ? "All InputActionReferences are assigned. You can inspect their visual mapping or regenerate actions below."
                        : "Use the 'Generate & Assign TMBS Input Actions' button in Controls to create a ready-to-use .inputactions asset automatically.";
                    _bannerActionBtn.text = "Configure Input Actions ▾";
                    _bannerActionBtn.clicked += OnConfigureControlsClicked;
#else
                    _bannerTitle.text = "Unity Input System is inactive or not installed";
                    _bannerBody.text = "Ensure 'com.unity.inputsystem' is installed and Active Input Handling includes 'Input System Package (New)'.";
                    _bannerActionBtn.text = "Open Player Settings";
                    _bannerActionBtn.clicked += OnOpenPlayerSettingsClicked;
#endif
                    break;

                case TmbsInputMode.ExternalProvided:
                    _bannerTitle.text = "Active Mode: External / Custom Adapter";
                    _bannerBody.text = "Call SetExternalInputAdapter(adapter) or SetExternalInputProvider(factory) on BuildeableTilemap before OnEnable.";
                    _bannerActionBtn.text = "View Code Snippet ▾";
                    _bannerActionBtn.clicked += OnConfigureControlsClicked;
                    break;

                case TmbsInputMode.None:
                    _bannerTitle.text = "Active Mode: None (Input Disabled)";
                    _bannerBody.text = "BuildeableTilemap will not listen to user pointer/keyboard input. Use public APIs on BuildeableTilemap if needed.";
                    _bannerActionBtn.text = "View Details ▾";
                    _bannerActionBtn.clicked += OnConfigureControlsClicked;
                    break;
            }
        }

        private void OnOpenPlayerSettingsClicked() => SettingsService.OpenProjectSettings("Project/Player");
        private void OnConfigureControlsClicked() => JumpToControlsRequested?.Invoke();

        private static int CountMissingInputSystemRefs(TmbsRootConfig config)
        {
#if ENABLE_INPUT_SYSTEM
            if (config == null || config.input == null)
                return 8;

            var input = config.input;
            int missing = 0;
            if (input.pointAction == null) missing++;
            if (input.dragAction == null) missing++;
            if (input.cancelAction == null) missing++;
            if (input.undoAction == null) missing++;
            if (input.redoAction == null) missing++;
            if (input.alternateModifierAction == null) missing++;
            if (input.rotateAction == null) missing++;
            if (input.mirrorAction == null) missing++;
            return missing;
#else
            return 0;
#endif
        }
    }
}
