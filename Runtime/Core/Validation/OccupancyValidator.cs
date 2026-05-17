using System;
using TMBS.Core.Metadata;
using TMBS.Core.Pipeline;
using UnityEngine;

namespace TMBS.Core.Validation
{
    [Serializable]
    public sealed class OccupancyValidator : IValidator, IInjectableValidator
    {
        public bool allowOverwrite = false;
        public bool skipOccupiedCells = true;
        public bool markBlockedArea = true;

        [NonSerialized] private IMetadataStore _metadata;

        public void Inject(IMetadataStore metadata)
        {
            _metadata = metadata;
        }

        public ValidationResult Validate(in PipelineContext ctx, ValidationMode mode)
        {
            if (ctx.AlternateBehaviour) return ValidationResult.Valid;
            if (_metadata == null) return ValidationResult.Valid;

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

                if (_metadata.TryGet(cell, out _))
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