using TMBS.Core.Pipeline;
using UnityEngine;
using System;

namespace TMBS.Core.Validation
{
    [Serializable]
    public sealed class BoundsValidator : IValidator
    {
        public BoundsInt allowedBounds = new BoundsInt(-50, -50, 0, 100, 100, 1);
        
        
        
        public bool allowPartialPlacement = false;
        
        
        public bool markBlockedArea = true;

        public bool validateAlternateBehaviour = true;

        public ValidationResult Validate(in PipelineContext ctx, ValidationMode mode)
        {
            if (ctx.AlternateBehaviour && !validateAlternateBehaviour)
                return ValidationResult.Valid;

            var opBounds = ValidationUtil.GetOperationBounds(in ctx);
            
            int w = opBounds.size.x;
            int h = opBounds.size.y;
            int d = opBounds.size.z;

            if (w <= 0 || h <= 0 || d <= 0)
                return ValidationResult.Valid;

            CellMask blocked = markBlockedArea ? CellMask.AllFalse(opBounds) : null;
            CellMask write = CellMask.AllTrue(opBounds);

            bool anyOut = false;
            bool anyIn = false;
            int len = w * h * d;

            for (int i = 0; i < len; i++)
            {
                
                var cell = write.CellAt(i);
                bool inside = allowedBounds.Contains(cell);

                if (!inside)
                {
                    anyOut = true;
                    write.Bits[i] = false;
                    if (blocked != null) blocked.Bits[i] = true;
                }
                else
                {
                    anyIn = true;
                }
            }

            if (!anyOut)
                return ValidationResult.Valid;

            var fb = markBlockedArea ? new ValidationFeedback(blocked) : default;

            
            
            if (allowPartialPlacement && anyIn)
                return ValidationResult.ValidWith(fb, write);

            
            
            return ValidationResult.InvalidWith(ValidationFailure.OutOfBounds, fb, write);
        }
    }
}