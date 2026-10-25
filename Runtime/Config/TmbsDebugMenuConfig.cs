using System;
using UnityEngine;

namespace TMBS.Runtime.Config
{
    [Serializable]
    public sealed class TmbsDebugMenuConfig
    {
        [Tooltip("Habilita o deshabilita el menú de debug. Si es falso, no responderá al atajo ni consumirá recursos.")]
        public bool enabled = true;

        [Tooltip("Requiere la tecla Control para el atajo (Ctrl + tecla).")]
        public bool requireControl = true;

        [Tooltip("Tecla de activación del menú (por defecto Slash '/').")]
        public KeyCode toggleKey = KeyCode.Slash;

        [Min(1)]
        [Tooltip("Cantidad de unidades de trabajo añadidas por acción manual.")]
        public int defaultWorkIncrement = 1;

        [Min(1)]
        [Tooltip("Cantidad de recursos entregados por acción manual.")]
        public int defaultResourceIncrement = 1;

        [Range(0.05f, 2.0f)]
        [Tooltip("Intervalo en segundos del auto-avance en modo continuo.")]
        public float autoTickInterval = 0.2f;
    }
}
