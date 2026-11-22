using System.Collections.Generic;
using TMBS.Core.Intents;
using TMBS.Runtime.Config;
using UnityEngine;

namespace TMBS.Editor.InputConfigurator
{
    public sealed class TmbsActionMeta
    {
        public readonly TmbsInputActionId Id;
        public readonly string DisplayName;
        public readonly string BadgeText;
        public readonly string Description;
        public readonly Color AccentColor;
        public readonly bool IsEssential;

        public TmbsActionMeta(
            TmbsInputActionId id,
            string displayName,
            string badgeText,
            string description,
            Color accentColor,
            bool isEssential = false)
        {
            Id = id;
            DisplayName = displayName;
            BadgeText = badgeText;
            Description = description;
            AccentColor = accentColor;
            IsEssential = isEssential;
        }
    }

    public static class TmbsActionCatalog
    {
        private static readonly Dictionary<TmbsInputActionId, TmbsActionMeta> Metas =
            new Dictionary<TmbsInputActionId, TmbsActionMeta>
            {
                {
                    TmbsInputActionId.Place,
                    new TmbsActionMeta(
                        TmbsInputActionId.Place,
                        "Place / Drag",
                        "PLACE",
                        "Press to start placing or dragging a rectangle; release to confirm.",
                        new Color(0.28f, 0.52f, 0.75f),
                        isEssential: true)
                },
                {
                    TmbsInputActionId.Cancel,
                    new TmbsActionMeta(
                        TmbsInputActionId.Cancel,
                        "Cancel",
                        "CANCEL",
                        "Cancels an active drag operation or current placement.",
                        new Color(0.75f, 0.36f, 0.33f),
                        isEssential: true)
                },
                {
                    TmbsInputActionId.EraseModifier,
                    new TmbsActionMeta(
                        TmbsInputActionId.EraseModifier,
                        "Erase (Hold)",
                        "ERASE",
                        "Hold while clicking or dragging to erase tiles instead of placing.",
                        new Color(0.78f, 0.56f, 0.24f))
                },
                {
                    TmbsInputActionId.Undo,
                    new TmbsActionMeta(
                        TmbsInputActionId.Undo,
                        "Undo",
                        "UNDO",
                        "Reverts the last building or erase operation.",
                        new Color(0.33f, 0.58f, 0.53f))
                },
                {
                    TmbsInputActionId.Redo,
                    new TmbsActionMeta(
                        TmbsInputActionId.Redo,
                        "Redo",
                        "REDO",
                        "Re-applies the last undone operation.",
                        new Color(0.47f, 0.45f, 0.72f))
                },
                {
                    TmbsInputActionId.Rotate,
                    new TmbsActionMeta(
                        TmbsInputActionId.Rotate,
                        "Rotate (90°)",
                        "ROTATE",
                        "Rotates the active tile or blueprint clockwise by 90 degrees.",
                        new Color(0.40f, 0.62f, 0.34f))
                },
                {
                    TmbsInputActionId.Mirror,
                    new TmbsActionMeta(
                        TmbsInputActionId.Mirror,
                        "Mirror (Flip)",
                        "MIRROR",
                        "Mirrors the active tile or blueprint horizontally.",
                        new Color(0.64f, 0.44f, 0.56f))
                }
            };

        public static TmbsActionMeta Get(TmbsInputActionId id)
        {
            return Metas.TryGetValue(id, out var meta) ? meta : Metas[TmbsInputActionId.Place];
        }

        public static Color GetIntentColor(BuildIntentType type)
        {
            switch (type)
            {
                case BuildIntentType.PointMove: return new Color(0.55f, 0.62f, 0.70f);
                case BuildIntentType.DragStart:
                case BuildIntentType.DragUpdate:
                case BuildIntentType.DragEnd:
                case BuildIntentType.Confirm: return Get(TmbsInputActionId.Place).AccentColor;
                case BuildIntentType.Cancel: return Get(TmbsInputActionId.Cancel).AccentColor;
                case BuildIntentType.Undo: return Get(TmbsInputActionId.Undo).AccentColor;
                case BuildIntentType.Redo: return Get(TmbsInputActionId.Redo).AccentColor;
                case BuildIntentType.Rotate: return Get(TmbsInputActionId.Rotate).AccentColor;
                case BuildIntentType.Mirror: return Get(TmbsInputActionId.Mirror).AccentColor;
                default: return Color.gray;
            }
        }
    }
}
