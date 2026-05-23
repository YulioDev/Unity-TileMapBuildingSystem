using System.Collections.Generic;
using TMBS.Core.Pipeline;

namespace TMBS.Core.Validation
{
    public sealed class ValidatorPipeline
    {
        private readonly List<IValidator> _validators;

        public ValidatorPipeline(List<IValidator> validators)
        {
            _validators = validators != null
                ? new List<IValidator>(validators)
                : new List<IValidator>();
        }

        public int InternalValidatorsCount => _validators.Count;

        public bool TryGetBounds(int index, out UnityEngine.BoundsInt bounds)
        {
            bounds = default;

            if (index < 0 || index >= _validators.Count)
                return false;

            if (_validators[index] is BoundsValidator b)
            {
                bounds = b.allowedBounds;
                return true;
            }

            return false;
        }

        public ValidationResult Validate(in PipelineContext ctx, ValidationMode mode)
        {
            CellMask combinedBlocked = null;
            CellMask combinedWrite = null;
            bool isValid = true;
            ValidationFailure firstFailure = ValidationFailure.None;

            for (int i = 0; i < _validators.Count; i++)
            {
                var result = _validators[i].Validate(in ctx, mode);

                if (result.Feedback.BlockedMask != null && result.Feedback.BlockedMask.AnyTrue())
                {
                    if (combinedBlocked == null) combinedBlocked = result.Feedback.BlockedMask;
                    else combinedBlocked.OrInPlace(result.Feedback.BlockedMask);
                }

                if (result.WriteMask != null)
                {
                    if (combinedWrite == null) combinedWrite = result.WriteMask;
                    else combinedWrite.AndInPlace(result.WriteMask);
                }

                if (!result.IsValid)
                {
                    isValid = false;
                    if (firstFailure == ValidationFailure.None)
                        firstFailure = result.Failure;
                    
                    if (mode == ValidationMode.Full)
                        break;
                }
            }

            var feedback = new ValidationFeedback(combinedBlocked);
            
            if (!isValid)
                return ValidationResult.InvalidWith(firstFailure, feedback, combinedWrite);
            
            return ValidationResult.ValidWith(feedback, combinedWrite);
        }
    }
}