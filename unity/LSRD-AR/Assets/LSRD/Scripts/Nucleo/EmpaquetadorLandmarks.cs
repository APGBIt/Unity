using System;

namespace LSRD.Nucleo
{
    /// <summary>
    /// Convierte la salida del detector holístico de MediaPipe al formato canónico LSRD-LM v1
    /// (copia exacta de <c>empaquetar</c> y <c>mano_activa</c> en Python).
    /// </summary>
    public static class EmpaquetadorLandmarks
    {
        public const float UmbralVisibilidadPose = 0.5f;

        /// <param name="pose">33 × 4 (x, y, z, visibilidad; NaN = desconocida) o null.</param>
        /// <param name="manoIzq">21 × 3 o null.</param>
        /// <param name="manoDer">21 × 3 o null.</param>
        /// <param name="cara">≥ 468 × 3 o null.</param>
        /// <param name="aspecto">ancho / alto de la imagen procesada.</param>
        /// <param name="destino">arreglo de <see cref="FormatoLandmarks.ValoresPorCuadro"/> valores.</param>
        public static void Empaquetar(float[] pose, float[] manoIzq, float[] manoDer, float[] cara, double aspecto, float[] destino)
        {
            if (destino == null || destino.Length != FormatoLandmarks.ValoresPorCuadro)
                throw new ArgumentException("Destino de tamaño incorrecto", nameof(destino));
            Array.Clear(destino, 0, destino.Length);

            if (pose != null && pose.Length >= 33 * 4)
            {
                var indices = FormatoLandmarks.PoseIndicesMediapipe;
                for (int k = 0; k < indices.Length; k++)
                {
                    int o = indices[k] * 4;
                    float vis = pose[o + 3];
                    if (float.IsNaN(vis) || vis >= UmbralVisibilidadPose)
                        Poner(destino, FormatoLandmarks.InicioPose + k, pose[o], pose[o + 1], pose[o + 2], aspecto);
                }
            }
            PonerMano(destino, FormatoLandmarks.InicioManoIzq, manoIzq, aspecto);
            PonerMano(destino, FormatoLandmarks.InicioManoDer, manoDer, aspecto);
            if (cara != null && cara.Length >= 468 * 3)
            {
                var indices = FormatoLandmarks.CaraIndicesMediapipe;
                for (int k = 0; k < indices.Length; k++)
                {
                    int o = indices[k] * 3;
                    Poner(destino, FormatoLandmarks.InicioCara + k, cara[o], cara[o + 1], cara[o + 2], aspecto);
                }
            }
        }

        static void PonerMano(float[] destino, int inicio, float[] mano, double aspecto)
        {
            if (mano == null || mano.Length < FormatoLandmarks.PuntosMano * 3) return;
            for (int k = 0; k < FormatoLandmarks.PuntosMano; k++)
                Poner(destino, inicio + k, mano[k * 3], mano[k * 3 + 1], mano[k * 3 + 2], aspecto);
        }

        static void Poner(float[] d, int idx, float x, float y, float z, double aspecto)
        {
            d[idx * 3] = (float)(x * aspecto);
            d[idx * 3 + 1] = y;
            d[idx * 3 + 2] = (float)(z * aspecto);
        }

        /// <summary>
        /// Mano que deletrea: la visible con la muñeca más alta (empate: la derecha), canonizada como
        /// mano derecha (si es la izquierda se invierte x). Devuelve false si no hay manos.
        /// </summary>
        public static bool ManoActiva(float[] cuadro, float[] destino)
        {
            if (destino == null || destino.Length != FormatoLandmarks.PuntosMano * 3)
                throw new ArgumentException("Destino de tamaño incorrecto", nameof(destino));
            int iz = FormatoLandmarks.InicioManoIzq, de = FormatoLandmarks.InicioManoDer;
            bool hayI = MedidorActividad.Presente(cuadro, iz + FormatoLandmarks.ManoMuneca);
            bool hayD = MedidorActividad.Presente(cuadro, de + FormatoLandmarks.ManoMuneca);
            if (!hayI && !hayD) return false;

            bool usarIzq;
            if (hayI && hayD)
            {
                float yI = cuadro[(iz + FormatoLandmarks.ManoMuneca) * 3 + 1];
                float yD = cuadro[(de + FormatoLandmarks.ManoMuneca) * 3 + 1];
                usarIzq = yI < yD;
            }
            else
            {
                usarIzq = hayI;
            }

            int inicio = usarIzq ? iz : de;
            Array.Copy(cuadro, inicio * 3, destino, 0, FormatoLandmarks.PuntosMano * 3);
            if (usarIzq)
                for (int k = 0; k < FormatoLandmarks.PuntosMano; k++) destino[k * 3] = -destino[k * 3];
            return true;
        }

        /// <summary>Punto canónico (x, y) en unidades de imagen 0–1 (x dividido por el aspecto). False si ausente.</summary>
        public static bool PuntoNormalizado(float[] cuadro, int idx, double aspecto, out float u, out float v)
        {
            u = v = 0f;
            if (cuadro == null || !MedidorActividad.Presente(cuadro, idx)) return false;
            u = (float)(cuadro[idx * 3] / aspecto);
            v = cuadro[idx * 3 + 1];
            return true;
        }
    }
}
