using System;
using System.Collections.Generic;

namespace LSRD.Nucleo
{
    /// <summary>Una clase candidata con su probabilidad.</summary>
    public readonly struct Candidata
    {
        public readonly string Glosa;
        public readonly float Probabilidad;
        public Candidata(string glosa, float probabilidad) { Glosa = glosa; Probabilidad = probabilidad; }
        public override string ToString() => $"{Glosa} ({Probabilidad:P0})";
    }

    /// <summary>Resultado de clasificar una seña: la mejor candidata y las siguientes.</summary>
    public sealed class Prediccion
    {
        public Candidata Mejor => Candidatas.Count > 0 ? Candidatas[0] : new Candidata("", 0f);
        public readonly List<Candidata> Candidatas = new List<Candidata>();
        public double InicioMs;
        public double FinMs;

        public static Prediccion DesdeProbabilidades(float[] probs, IReadOnlyList<string> clases, int k = 3)
        {
            if (probs == null) throw new ArgumentNullException(nameof(probs));
            if (probs.Length != clases.Count)
                throw new ArgumentException($"El modelo devolvió {probs.Length} probabilidades para {clases.Count} clases");
            var indices = new List<int>(probs.Length);
            for (int i = 0; i < probs.Length; i++) indices.Add(i);
            indices.Sort((a, b) =>
            {
                int c = probs[b].CompareTo(probs[a]);
                return c != 0 ? c : a.CompareTo(b);
            });
            var p = new Prediccion();
            for (int i = 0; i < Math.Min(k, indices.Count); i++)
                p.Candidatas.Add(new Candidata(clases[indices[i]], probs[indices[i]]));
            return p;
        }
    }
}
