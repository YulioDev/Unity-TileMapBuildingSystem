using System;

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

    public readonly struct ValidationResult
    {
        public readonly bool IsValid;
        public readonly ValidationFailure Failure;

        public ValidationResult(bool isValid, ValidationFailure failure)
        {
            IsValid = isValid;
            Failure = failure;
        }

        public static ValidationResult Valid => new ValidationResult(true, ValidationFailure.None);
        public static ValidationResult Invalid(ValidationFailure failure) => new ValidationResult(false, failure);
    }
}

