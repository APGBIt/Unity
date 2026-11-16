using System;
using System.Collections.Generic;
using System.IO;

namespace LSRD.Nucleo
{
    /// <summary>
    /// Detector de actividad de voz por energía, para la escucha automática del empleado.
    /// Recibe bloques de muestras y devuelve la frase completa cuando termina el habla.
    /// </summary>
    public sealed class DetectorVoz
    {
        public float Umbral = 0.02f;      // RMS
        public double InicioMs = 150;     // voz continua para empezar
        public double FinMs = 800;        // silencio para terminar
        public double MaxMs = 15000;
        public double PrevioMs = 300;     // audio previo que se conserva
        public double MinMs = 400;

        readonly int frecuencia;
        readonly int tamBloque;
        readonly List<float> pendiente = new List<float>();
        readonly List<float> frase = new List<float>();
        readonly Queue<float[]> previo = new Queue<float[]>();
        bool hablando;
        double msVoz, msSilencio;

        public bool Hablando => hablando;
        public float UltimoNivel { get; private set; }

        public DetectorVoz(int frecuencia)
        {
            this.frecuencia = frecuencia;
            tamBloque = Math.Max(1, frecuencia / 50); // 20 ms
        }

        double MsBloque => 1000.0 * tamBloque / frecuencia;

        public void Reiniciar()
        {
            pendiente.Clear();
            frase.Clear();
            previo.Clear();
            hablando = false;
            msVoz = msSilencio = 0;
        }

        /// <summary>Agrega muestras; devuelve una frase terminada o null.</summary>
        public float[] Procesar(float[] muestras, int cantidad)
        {
            float[] resultado = null;
            for (int i = 0; i < cantidad; i++) pendiente.Add(muestras[i]);
            int k = 0;
            while (pendiente.Count - k >= tamBloque)
            {
                var bloque = pendiente.GetRange(k, tamBloque).ToArray();
                k += tamBloque;
                var r = ProcesarBloque(bloque);
                if (r != null) resultado = r;
            }
            pendiente.RemoveRange(0, k);
            return resultado;
        }

        float[] ProcesarBloque(float[] bloque)
        {
            double suma = 0;
            foreach (var m in bloque) suma += m * m;
            float rms = (float)Math.Sqrt(suma / bloque.Length);
            UltimoNivel = rms;
            bool voz = rms >= Umbral;

            if (!hablando)
            {
                previo.Enqueue(bloque);
                while (previo.Count * MsBloque > PrevioMs + InicioMs) previo.Dequeue();
                msVoz = voz ? msVoz + MsBloque : 0;
                if (msVoz >= InicioMs)
                {
                    hablando = true;
                    msSilencio = 0;
                    frase.Clear();
                    foreach (var b in previo) frase.AddRange(b);
                    previo.Clear();
                }
                return null;
            }

            frase.AddRange(bloque);
            msSilencio = voz ? 0 : msSilencio + MsBloque;
            double duracion = 1000.0 * frase.Count / frecuencia;
            if (msSilencio >= FinMs || duracion >= MaxMs)
            {
                hablando = false;
                msVoz = 0;
                var salida = frase.ToArray();
                frase.Clear();
                return duracion >= MinMs ? salida : null;
            }
            return null;
        }
    }

    /// <summary>Codificación WAV PCM de 16 bits, mono.</summary>
    public static class Wav
    {
        public static byte[] Codificar(float[] muestras, int frecuencia)
        {
            using (var ms = new MemoryStream(44 + muestras.Length * 2))
            using (var w = new BinaryWriter(ms))
            {
                int datos = muestras.Length * 2;
                w.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
                w.Write(36 + datos);
                w.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E', (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
                w.Write(16);
                w.Write((short)1);           // PCM
                w.Write((short)1);           // mono
                w.Write(frecuencia);
                w.Write(frecuencia * 2);     // bytes por segundo
                w.Write((short)2);           // alineación
                w.Write((short)16);          // bits
                w.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
                w.Write(datos);
                foreach (var m in muestras)
                {
                    float c = Math.Max(-1f, Math.Min(1f, m));
                    w.Write((short)Math.Round(c * 32767f));
                }
                w.Flush();
                return ms.ToArray();
            }
        }
    }
}
