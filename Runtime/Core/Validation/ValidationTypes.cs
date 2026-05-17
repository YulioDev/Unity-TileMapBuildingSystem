using System;
using UnityEngine;

namespace TMBS.Core.Validation
{
    public enum ValidationMode
    {
        Quick,
        Full
    }

    public enum ValidationFailure
    {
        None,
        MissingSelection,
        OutOfBounds,
        Occupied
    }

    public readonly struct ValidationFeedback
    {
        /// <summary>
        /// Máscara de celdas bloqueadas para preview. True = bloqueado.
        /// Null = no hay bloqueos que pintar.
        /// </summary>
        public readonly CellMask BlockedMask;

        public ValidationFeedback(CellMask blockedMask)
        {
            BlockedMask = blockedMask;
        }

        public bool HasBlockedCells => BlockedMask != null && BlockedMask.AnyTrue();
    }

    public readonly struct ValidationResult
    {
        public readonly bool IsValid;
        public readonly ValidationFailure Failure;

        /// <summary>
        /// Feedback para preview (bloqueos).
        /// </summary>
        public readonly ValidationFeedback Feedback;

        /// <summary>
        /// Máscara de escritura. True = escribible. Null = todo escribible.
        /// Este campo es el "plan de ejecución" acumulable entre validadores.
        /// </summary>
        public readonly CellMask WriteMask;

        public ValidationResult(bool isValid, ValidationFailure failure, ValidationFeedback feedback, CellMask writeMask)
        {
            IsValid = isValid;
            Failure = failure;
            Feedback = feedback;
            WriteMask = writeMask;
        }

        public static ValidationResult Valid => new ValidationResult(true, ValidationFailure.None, default, null);

        public static ValidationResult Invalid(ValidationFailure failure) =>
            new ValidationResult(false, failure, default, null);

        public static ValidationResult Invalid(ValidationFailure failure, ValidationFeedback feedback) =>
            new ValidationResult(false, failure, feedback, null);

        public static ValidationResult ValidWith(ValidationFeedback feedback, CellMask writeMask = null) =>
            new ValidationResult(true, ValidationFailure.None, feedback, writeMask);

        public static ValidationResult InvalidWith(ValidationFailure failure, ValidationFeedback feedback, CellMask writeMask = null) =>
            new ValidationResult(false, failure, feedback, writeMask);
    }

    public static class ValidationUtil
    {
        public static BoundsInt GetOperationBounds(in TMBS.Core.Pipeline.PipelineContext ctx)
        {
            return ctx.HasDragBounds
                ? ctx.DragBounds
                : new BoundsInt(ctx.Cell, Vector3Int.one);
        }
    }
}