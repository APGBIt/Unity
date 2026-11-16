using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using LSRD.Nucleo;
using UnityEngine;

namespace LSRD.App
{
#if LSRD_MEDIAPIPE
    /// <summary>
    /// Detector holístico de MediaPipe (pose + manos + cara en una pasada) con el plugin
    /// homuler/MediaPipeUnityPlugin. Trabaja en modo LIVE_STREAM: los resultados llegan en otro
    /// hilo, se empaquetan en formato LSRD-LM v1 y se entregan en el hilo principal.
    /// </summary>
    public sealed class DetectorHolistico : MonoBehaviour, IDetectorLandmarks
    {
        public bool Listo { get; private set; }
        public string Estado { get; private set; } = "Sin iniciar";
        public event Action<CuadroLandmarks> AlCuadro;
        public float CuadrosPorSegundoMax = 30f;

        Mediapipe.Tasks.Vision.HolisticLandmarker.HolisticLandmarker detector;
        Mediapipe.Unity.Experimental.TextureFramePool pool;
        FuenteCamara camara;
        readonly ConcurrentQueue<CuadroLandmarks> cola = new ConcurrentQueue<CuadroLandmarks>();
        readonly ConcurrentBag<CuadroLandmarks> libres = new ConcurrentBag<CuadroLandmarks>();
        readonly System.Diagnostics.Stopwatch reloj = new System.Diagnostics.Stopwatch();
        long ultimoEnvioMs = -1000;
        double aspectoActual = 1.0;
        int ultimoCuadroCamara = -1;
        bool glogIniciado;

        // Búferes reutilizados en el hilo del callback
        readonly float[] pose = new float[33 * 4];
        readonly float[] manoI = new float[21 * 3];
        readonly float[] manoD = new float[21 * 3];
        readonly float[] cara = new float[478 * 3];

        public IEnumerator Iniciar(FuenteCamara fuente, AjustesApp ajustes)
        {
            camara = fuente;
            var modelo = Resources.Load<TextAsset>("LSRD/MediaPipe/holistic_landmarker");
            if (modelo == null)
            {
                Estado = "Falta el modelo holistic_landmarker (menú LSRD > Preparar proyecto).";
                yield break;
            }
            try
            {
                Mediapipe.Protobuf.SetLogHandler(Mediapipe.Protobuf.DefaultLogHandler);
                Mediapipe.Glog.Initialize("LSRD");
                glogIniciado = true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[LSRD] No se pudo iniciar el registro de MediaPipe: " + e.Message);
            }

            var delegado = Mediapipe.Tasks.Core.BaseOptions.Delegate.CPU;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (ajustes.UsarGpuMediaPipe)
            {
                yield return Mediapipe.Unity.GpuManager.Initialize();
                if (Mediapipe.Unity.GpuManager.IsInitialized) delegado = Mediapipe.Tasks.Core.BaseOptions.Delegate.GPU;
            }
#endif
            var opciones = new Mediapipe.Tasks.Vision.HolisticLandmarker.HolisticLandmarkerOptions(
                new Mediapipe.Tasks.Core.BaseOptions(delegado, modelAssetBuffer: modelo.bytes),
                runningMode: Mediapipe.Tasks.Vision.Core.RunningMode.LIVE_STREAM,
                minFaceDetectionConfidence: 0.5f,
                minPoseDetectionConfidence: 0.5f,
                minPoseLandmarksConfidence: 0.5f,
                minHandLandmarksConfidence: 0.5f,
                resultCallback: AlResultado);
            try
            {
                detector = Mediapipe.Tasks.Vision.HolisticLandmarker.HolisticLandmarker.CreateFromOptions(
                    opciones, delegado == Mediapipe.Tasks.Core.BaseOptions.Delegate.GPU ? Mediapipe.Unity.GpuManager.GpuResources : null);
            }
            catch (Exception e)
            {
                Estado = "No se pudo crear el detector de MediaPipe: " + e.Message;
                yield break;
            }
            reloj.Start();
            Listo = true;
            Estado = "MediaPipe holístico (" + delegado + ")";
        }

        // Lectura asíncrona de la GPU (evita trabar la interfaz) cuando el dispositivo la soporta.
        Mediapipe.Unity.Experimental.TextureFrame marcoPendiente;
        UnityEngine.Rendering.AsyncGPUReadbackRequest lectura;
        long tiempoPendiente;
        double aspectoPendiente;

        void Update()
        {
            while (cola.TryDequeue(out var c))
            {
                AlCuadro?.Invoke(c);
                libres.Add(c);
            }
            if (!Listo) return;

            if (marcoPendiente != null)
            {
                if (!lectura.done) return;
                var marco = marcoPendiente;
                marcoPendiente = null;
                if (lectura.hasError) { marco.Release(); return; }
                Enviar(marco, tiempoPendiente, aspectoPendiente);
                return;
            }

            if (camara == null || !camara.Lista || camara.CuadrosProcesados == ultimoCuadroCamara) return;
            long ahora = reloj.ElapsedMilliseconds;
            if (ahora - ultimoEnvioMs < 1000f / CuadrosPorSegundoMax) return;
            var rt = camara.Salida;
            if (pool == null || pool.textureWidth != rt.width || pool.textureHeight != rt.height)
            {
                pool?.Dispose();
                pool = new Mediapipe.Unity.Experimental.TextureFramePool(rt.width, rt.height, TextureFormat.RGBA32, 10);
            }
            if (!pool.TryGetTextureFrame(out var nuevo)) return;
            ultimoCuadroCamara = camara.CuadrosProcesados;
            ultimoEnvioMs = ahora;
            double aspecto = (double)rt.width / rt.height;
            // La textura de Unity tiene el origen abajo; MediaPipe lo espera arriba: se invierte en vertical.
            if (SystemInfo.supportsAsyncGPUReadback)
            {
                lectura = nuevo.ReadTextureAsync(rt, false, true);
                marcoPendiente = nuevo;
                tiempoPendiente = ahora;
                aspectoPendiente = aspecto;
            }
            else
            {
                nuevo.ReadTextureOnCPU(rt, false, true);
                Enviar(nuevo, ahora, aspecto);
            }
        }

        void Enviar(Mediapipe.Unity.Experimental.TextureFrame marco, long tiempo, double aspecto)
        {
            var imagen = marco.BuildCPUImage();
            marco.Release();
            aspectoActual = aspecto;
            try
            {
                detector.DetectAsync(imagen, tiempo);  // el paquete toma posesión de la imagen
            }
            catch (Exception e)
            {
                Estado = "Error de MediaPipe: " + e.Message;
            }
        }

        void AlResultado(in Mediapipe.Tasks.Vision.HolisticLandmarker.HolisticLandmarkerResult r, Mediapipe.Image imagen, long tiempoMs)
        {
            var hayPose = Copiar(r.poseLandmarks.landmarks, pose, 4, 33);
            var hayI = Copiar(r.leftHandLandmarks.landmarks, manoI, 3, 21);
            var hayD = Copiar(r.rightHandLandmarks.landmarks, manoD, 3, 21);
            var hayCara = Copiar(r.faceLandmarks.landmarks, cara, 3, 468);
            if (!libres.TryTake(out var c)) c = new CuadroLandmarks();
            c.TiempoMs = tiempoMs;
            c.Aspecto = aspectoActual;
            EmpaquetadorLandmarks.Empaquetar(hayPose ? pose : null, hayI ? manoI : null, hayD ? manoD : null,
                hayCara ? cara : null, aspectoActual, c.Datos);
            cola.Enqueue(c);
        }

        static bool Copiar(List<Mediapipe.Tasks.Components.Containers.NormalizedLandmark> lista, float[] destino, int paso, int minimo)
        {
            if (lista == null || lista.Count < minimo) return false;
            int n = Math.Min(lista.Count, destino.Length / paso);
            for (int i = 0; i < n; i++)
            {
                var p = lista[i];
                destino[i * paso] = p.x;
                destino[i * paso + 1] = p.y;
                destino[i * paso + 2] = p.z;
                if (paso == 4) destino[i * paso + 3] = p.visibility ?? float.NaN;
            }
            return true;
        }

        public void Detener()
        {
            Listo = false;
            try { detector?.Close(); } catch (Exception) { /* ya cerrado */ }
            detector = null;
            marcoPendiente = null;
            pool?.Dispose();
            pool = null;
            Estado = "Detenido";
        }

        void OnDestroy()
        {
            Detener();
            if (glogIniciado)
            {
                try { Mediapipe.Glog.Shutdown(); } catch (Exception) { /* ignorar */ }
            }
        }
    }
#endif
}
