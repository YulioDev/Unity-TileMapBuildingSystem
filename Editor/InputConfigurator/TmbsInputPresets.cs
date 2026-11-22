using System.Collections.Generic;
using TMBS.Runtime.Config;
using UnityEngine;

namespace TMBS.Editor.InputConfigurator
{
    public enum TmbsInputPresetId
    {
        ClassicPc = 0,
        LeftHanded = 1,
        Mac = 2,
        MouseOnly = 3
    }

    public sealed class TmbsInputPreset
    {
        public readonly TmbsInputPresetId Id;
        public readonly string DisplayName;
        public readonly string Subtitle;

        public TmbsInputPreset(TmbsInputPresetId id, string displayName, string subtitle)
        {
            Id = id;
            DisplayName = displayName;
            Subtitle = subtitle;
        }

        public TmbsLegacyBindings CreateBindings()
        {
            return TmbsInputPresets.CreateBindings(Id);
        }

        public void ApplyTo(TmbsLegacyBindings target)
        {
            if (target == null)
                return;
            target.CopyFrom(CreateBindings());
        }
    }

    public static class TmbsInputPresets
    {
        public static readonly IReadOnlyList<TmbsInputPreset> All = new[]
        {
            new TmbsInputPreset(TmbsInputPresetId.ClassicPc, "Classic PC", "LMB place · RMB/Esc cancel · Shift erase · R/F"),
            new TmbsInputPreset(TmbsInputPresetId.LeftHanded, "Left-handed", "RMB place · LMB/Esc cancel · RShift · P/O"),
            new TmbsInputPreset(TmbsInputPresetId.Mac, "Mac", "LMB place · Cmd+Z / Cmd+Shift+Z · R/F"),
            new TmbsInputPreset(TmbsInputPresetId.MouseOnly, "Mouse only", "LMB/RMB · MMB rotate · Mouse 4/5 Undo/Redo")
        };

        public static TmbsInputPreset Get(TmbsInputPresetId id)
        {
            for (int i = 0; i < All.Count; i++)
            {
                if (All[i].Id == id)
                    return All[i];
            }
            return All[0];
        }

        public static TmbsLegacyBindings CreateBindings(TmbsInputPresetId id)
        {
            switch (id)
            {
                case TmbsInputPresetId.LeftHanded:
                    return new TmbsLegacyBindings
                    {
                        place = new TmbsActionBinding { primary = TmbsKeyBinding.Mouse(1) },
                        cancel = new TmbsActionBinding
                        {
                            primary = TmbsKeyBinding.Mouse(0),
                            secondary = TmbsKeyBinding.Key(KeyCode.Escape)
                        },
                        eraseModifier = new TmbsActionBinding { primary = TmbsKeyBinding.Key(KeyCode.RightShift) },
                        undo = new TmbsActionBinding { primary = TmbsKeyBinding.Key(KeyCode.Backspace, TmbsModifiers.Ctrl) },
                        redo = new TmbsActionBinding { primary = TmbsKeyBinding.Key(KeyCode.Return, TmbsModifiers.Ctrl) },
                        rotate = new TmbsActionBinding { primary = TmbsKeyBinding.Key(KeyCode.P) },
                        mirror = new TmbsActionBinding { primary = TmbsKeyBinding.Key(KeyCode.O) }
                    };

                case TmbsInputPresetId.Mac:
                    return new TmbsLegacyBindings
                    {
                        place = new TmbsActionBinding { primary = TmbsKeyBinding.Mouse(0) },
                        cancel = new TmbsActionBinding
                        {
                            primary = TmbsKeyBinding.Mouse(1),
                            secondary = TmbsKeyBinding.Key(KeyCode.Escape)
                        },
                        eraseModifier = new TmbsActionBinding
                        {
                            primary = TmbsKeyBinding.Key(KeyCode.LeftShift),
                            secondary = TmbsKeyBinding.Key(KeyCode.RightShift)
                        },
                        undo = new TmbsActionBinding { primary = TmbsKeyBinding.Key(KeyCode.Z, TmbsModifiers.Command) },
                        redo = new TmbsActionBinding
                        {
                            primary = TmbsKeyBinding.Key(KeyCode.Z, TmbsModifiers.Command | TmbsModifiers.Shift),
                            secondary = TmbsKeyBinding.Key(KeyCode.Y, TmbsModifiers.Command)
                        },
                        rotate = new TmbsActionBinding { primary = TmbsKeyBinding.Key(KeyCode.R) },
                        mirror = new TmbsActionBinding { primary = TmbsKeyBinding.Key(KeyCode.F) }
                    };

                case TmbsInputPresetId.MouseOnly:
                    return new TmbsLegacyBindings
                    {
                        place = new TmbsActionBinding { primary = TmbsKeyBinding.Mouse(0) },
                        cancel = new TmbsActionBinding { primary = TmbsKeyBinding.Mouse(1) },
                        eraseModifier = default,
                        undo = new TmbsActionBinding { primary = TmbsKeyBinding.Mouse(3) },
                        redo = new TmbsActionBinding { primary = TmbsKeyBinding.Mouse(4) },
                        rotate = new TmbsActionBinding { primary = TmbsKeyBinding.Mouse(2) },
                        mirror = default
                    };

                case TmbsInputPresetId.ClassicPc:
                default:
                    var bindings = TmbsLegacyBindings.CreateDefault();
                    bindings.cancel.secondary = TmbsKeyBinding.Key(KeyCode.Escape);
                    return bindings;
            }
        }
    }
}
