using System;
using LSRD.Nucleo;
using UnityEngine;
#if LSRD_INFERENCIA
using Unity.InferenceEngine;
#endif

namespace LSRD.App
{
    /// <summary>
    /// Modelo ONNX cargado desde Resources/LSRD/Modelos (senas.onnx o dactilologia.onnx + *_info.json),
    /// ejecutado con Unity Inference Engine en la CPU (son modelos pequeños).
    /// </summary>
    public sealed class ClasificadorOnnx : IDisposable
    {
        public InfoModelo Info { get; private set; }
        public string Nombre { get; private set; }
        readonly int[] forma;
#if LSRD_INFERENCIA
        Worker trabajador;
#endif

        ClasificadorOnnx(string nombre, int[] forma)
        {
            Nombre = nombre;
            this.forma = forma;
        }

        /// <summary>Carga el modelo; devuelve null y un mensaje si no está disponible.</summary>
        public static ClasificadorOnnx Cargar(string nombre, int[] forma, out string error)
        {
            error = null;
            var info = Resources.Load<TextAsset>("LSRD/Modelos/" + nombre + "_info");
            if (info == null)
            {
                error = $"Falta {nombre}_info.json en Resources/LSRD/Modelos.";
                return null;
            }
            var c = new ClasificadorOnnx(nombre, forma);
            try
            {
                c.Info = InfoModelo.DesdeJson(info.text);
            }
            catch (Exception e)
            {
                error = $"{nombre}_info.json inválido: {e.Message}";
                return null;
            }
#if LSRD_INFERENCIA
            var activo = Resources.Load<ModelAsset>("LSRD/Modelos/" + nombre);
            if (activo == null)
            {
                error = $"Falta {nombre}.onnx en Resources/LSRD/Modelos.";
                return null;
            }
            try
            {
                var modelo = ModelLoader.Load(activo);
                c.trabajador = new Worker(modelo, BackendType.CPU);
            }
            catch (Exception e)
            {
                error = $"No se pudo cargar {nombre}.onnx: {e.Message}";
                return null;
            }
            return c;
#else
            error = "Unity Inference Engine (com.unity.ai.inference) no está instalado.";
            return null;
#endif
        }

        /// <summary>Probabilidades por clase (en el orden de <see cref="InfoModelo.Clases"/>).</summary>
        public float[] Predecir(float[] entrada)
        {
#if LSRD_INFERENCIA
            var forma4 = forma.Length == 4
                ? new TensorShape(forma[0], forma[1], forma[2], forma[3])
                : new TensorShape(forma[0], forma[1], forma[2]);
            using (var tensor = new Tensor<float>(forma4, entrada))
            {
                trabajador.Schedule(tensor);
                var salida = trabajador.PeekOutput("probs") as Tensor<float>;
                return salida.DownloadToArray();
            }
#else
            throw new InvalidOperationException("Inference Engine no está instalado");
#endif
        }

        public void Dispose()
        {
#if LSRD_INFERENCIA
            trabajador?.Dispose();
            trabajador = null;
#endif
        }
    }
}
