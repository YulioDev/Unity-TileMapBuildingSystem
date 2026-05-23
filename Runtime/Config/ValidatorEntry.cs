using System;
using TMBS.Core.Validation;
using UnityEngine;

namespace TMBS.Runtime.Config
{
    [Serializable]
    public class ValidatorEntry
    {
        [Tooltip("If disabled, this validator will be skipped during the build process.")]
        public bool enabled = true;

        [Tooltip("The validator instance and its specific configuration.")]
        [SerializeReference]
        public IValidator validator;
    }
}
