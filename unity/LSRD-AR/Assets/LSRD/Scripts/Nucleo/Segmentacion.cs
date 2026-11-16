using System;

namespace LSRD.Nucleo
{
    /// <summary>Parámetros del segmentador (mismos valores por defecto que en Python).</summary>
    [Serializable]
    public sealed class ParametrosSegmentacion
    {
        public double InicioMs = 100.0;
        public double FinMs = 250.0;
        public double MinMs = 250.0;
        public double MaxMs = 3000.0;
        public double QuietudMs = 400.0;
        public double UmbralVelocidad = 0.5;   // anchos de hombro por segundo
        public double LineaReposo = 1.5;       // anchos de hombro bajo la línea de hombros
        public bool UsarQuietud = true;
        public double FactorManoAHombros = 3.5;

        public ParametrosSegmentacion Copiar() => (ParametrosSegmentacion)MemberwiseClone();
    }

    /// <summary>Mide si un cuadro está "activo" y la velocidad de las muñecas.</summary>
    public static class MedidorActividad
    {
        public static bool Presente(float[] c, int idx)
        {
            int k = idx * 3;
            return (Math.Abs((double)c[k]) + Math.Abs((double)c[k + 1]) + Math.Abs((double)c[k + 2])) > 0.0;
        }

        static double Dist2D(float[] a, int ia, float[] b, int ib)
        {
            double dx = (double)a[ia * 3] - (double)b[ib * 3];
            double dy = (double)a[ia * 3 + 1] - (double)b[ib * 3 + 1];
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public static double AnchoReferencia(float[] c, ParametrosSegmentacion p)
        {
            if (Presente(c, FormatoLandmarks.IdxHombroIzq) && Presente(c, FormatoLandmarks.IdxHombroDer))
                return Dist2D(c, FormatoLandmarks.IdxHombroIzq, c, FormatoLandmarks.IdxHombroDer);
            foreach (int inicio in new[] { FormatoLandmarks.InicioManoIzq, FormatoLandmarks.InicioManoDer })
            {
                int m = inicio + FormatoLandmarks.ManoMuneca, b = inicio + FormatoLandmarks.ManoBaseMedio;
                if (Presente(c, m) && Presente(c, b)) return p.FactorManoAHombros * Dist2D(c, m, c, b);
            }
            return 0.0;
        }

        /// <summary>Devuelve si el cuadro está activo; <paramref name="velocidad"/> = +∞ si no se puede medir.</summary>
        public static bool Medir(float[] actual, float[] anterior, double dtS, ParametrosSegmentacion p, out double velocidad)
        {
            int mi = FormatoLandmarks.IdxMunecaIzq, md = FormatoLandmarks.IdxMunecaDer;
            bool pi = Presente(actual, mi), pd = Presente(actual, md);
            if (!pi && !pd)
            {
                velocidad = 0.0;
                return false;
            }

            bool activo = false;
            if (Presente(actual, FormatoLandmarks.IdxHombroIzq) && Presente(actual, FormatoLandmarks.IdxHombroDer))
            {
                double ancho = Dist2D(actual, FormatoLandmarks.IdxHombroIzq, actual, FormatoLandmarks.IdxHombroDer);
                double yHombros = ((double)actual[FormatoLandmarks.IdxHombroIzq * 3 + 1] +
                                   (double)actual[FormatoLandmarks.IdxHombroDer * 3 + 1]) / 2.0;
                if (pi && (ancho < 1e-6 || (double)actual[mi * 3 + 1] < yHombros + p.LineaReposo * ancho)) activo = true;
                if (pd && (ancho < 1e-6 || (double)actual[md * 3 + 1] < yHombros + p.LineaReposo * ancho)) activo = true;
            }
            else
            {
                activo = true;
            }

            velocidad = double.PositiveInfinity;
            double escala = AnchoReferencia(actual, p);
            if (anterior != null && dtS > 0.0 && escala > 1e-6)
            {
                double vmax = -1.0;
                if (pi && Presente(anterior, mi)) vmax = Math.Max(vmax, Dist2D(actual, mi, anterior, mi) / escala / dtS);
                if (pd && Presente(anterior, md)) vmax = Math.Max(vmax, Dist2D(actual, md, anterior, md) / escala / dtS);
                if (vmax >= 0.0) velocidad = vmax;
            }
            return activo;
        }
    }

    /// <summary>Segmento detectado (tiempos en milisegundos del flujo).</summary>
    public readonly struct Segmento
    {
        public readonly double InicioMs;
        public readonly double FinMs;
        public Segmento(double inicio, double fin) { InicioMs = inicio; FinMs = fin; }
        public double DuracionMs => FinMs - InicioMs;
        public override string ToString() => $"[{InicioMs:0.0}, {FinMs:0.0}]";
    }

    /// <summary>
    /// Máquina de estados que detecta inicio y fin de señas.
    /// Copia exacta de ml/lsrd_ml/segmentacion.py (verificada con vectores de oro).
    /// </summary>
    public sealed class Segmentador
    {
        public enum Estado { Reposo = 0, Posible = 1, Sena = 2, Espera = 3 }

        public ParametrosSegmentacion Parametros { get; }
        public Estado EstadoActual { get; private set; }
        public bool EnSena => EstadoActual == Estado.Sena;
        public double InicioActualMs => tInicio;

        double tInicio, tUltimoActivo;
        double? tQuieto, tInactivo;

        public Segmentador(ParametrosSegmentacion parametros = null)
        {
            Parametros = parametros ?? new ParametrosSegmentacion();
            Reiniciar();
        }

        public void Reiniciar()
        {
            EstadoActual = Estado.Reposo;
            tInicio = 0.0;
            tUltimoActivo = 0.0;
            tQuieto = null;
            tInactivo = null;
        }

        Segmento? Emitir(double ini, double fin)
        {
            if (fin - ini >= Parametros.MinMs) return new Segmento(ini, fin);
            return null;
        }

        public Segmento? Procesar(double tMs, bool activo, double velocidad)
        {
            var p = Parametros;
            switch (EstadoActual)
            {
                case Estado.Reposo:
                    if (activo)
                    {
                        EstadoActual = Estado.Posible;
                        tInicio = tMs;
                    }
                    return null;

                case Estado.Posible:
                    if (!activo)
                    {
                        EstadoActual = Estado.Reposo;
                        return null;
                    }
                    if (tMs - tInicio >= p.InicioMs)
                    {
                        EstadoActual = Estado.Sena;
                        tUltimoActivo = tMs;
                        tQuieto = null;
                    }
                    return null;

                case Estado.Sena:
                    if (activo)
                    {
                        tUltimoActivo = tMs;
                        if (p.UsarQuietud && velocidad < p.UmbralVelocidad)
                        {
                            if (tQuieto == null)
                            {
                                tQuieto = tMs;
                            }
                            else if (tMs - tQuieto.Value >= p.QuietudMs && tMs - tInicio >= p.MinMs)
                            {
                                var seg = Emitir(tInicio, tMs);
                                EstadoActual = Estado.Espera;
                                tInactivo = null;
                                return seg;
                            }
                        }
                        else
                        {
                            tQuieto = null;
                        }
                        if (tMs - tInicio >= p.MaxMs)
                        {
                            var seg = Emitir(tInicio, tMs);
                            tInicio = tMs;
                            tQuieto = null;
                            return seg;
                        }
                        return null;
                    }
                    if (tMs - tUltimoActivo >= p.FinMs)
                    {
                        EstadoActual = Estado.Reposo;
                        return Emitir(tInicio, tUltimoActivo);
                    }
                    return null;

                default: // Espera
                    if (activo)
                    {
                        tInactivo = null;
                        if (velocidad >= p.UmbralVelocidad)
                        {
                            EstadoActual = Estado.Posible;
                            tInicio = tMs;
                        }
                    }
                    else
                    {
                        if (tInactivo == null) tInactivo = tMs;
                        else if (tMs - tInactivo.Value >= p.FinMs) EstadoActual = Estado.Reposo;
                    }
                    return null;
            }
        }

        /// <summary>Fin del flujo: emite la seña en curso, si la hay.</summary>
        public Segmento? Cerrar()
        {
            Segmento? seg = null;
            if (EstadoActual == Estado.Sena) seg = Emitir(tInicio, tUltimoActivo);
            Reiniciar();
            return seg;
        }
    }
}
