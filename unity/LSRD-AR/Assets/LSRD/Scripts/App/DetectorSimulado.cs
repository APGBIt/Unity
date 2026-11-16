using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using LSRD.Nucleo;
using UnityEngine;

namespace LSRD.App
{
    /// <summary>
    /// Reproduce grabaciones (Resources/LSRD/Simulacion/*.json) como si vinieran de la cámara,
    /// separadas por pausas sin manos. Permite probar segmentación, modelos e interfaz en el editor.
    /// </summary>
    public sealed class DetectorSimulado : MonoBehaviour, IDetectorLandmarks
    {
        public bool Listo { get; private set; }
        public string Estado { get; private set; } = "Sin iniciar";
        public string GlosaActual { get; private set; } = "";
        public float PausaEntreSenasS = 1.0f;
        public float PausaEntreFrasesS = 2.5f;
        public int SenasPorFrase = 3;
        public event Action<CuadroLandmarks> AlCuadro;

        readonly List<MuestraLandmarks> muestras = new List<MuestraLandmarks>();
        readonly float[] reposo = new float[FormatoLandmarks.ValoresPorCuadro];
        readonly CuadroLandmarks cuadro = new CuadroLandmarks();
        int indiceMuestra, indiceCuadro, enFrase;
        double inicioMuestraMs, finPausaMs, siguienteReposoMs;
        bool enPausa = true;
        System.Random azar = new System.Random(7);

        public IEnumerator Iniciar(FuenteCamara camara, AjustesApp ajustes)
        {
            muestras.Clear();
            foreach (var t in Resources.LoadAll<TextAsset>("LSRD/Simulacion"))
            {
                try { muestras.Add(MuestraLandmarks.DesdeJson(t.text)); }
                catch (Exception e) { Debug.LogWarning($"[LSRD] Simulación: no se pudo leer {t.name}: {e.Message}"); }
            }
            if (muestras.Count == 0)
            {
                Estado = "No hay grabaciones en Resources/LSRD/Simulacion";
                yield break;
            }
            // Cuadro de reposo: el cuerpo de la primera muestra sin manos
            Array.Copy(muestras[0].Cuadros[0], reposo, reposo.Length);
            for (int i = FormatoLandmarks.InicioManoIzq * 3; i < FormatoLandmarks.InicioCara * 3; i++) reposo[i] = 0f;
            Listo = true;
            Estado = $"Simulando {muestras.Count} grabaciones";
            finPausaMs = Ahora() + 1000;
        }

        static double Ahora() => Time.realtimeSinceStartupAsDouble * 1000.0;

        void Update()
        {
            if (!Listo) return;
            double t = Ahora();
            if (enPausa)
            {
                if (t >= siguienteReposoMs)
                {
                    Emitir(t, reposo, 1.0);
                    siguienteReposoMs = t + 33;
                }
                if (t < finPausaMs) return;
                enPausa = false;
                indiceMuestra = azar.Next(muestras.Count);
                indiceCuadro = 0;
                inicioMuestraMs = t;
                GlosaActual = muestras[indiceMuestra].Glosa;
            }
            var m = muestras[indiceMuestra];
            double rel = t - inicioMuestraMs;
            double aspecto = m.CamaraAlto > 0 ? (double)m.CamaraAncho / m.CamaraAlto : 1.0;
            while (indiceCuadro < m.NumeroCuadros && m.TiemposMs[indiceCuadro] <= rel)
            {
                Emitir(inicioMuestraMs + m.TiemposMs[indiceCuadro], m.Cuadros[indiceCuadro], aspecto);
                indiceCuadro++;
            }
            if (indiceCuadro >= m.NumeroCuadros)
            {
                enPausa = true;
                enFrase++;
                bool finFrase = enFrase >= SenasPorFrase;
                if (finFrase) enFrase = 0;
                finPausaMs = t + 1000.0 * (finFrase ? PausaEntreFrasesS : PausaEntreSenasS);
                GlosaActual = "";
            }
        }

        void Emitir(double t, float[] datos, double aspecto)
        {
            cuadro.TiempoMs = t;
            cuadro.Aspecto = aspecto;
            Array.Copy(datos, cuadro.Datos, cuadro.Datos.Length);
            AlCuadro?.Invoke(cuadro);
        }

        public void Detener()
        {
            Listo = false;
            Estado = "Detenido";
        }
    }
}
