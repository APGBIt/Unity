using System;
using System.Collections.Generic;
using System.Linq;

namespace LSRD.Nucleo
{
    /// <summary>Una seña aceptada dentro de la frase en construcción.</summary>
    public sealed class SenaReconocida
    {
        public string Glosa;
        public float Probabilidad;
        public List<Candidata> Alternativas = new List<Candidata>();
        public double InicioMs;
        public double FinMs;
        public bool Dudosa;
        public bool EsDeletreo;
        public override string ToString() => Dudosa ? Glosa + "?" : Glosa;
    }

    /// <summary>Frase terminada (la persona hizo una pausa): lista para traducir al español.</summary>
    public sealed class Frase
    {
        public readonly List<SenaReconocida> Senas;
        public Frase(List<SenaReconocida> senas) { Senas = senas; }
        public List<string> Glosas => Senas.Select(s => s.EsDeletreo ? "DACT:" + s.Glosa : s.Glosa).ToList();
        public float ConfianzaMedia => Senas.Count == 0 ? 0f : Senas.Average(s => s.Probabilidad);
        public bool TieneDudas => Senas.Any(s => s.Dudosa);
        public double FinMs => Senas.Count == 0 ? 0 : Senas[Senas.Count - 1].FinMs;
    }

    /// <summary>
    /// Acumula las señas reconocidas y decide cuándo termina una frase.
    /// Ignora el fondo y las predicciones muy inseguras; marca como dudosas las intermedias
    /// para que la persona sorda elija entre las alternativas.
    /// </summary>
    public sealed class ConstructorFrase
    {
        public const string ClaseFondo = "_FONDO";

        public float UmbralMinimo = 0.35f;
        public float UmbralConfianza = 0.6f;
        public double PausaFraseMs = 1500;
        public double FusionRepetidaMs = 500;
        public int MaxSenas = 30;

        readonly List<SenaReconocida> actual = new List<SenaReconocida>();
        public IReadOnlyList<SenaReconocida> Actual => actual;

        /// <summary>Devuelve la seña aceptada (o la fusionada con la anterior), o null si se descartó.</summary>
        public SenaReconocida Agregar(Prediccion p)
        {
            var mejor = p.Mejor;
            if (string.IsNullOrEmpty(mejor.Glosa) || mejor.Glosa == ClaseFondo || mejor.Probabilidad < UmbralMinimo)
                return null;

            if (actual.Count > 0)
            {
                var ultima = actual[actual.Count - 1];
                if (!ultima.EsDeletreo && ultima.Glosa == mejor.Glosa && p.InicioMs - ultima.FinMs < FusionRepetidaMs)
                {
                    ultima.FinMs = Math.Max(ultima.FinMs, p.FinMs);
                    if (mejor.Probabilidad > ultima.Probabilidad)
                    {
                        ultima.Probabilidad = mejor.Probabilidad;
                        ultima.Dudosa = mejor.Probabilidad < UmbralConfianza;
                    }
                    return ultima;
                }
            }

            var s = new SenaReconocida
            {
                Glosa = mejor.Glosa,
                Probabilidad = mejor.Probabilidad,
                InicioMs = p.InicioMs,
                FinMs = p.FinMs,
                Dudosa = mejor.Probabilidad < UmbralConfianza,
            };
            s.Alternativas.AddRange(p.Candidatas.Where(c => c.Glosa != ClaseFondo));
            actual.Add(s);
            if (actual.Count > MaxSenas) actual.RemoveAt(0);
            return s;
        }

        public SenaReconocida AgregarDeletreo(string palabra, double inicioMs, double finMs)
        {
            if (string.IsNullOrEmpty(palabra)) return null;
            var s = new SenaReconocida
            {
                Glosa = palabra, Probabilidad = 1f, InicioMs = inicioMs, FinMs = finMs, EsDeletreo = true,
            };
            actual.Add(s);
            return s;
        }

        /// <summary>Llamar en cada cuadro: devuelve la frase cuando hubo una pausa suficiente.</summary>
        public Frase Actualizar(double ahoraMs, bool senaEnCurso)
        {
            if (actual.Count == 0 || senaEnCurso) return null;
            if (ahoraMs - actual[actual.Count - 1].FinMs < PausaFraseMs) return null;
            return Forzar();
        }

        public Frase Forzar()
        {
            if (actual.Count == 0) return null;
            var f = new Frase(new List<SenaReconocida>(actual));
            actual.Clear();
            return f;
        }

        /// <summary>La persona eligió otra alternativa para la seña <paramref name="indice"/>.</summary>
        public void Corregir(int indice, string glosa)
        {
            if (indice < 0 || indice >= actual.Count) return;
            var s = actual[indice];
            var alt = s.Alternativas.FirstOrDefault(a => a.Glosa == glosa);
            s.Glosa = glosa;
            s.Probabilidad = alt.Glosa == glosa ? Math.Max(alt.Probabilidad, UmbralConfianza) : 1f;
            s.Dudosa = false;
        }

        public void Deshacer()
        {
            if (actual.Count > 0) actual.RemoveAt(actual.Count - 1);
        }

        public void Limpiar() => actual.Clear();
    }
}
