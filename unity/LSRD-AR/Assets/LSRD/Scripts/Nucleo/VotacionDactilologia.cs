using System;
using System.Collections.Generic;
using System.Text;

namespace LSRD.Nucleo
{
    /// <summary>
    /// Convierte las predicciones cuadro a cuadro del modelo de dactilología en letras estables
    /// y palabras. Una letra se confirma cuando domina la ventana con buena probabilidad; para
    /// repetir la misma letra (p. ej. "LL") hay que soltarla un instante.
    /// </summary>
    public sealed class VotacionDactilologia
    {
        public int Ventana = 8;
        public int Minimo = 6;
        public float ProbMinima = 0.6f;
        public int CuadrosReinicio = 3;
        public double FinPalabraMs = 700;
        public Func<string, string> TextoDeGlosa = GlosaALetra;

        readonly Queue<KeyValuePair<string, float>> ventana = new Queue<KeyValuePair<string, float>>();
        readonly StringBuilder palabra = new StringBuilder();
        string ultimaConfirmada;
        bool listaParaRepetir = true;
        int cuadrosSinLetra;
        double tUltimaMano = double.NaN, tInicioPalabra = double.NaN;

        public string PalabraEnCurso => palabra.ToString();
        public double InicioPalabraMs => tInicioPalabra;

        public static string GlosaALetra(string glosa)
        {
            if (string.IsNullOrEmpty(glosa)) return "";
            int i = glosa.LastIndexOf('-');
            return (i >= 0 ? glosa.Substring(i + 1) : glosa).ToLowerInvariant();
        }

        /// <summary>
        /// Procesa un cuadro. <paramref name="glosa"/> = null si no hay mano.
        /// Devuelve la letra recién confirmada (o null). <paramref name="palabraTerminada"/> trae la
        /// palabra cuando la mano desapareció durante <see cref="FinPalabraMs"/>.
        /// </summary>
        public string Procesar(double tMs, string glosa, float prob, out string palabraTerminada)
        {
            palabraTerminada = null;
            if (glosa == null)
            {
                ventana.Clear();
                listaParaRepetir = true;
                if (palabra.Length > 0 && !double.IsNaN(tUltimaMano) && tMs - tUltimaMano >= FinPalabraMs)
                {
                    palabraTerminada = palabra.ToString();
                    palabra.Clear();
                    ultimaConfirmada = null;
                    tInicioPalabra = double.NaN;
                }
                return null;
            }

            tUltimaMano = tMs;
            if (prob < ProbMinima)
            {
                cuadrosSinLetra++;
                if (cuadrosSinLetra >= CuadrosReinicio) listaParaRepetir = true;
            }
            else
            {
                cuadrosSinLetra = 0;
            }

            ventana.Enqueue(new KeyValuePair<string, float>(glosa, prob));
            while (ventana.Count > Ventana) ventana.Dequeue();
            if (ventana.Count < Minimo) return null;

            var conteo = new Dictionary<string, int>();
            var suma = new Dictionary<string, float>();
            foreach (var kv in ventana)
            {
                if (kv.Value < ProbMinima) continue;
                conteo.TryGetValue(kv.Key, out int n);
                conteo[kv.Key] = n + 1;
                suma.TryGetValue(kv.Key, out float s);
                suma[kv.Key] = s + kv.Value;
            }
            string ganadora = null;
            int maximo = 0;
            foreach (var kv in conteo)
                if (kv.Value > maximo) { maximo = kv.Value; ganadora = kv.Key; }
            if (ganadora == null || maximo < Minimo) return null;

            if (ganadora == ultimaConfirmada && !listaParaRepetir) return null;

            ultimaConfirmada = ganadora;
            listaParaRepetir = false;
            ventana.Clear();
            if (palabra.Length == 0) tInicioPalabra = tMs;
            var letra = TextoDeGlosa(ganadora);
            palabra.Append(letra);
            return letra;
        }

        public string Cerrar()
        {
            var p = palabra.ToString();
            palabra.Clear();
            ventana.Clear();
            ultimaConfirmada = null;
            listaParaRepetir = true;
            tInicioPalabra = double.NaN;
            return p.Length > 0 ? p : null;
        }

        public void Borrar()
        {
            if (palabra.Length > 0) palabra.Length--;
        }
    }
}
