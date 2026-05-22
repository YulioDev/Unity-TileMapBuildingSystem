using System;
using UnityEngine;

namespace TMBS.Core.Intents
{
    public enum BuildIntentType
    {
        PointMove,
        DragStart,
        DragUpdate,
        DragEnd,
        Confirm,
        Cancel,
        Undo,
        Redo
    }

    public readonly struct BuildIntent
    {
        public readonly BuildIntentType Type;
        public readonly Vector3 WorldPoint;
        public readonly bool AlternateBehaviour;

        public BuildIntent(BuildIntentType type, Vector3 worldPoint, bool alternateBehaviour)
        {
            Type = type;
            WorldPoint = worldPoint;
            AlternateBehaviour = alternateBehaviour;
        }
    }
}

