using System;
using System.Collections;
using LSRD.Nucleo;
using UnityEngine;

namespace LSRD.App
{
    /// <summary>
    /// Abre la cámara y produce en <see cref="Salida"/> una imagen derecha (sin espejo) que usan
    /// tanto el detector como la pantalla. El espejo para la persona que se ve a sí misma se aplica
    /// solo al mostrarla.
    /// </summary>
    public sealed class FuenteCamara : MonoBehaviour
    {
        public RenderTexture Salida { get; private set; }
        public bool EsFrontal { get; private set; }
        public int CuadrosProcesados { get; private set; }
        public string Error { get; private set; }
        public string NombreDispositivo => camara != null ? camara.deviceName : "";
        public bool Lista => Salida != null && camara != null && camara.isPlaying && camara.width > 16;
        public double Aspecto => Salida != null && Salida.height > 0 ? (double)Salida.width / Salida.height : 1.0;
        public int RotacionForzada = -1;

        /// <summary>Se dispara cuando hay un cuadro nuevo en <see cref="Salida"/>.</summary>
        public event Action AlCuadro;

        WebCamTexture camara;
        Material material;

        public IEnumerator Iniciar(bool frontal, int rotacionForzada)
        {
            Detener();
            Error = null;
            RotacionForzada = rotacionForzada;
            yield return Permisos.SolicitarCamara();
            if (!Permisos.Camara)
            {
                Error = "La app no tiene permiso para usar la cámara.";
                yield break;
            }
            var dispositivos = WebCamTexture.devices;
            if (dispositivos.Length == 0)
            {
                Error = "No se encontró ninguna cámara.";
                yield break;
            }
            var elegido = dispositivos[0];
            foreach (var d in dispositivos)
                if (d.isFrontFacing == frontal) { elegido = d; break; }
            EsFrontal = elegido.isFrontFacing;

            var shader = Resources.Load<Shader>("LSRD/CamaraNormalizada");
            if (shader == null)
            {
                Error = "Falta el sombreador LSRD/CamaraNormalizada en Resources.";
                yield break;
            }
            material = new Material(shader);
            camara = new WebCamTexture(elegido.name, 1280, 720, 30);
            camara.Play();

            float espera = 0f;
            while (camara.width <= 16 && espera < 8f)
            {
                espera += Time.unscaledDeltaTime;
                yield return null;
            }
            if (camara.width <= 16) Error = "La cámara no entregó imágenes.";
        }

        void Update()
        {
            if (camara == null || !camara.isPlaying || !camara.didUpdateThisFrame || camara.width <= 16) return;
            int rot = RotacionForzada >= 0 ? RotacionForzada : camara.videoRotationAngle;
            var t = TransformacionCamara.Calcular(rot, camara.videoVerticallyMirrored);
            t.TamanoSalida(camara.width, camara.height, out int ancho, out int alto);
            if (Salida == null || Salida.width != ancho || Salida.height != alto)
            {
                if (Salida != null) Salida.Release();
                Salida = new RenderTexture(ancho, alto, 0, RenderTextureFormat.ARGB32) { name = "LSRD-Camara" };
                Salida.Create();
            }
            material.SetVector("_Transformacion", new Vector4(t.M00, t.M01, t.M10, t.M11));
            Graphics.Blit(camara, Salida, material);
            CuadrosProcesados++;
            AlCuadro?.Invoke();
        }

        public void Detener()
        {
            if (camara != null)
            {
                camara.Stop();
                Destroy(camara);
                camara = null;
            }
            if (Salida != null)
            {
                Salida.Release();
                Destroy(Salida);
                Salida = null;
            }
            if (material != null)
            {
                Destroy(material);
                material = null;
            }
        }

        void OnDestroy() => Detener();
    }
}
