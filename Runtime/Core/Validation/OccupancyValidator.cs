using System;
using TMBS.Core.Metadata;
using TMBS.Core.Pipeline;

namespace TMBS.Core.Validation
{
    [Serializable]
    public sealed class OccupancyValidator : IValidator, IInjectableValidator
    {
        [NonSerialized] private IMetadataStore _metadata;

        public void Inject(IMetadataStore metadata)
        {
            _metadata = metadata;
        }

        public ValidationResult Validate(in PipelineContext ctx, ValidationMode mode)
        {
            if (ctx.AlternateBehaviour)
                return ValidationResult.Valid;

            if (_metadata == null)
                return ValidationResult.Valid;

            if (ctx.HasDragBounds)
            {
                var rect = ctx.DragBounds;
                var origin = rect.position;

                for (int z = 0; z < rect.size.z; z++)
                {
                    for (int y = 0; y < rect.size.y; y++)
                    {
                        for (int x = 0; x < rect.size.x; x++)
                        {
                            var point = new UnityEngine.Vector3Int(origin.x + x, origin.y + y, origin.z + z);
                            if (_metadata.TryGet(point, out _))
                                return ValidationResult.Invalid(ValidationFailure.Occupied);
                        }
                    }
                }
                return ValidationResult.Valid;
            }

            if (_metadata.TryGet(ctx.Cell, out _))
                return ValidationResult.Invalid(ValidationFailure.Occupied);

            return ValidationResult.Valid;
        }
    }
}

