using System;
using TMBS.Core.Validation;
using UnityEngine;

namespace TMBS.Runtime.Config
{
    [Serializable]
    public class ValidatorEntry
    {
        public bool enabled = true;

        [SerializeReference]
        public IValidator validator;
    }
}
