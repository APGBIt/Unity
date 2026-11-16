using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using LSRD.Nucleo;
using UnityEngine;

namespace LSRD.App
{
    /// <summary>
    /// Micrófono del empleado: "mantener para hablar" o escucha automática por detección de voz.
    /// Entrega cada frase como WAV de 16 kHz.
    /// </summary>
    public sealed class Microfono : MonoBehaviour
    {
        public const int Frecuencia = 16000;
        public event Action<byte[]> AlFrase;
        public bool EscuchaAutomatica;
        public bool Silenciado;          // mientras la app habla, para no escucharse a sí misma
        public bool Listo { get; private set; }
        public bool Pulsando { get; private set; }
        public string Error { get; private set; }
        public float Nivel => vad?.UltimoNivel ?? 0f;
        public bool HablandoDetectado => vad != null && vad.Hablando;

        AudioClip clip;
        int ultimaPosicion;
        float[] bloque = new float[Frecuencia];
        DetectorVoz vad;
        readonly List<float> manual = new List<float>();

        public IEnumerator Iniciar(float umbralVoz)
        {
            yield return Permisos.SolicitarMicrofono();
            if (!Permisos.Microfono) { Error = "La app no tiene permiso para usar el micrófono."; yield break; }
            if (Microphone.devices.Length == 0) { Error = "No se encontró un micrófono."; yield break; }
            clip = Microphone.Start(null, true, 10, Frecuencia);
            float t = 0f;
            while (Microphone.GetPosition(null) <= 0 && t < 3f) { t += Time.unscaledDeltaTime; yield return null; }
            vad = new DetectorVoz(Frecuencia) { Umbral = umbralVoz };
            ultimaPosicion = Microphone.GetPosition(null);
            Listo = true;
        }

        public void EmpezarPulsacion()
        {
            manual.Clear();
            Pulsando = true;
        }

        public void TerminarPulsacion()
        {
            if (!Pulsando) return;
            Pulsando = false;
            if (manual.Count > Frecuencia / 4) AlFrase?.Invoke(Wav.Codificar(manual.ToArray(), Frecuencia));
            manual.Clear();
            vad?.Reiniciar();
        }

        void Update()
        {
            if (!Listo || clip == null) return;
            int pos = Microphone.GetPosition(null);
            if (pos < 0 || pos == ultimaPosicion) return;
            int total = clip.samples;
            int nuevas = (pos - ultimaPosicion + total) % total;
            if (nuevas > bloque.Length) bloque = new float[nuevas];
            if (ultimaPosicion + nuevas <= total)
            {
                LeerDesde(ultimaPosicion, nuevas, 0);
            }
            else
            {
                int primera = total - ultimaPosicion;
                LeerDesde(ultimaPosicion, primera, 0);
                LeerDesde(0, nuevas - primera, primera);
            }
            ultimaPosicion = pos;

            if (Pulsando)
            {
                for (int i = 0; i < nuevas; i++) manual.Add(bloque[i]);
                return;
            }
            if (!EscuchaAutomatica || Silenciado)
            {
                vad.Reiniciar();
                return;
            }
            var frase = vad.Procesar(bloque, nuevas);
            if (frase != null) AlFrase?.Invoke(Wav.Codificar(frase, Frecuencia));
        }

        void LeerDesde(int desde, int cantidad, int destino)
        {
            if (cantidad <= 0) return;
            var tmp = new float[cantidad];
            clip.GetData(tmp, desde);
            Array.Copy(tmp, 0, bloque, destino, cantidad);
        }

        public void CambiarUmbral(float umbral)
        {
            if (vad != null) vad.Umbral = umbral;
        }

        void OnDestroy()
        {
            if (Listo) Microphone.End(null);
        }
    }
}
