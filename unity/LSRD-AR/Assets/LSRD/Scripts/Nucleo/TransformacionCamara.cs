using System;

namespace LSRD.Nucleo
{
    /// <summary>
    /// Transformación que lleva la imagen cruda de la cámara a una imagen derecha.
    ///
    /// Unity entrega <c>WebCamTexture.videoRotationAngle</c> (giro horario para verla derecha) y
    /// <c>videoVerticallyMirrored</c>. El sombreador de normalización muestrea, para cada punto de
    /// salida (u, v), el punto de entrada <c>M · (u − ½, v − ½) + ½</c>, con
    /// <c>M = EspejoV · Rot_antihorario(θ) · EspejoH</c>.
    /// </summary>
    public readonly struct TransformacionCamara
    {
        public readonly float M00, M01, M10, M11;
        public readonly int Rotacion;

        TransformacionCamara(float m00, float m01, float m10, float m11, int rotacion)
        {
            M00 = m00; M01 = m01; M10 = m10; M11 = m11; Rotacion = rotacion;
        }

        public bool IntercambiaEjes => Rotacion == 90 || Rotacion == 270;

        public static int NormalizarGrados(int grados)
        {
            int r = ((grados % 360) + 360) % 360;
            return (int)(Math.Round(r / 90.0) * 90) % 360;
        }

        public static TransformacionCamara Calcular(int rotacionGrados, bool espejoVertical, bool espejoHorizontal = false)
        {
            int r = NormalizarGrados(rotacionGrados);
            int c, s;
            switch (r)
            {
                case 90: c = 0; s = 1; break;
                case 180: c = -1; s = 0; break;
                case 270: c = 0; s = -1; break;
                default: c = 1; s = 0; break;
            }
            // Rot antihoraria: [[c, -s], [s, c]]
            float a = c, b = -s, d = s, e = c;
            if (espejoHorizontal) { a = -a; d = -d; }       // · diag(−1, 1) por la derecha
            if (espejoVertical) { d = -d; e = -e; }         // diag(1, −1) · por la izquierda
            return new TransformacionCamara(a, b, d, e, r);
        }

        /// <summary>Punto de entrada (u, v) que corresponde al punto de salida (u, v); origen abajo a la izquierda.</summary>
        public void Mapear(float uSalida, float vSalida, out float uEntrada, out float vEntrada)
        {
            float cx = uSalida - 0.5f, cy = vSalida - 0.5f;
            uEntrada = M00 * cx + M01 * cy + 0.5f;
            vEntrada = M10 * cx + M11 * cy + 0.5f;
        }

        public void TamanoSalida(int anchoFuente, int altoFuente, out int ancho, out int alto)
        {
            if (IntercambiaEjes) { ancho = altoFuente; alto = anchoFuente; }
            else { ancho = anchoFuente; alto = altoFuente; }
        }
    }
}
