using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LSRD.App
{
    /// <summary>Botón de "mantener presionado" (para hablar).</summary>
    public sealed class BotonMantener : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public Action AlPresionar;
        public Action AlSoltar;
        bool presionado;

        public void OnPointerDown(PointerEventData e)
        {
            presionado = true;
            AlPresionar?.Invoke();
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (!presionado) return;
            presionado = false;
            AlSoltar?.Invoke();
        }

        void OnDisable()
        {
            if (presionado) { presionado = false; AlSoltar?.Invoke(); }
        }
    }
}
