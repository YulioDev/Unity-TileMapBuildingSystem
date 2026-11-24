using System;
using System.Collections.Generic;
using TMBS.Core.Metadata;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TMBS.Core.Agents
{
    public sealed class TmbsTilemapWalkability : ITmbsGridWalkabilityProvider
    {
        private IMetadataStore _metadata;
        private Tilemap _targetTilemap;
        private IReadOnlyList<Tilemap> _extraObstacleTilemaps;
        private bool _treatUnrecordedTargetTilesAsBlocking;
        private BoundsInt? _walkableBounds;
        private Func<Vector3Int, bool> _customWalkablePredicate;

        public IMetadataStore Metadata
        {
            get => _metadata;
            set => _metadata = value;
        }

        public Tilemap TargetTilemap
        {
            get => _targetTilemap;
            set => _targetTilemap = value;
        }

        public IReadOnlyList<Tilemap> ExtraObstacleTilemaps
        {
            get => _extraObstacleTilemaps;
            set => _extraObstacleTilemaps = value;
        }

        public bool TreatUnrecordedTargetTilesAsBlocking
        {
            get => _treatUnrecordedTargetTilesAsBlocking;
            set => _treatUnrecordedTargetTilesAsBlocking = value;
        }

        public BoundsInt? WalkableBounds
        {
            get => _walkableBounds;
            set => _walkableBounds = value;
        }

        public Func<Vector3Int, bool> CustomWalkablePredicate
        {
            get => _customWalkablePredicate;
            set => _customWalkablePredicate = value;
        }

        public TmbsTilemapWalkability(
            IMetadataStore metadata,
            Tilemap targetTilemap,
            IReadOnlyList<Tilemap> extraObstacleTilemaps = null,
            bool treatUnrecordedTargetTilesAsBlocking = false,
            BoundsInt? walkableBounds = null,
            Func<Vector3Int, bool> customWalkablePredicate = null)
        {
            _metadata = metadata;
            _targetTilemap = targetTilemap;
            _extraObstacleTilemaps = extraObstacleTilemaps;
            _treatUnrecordedTargetTilesAsBlocking = treatUnrecordedTargetTilesAsBlocking;
            _walkableBounds = walkableBounds;
            _customWalkablePredicate = customWalkablePredicate;
        }

        public void SetSources(IMetadataStore metadata, Tilemap targetTilemap)
        {
            _metadata = metadata;
            _targetTilemap = targetTilemap;
        }

        public bool IsWalkable(Vector3Int cell)
        {
            if (_walkableBounds.HasValue)
            {
                var b = _walkableBounds.Value;
                if (cell.x < b.xMin || cell.x >= b.xMax || cell.y < b.yMin || cell.y >= b.yMax)
                    return false;
            }

            if (_customWalkablePredicate != null && !_customWalkablePredicate(cell))
                return false;

            if (_extraObstacleTilemaps != null)
            {
                for (int i = 0; i < _extraObstacleTilemaps.Count; i++)
                {
                    var obs = _extraObstacleTilemaps[i];
                    if (obs != null && obs.HasTile(cell))
                        return false;
                }
            }

            if (_metadata != null && _metadata.TryGet(cell, out var record))
            {
                if (record.BuildableData != null)
                    return !record.BuildableData.blocksMovement;

                if (record.PlacedTile != null)
                    return !_treatUnrecordedTargetTilesAsBlocking;
            }

            if (_treatUnrecordedTargetTilesAsBlocking && _targetTilemap != null && _targetTilemap.HasTile(cell))
                return false;

            return true;
        }
    }
}
