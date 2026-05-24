using System;
using TMBS.Core.Pending;
using TMBS.Core.Pipeline;

namespace TMBS.Core.Validation
{
    [Serializable]
    public sealed class PendingOccupancyValidator : IValidator, IPendingStoreInjectable
    {
        [NonSerialized] private IPendingConstructionStore _store;

        public void InjectStore(IPendingConstructionStore store) => _store = store;

        public ValidationResult Validate(in PipelineContext ctx, ValidationMode mode)
        {
            if (_store == null) return ValidationResult.Valid;

            var opBounds = ValidationUtil.GetOperationBounds(in ctx);
            int len = opBounds.size.x * opBounds.size.y * opBounds.size.z;

            var blocked = CellMask.AllFalse(opBounds);
            var write = CellMask.AllTrue(opBounds);

            bool anyBlocked = false;
            for (int i = 0; i < len; i++)
            {
                var cell = write.CellAt(i);
                if (_store.TryGetByCell(cell, out _, out _))
                {
                    anyBlocked = true;
                    blocked.Bits[i] = true;
                    write.Bits[i] = false;
                }
            }

            if (!anyBlocked) return ValidationResult.Valid;

            var fb = new ValidationFeedback(blocked);

            bool anyFree = false;
            for (int i = 0; i < len; i++)
                if (write.Bits[i]) { anyFree = true; break; }

            if (!anyFree)
                return ValidationResult.InvalidWith(ValidationFailure.Occupied, fb, write);

            return ValidationResult.ValidWith(fb, write);
        }
    }
}