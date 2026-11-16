using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using LSRD.Nucleo;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.UI;

namespace LSRD.App
{
    /// <summary>Reproduce una grabación sobre un <see cref="GraficoEsqueleto"/> en modo avatar.</summary>
    public sealed class AvatarEsquematico : MonoBehaviour
    {
        public GraficoEsqueleto Grafico;
        public float Velocidad = 1f;
        public bool Reproduciendo => actual != null;

        MuestraLandmarks actual;
        double inicio;
        int indice;
        Action alTerminar;
        float[] reposo;

        static double Ahora() => Time.realtimeSinceStartupAsDouble * 1000.0;

        public void Reproducir(MuestraLandmarks m, Action fin)
        {
            if (m == null || m.NumeroCuadros == 0) { fin?.Invoke(); return; }
            actual = m;
            indice = 0;
            inicio = Ahora();
            alTerminar = fin;
            Grafico.ModoAvatar = true;
            Grafico.FijarEncuadre(m);
            Grafico.Mostrar(m.Cuadros[0], 1.0);
        }

        public void MostrarReposo(MuestraLandmarks referencia)
        {
            actual = null;
            if (referencia == null || referencia.NumeroCuadros == 0) { Grafico.Limpiar(); return; }
            reposo = (float[])referencia.Cuadros[0].Clone();
            for (int i = FormatoLandmarks.InicioManoIzq * 3; i < FormatoLandmarks.InicioCara * 3; i++) reposo[i] = 0f;
            Grafico.ModoAvatar = true;
            Grafico.FijarEncuadre(referencia);
            Grafico.Mostrar(reposo, 1.0);
        }

        public void Detener()
        {
            actual = null;
            alTerminar = null;
        }

        void Update()
        {
            if (actual == null) return;
            double rel = (Ahora() - inicio) * Mathf.Max(0.25f, Velocidad);
            int n = actual.NumeroCuadros;
            while (indice + 1 < n && actual.TiemposMs[indice + 1] <= rel) indice++;
            Grafico.Mostrar(actual.Cuadros[indice], 1.0);
            if (rel > actual.TiemposMs[n - 1] + 120)
            {
                var fin = alTerminar;
                actual = null;
                alTerminar = null;
                fin?.Invoke();
            }
        }
    }
}
