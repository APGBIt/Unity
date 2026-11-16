using System;
using System.Collections.Generic;

namespace LSRD.Nucleo
{
    /// <summary>
    /// Remuestreo temporal a longitud fija por vecino más cercano.
    /// Copia exacta de ml/lsrd_ml/remuestreo.py (verificada con vectores de oro).
    /// </summary>
    public static class Remuestreo
    {
        public static int[] Indices(int longitud, int t = FormatoLandmarks.LongitudSecuencia)
        {
            if (longitud <= 0) throw new ArgumentException("La secuencia está vacía", nameof(longitud));
            var salida = new int[t];
            for (int i = 0; i < t; i++)
            {
                int k = (int)Math.Floor((i + 0.5) * longitud / t);
                salida[i] = Math.Min(longitud - 1, k);
            }
            return salida;
        }

        /// <summary>Cuadros de longitud variable → tensor plano [t, N, 3] listo para el modelo.</summary>
        public static float[] Aplanar(IReadOnlyList<float[]> cuadros, int t = FormatoLandmarks.LongitudSecuencia)
        {
            int v = FormatoLandmarks.ValoresPorCuadro;
            var indices = Indices(cuadros.Count, t);
            var salida = new float[t * v];
            for (int i = 0; i < t; i++)
            {
                var origen = cuadros[indices[i]];
                if (origen.Length != v) throw new ArgumentException($"Cuadro con {origen.Length} valores; se esperaban {v}");
                Array.Copy(origen, 0, salida, i * v, v);
            }
            return salida;
        }
    }
}
