using System;
using TMBS.Core.Metadata;
using TMBS.Core.Pipeline;
using UnityEngine;

namespace TMBS.Core.Validation
{
    public enum OccupancySourceMode
    {
        MetadataOnly,
        TilemapOnly,
        MetadataOrTilemap
    }

    [Serializable]
    public sealed class OccupancyValidator : IValidator, IInjectableValidator, IOccupancySourceInjectable
    {
        public bool allowOverwrite = false;
        public bool skipOccupiedCells = true;
        public bool markBlockedArea = true;
        public bool validateAlternateBehaviour = false;
        public OccupancySourceMode occupancySourceMode = OccupancySourceMode.MetadataOnly;

        [NonSerialized] private IMetadataStore _metadata;
        [NonSerialized] private ICellOccupancySource _occupancySource;

        public void Inject(IMetadataStore metadata)
        {
            _metadata = metadata;
        }

        public void InjectOccupancySource(ICellOccupancySource occupancySource)
        {
            _occupancySource = occupancySource;
        }

        private bool IsOccupied(Vector3Int cell)
        {
            bool metadataOccupied = _metadata != null && _metadata.TryGet(cell, out _);
            bool tilemapOccupied = _occupancySource != null && _occupancySource.HasTile(cell);

            switch (occupancySourceMode)
            {
                case OccupancySourceMode.MetadataOnly:
                    return metadataOccupied;
                case OccupancySourceMode.TilemapOnly:
                    return tilemapOccupied;
                case OccupancySourceMode.MetadataOrTilemap:
                    return metadataOccupied || tilemapOccupied;
                default:
                    return metadataOccupied;
            }
        }

        public ValidationResult Validate(in PipelineContext ctx, ValidationMode mode)
        {
            if (ctx.AlternateBehaviour && !validateAlternateBehaviour) return ValidationResult.Valid;

            var opBounds = ValidationUtil.GetOperationBounds(in ctx);

            if (allowOverwrite)
                return ValidationResult.Valid;

            CellMask blocked = null; 
            CellMask write = null;   

            bool anyOccupied = false;

            if (skipOccupiedCells)
                write = CellMask.AllTrue(opBounds);

            if (markBlockedArea)
                blocked = CellMask.AllFalse(opBounds);

            for (int i = 0; i < opBounds.size.x * opBounds.size.y * opBounds.size.z; i++)
            {
                var cell = blocked != null ? blocked.CellAt(i)
                         : write != null ? write.CellAt(i)
                         : 
                           new Vector3Int(
                               opBounds.position.x + (i % opBounds.size.x),
                               opBounds.position.y + ((i / opBounds.size.x) % opBounds.size.y),
                               opBounds.position.z + (i / (opBounds.size.x * opBounds.size.y))
                           );

                if (IsOccupied(cell))
                {
                    anyOccupied = true;

                    if (blocked != null) blocked.Bits[i] = true;     
                    if (write != null) write.Bits[i] = false;        

                    if (!skipOccupiedCells)
                    {
                        var fb = markBlockedArea ? new ValidationFeedback(blocked) : default;
                        return ValidationResult.InvalidWith(ValidationFailure.Occupied, fb);
                    }
                }
            }

            if (skipOccupiedCells && anyOccupied)
            {
                var fb = markBlockedArea ? new ValidationFeedback(blocked) : default;
                return ValidationResult.ValidWith(fb, write);
            }

            return ValidationResult.Valid;
        }
    }
}