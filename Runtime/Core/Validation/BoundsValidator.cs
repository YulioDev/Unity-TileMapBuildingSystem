using TMBS.Core.Pipeline;
using UnityEngine;
using System;

namespace TMBS.Core.Validation
{
    [Serializable]
    public sealed class BoundsValidator : IValidator
    {
        public BoundsInt allowedBounds = new BoundsInt(-50, -50, 0, 100, 100, 1);
        
        // Por defecto NO cambia semántica: sigue siendo “todo o nada”.
        // Si lo activas, permite construir solo en las celdas dentro de bounds usando WriteMask.
        public bool allowPartialPlacement = false;
        
        // Para preview mixto: marcar fuera de bounds como inválido.
        public bool markBlockedArea = true;

        public ValidationResult Validate(in PipelineContext ctx, ValidationMode mode)
        {
            if (ctx.AlternateBehaviour)
                return ValidationResult.Valid;

            var opBounds = ValidationUtil.GetOperationBounds(in ctx);
            // Si no hay volumen, no hay nada que hacer.
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
                // Mantén el mismo orden que tu CellMask (x rápido, luego y, luego z)
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

            // Si se permite parcial y hay al menos una celda válida, devolvemos “válido con máscara”
            // para que el executor use WriteMask y solo escriba dentro de bounds.
            if (allowPartialPlacement && anyIn)
                return ValidationResult.ValidWith(fb, write);

            // Semántica estricta por defecto: invalida la operación completa,
            // PERO mantiene feedback + write mask para preview y/o tooling.
            return ValidationResult.InvalidWith(ValidationFailure.OutOfBounds, fb, write);
        }
    }
}