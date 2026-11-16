using System;
using LSRD.Nucleo;
using UnityEngine;
using UnityEngine.UI;

namespace LSRD.App
{
    /// <summary>
    /// Dibuja un cuadro LSRD-LM v1 como figura (brazos, manos, rasgos de la cara).
    /// Sirve de superposición sobre la cámara y de avatar esquemático que reproduce grabaciones
    /// de firmantes sordos.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GraficoEsqueleto : MaskableGraphic
    {
        public float[] Cuadro;
        public double Aspecto = 1.0;
        public bool Espejo;
        public bool ModoAvatar;
        public float Grosor = 7f;

        Vector2 centroAvatar;
        float escalaAvatar = 0.3f;

        static int[] cejaDer, cejaIzq, ojoDer, ojoIzq, bocaExt, bocaInt;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
            if (cejaDer == null)
            {
                cejaDer = Indices(70, 63, 105, 66, 107);
                cejaIzq = Indices(300, 293, 334, 296, 336);
                ojoDer = Indices(33, 159, 133, 145, 33);
                ojoIzq = Indices(263, 386, 362, 374, 263);
                bocaExt = Indices(61, 0, 291, 17, 61);
                bocaInt = Indices(78, 13, 308, 14, 78);
            }
        }

        static int[] Indices(params int[] malla)
        {
            var salida = new int[malla.Length];
            for (int i = 0; i < malla.Length; i++)
                salida[i] = FormatoLandmarks.InicioCara + Array.IndexOf(FormatoLandmarks.CaraIndicesMediapipe, malla[i]);
            return salida;
        }

        public void Mostrar(float[] cuadro, double aspecto)
        {
            Cuadro = cuadro;
            Aspecto = aspecto > 0 ? aspecto : 1.0;
            SetVerticesDirty();
        }

        public void Limpiar()
        {
            Cuadro = null;
            SetVerticesDirty();
        }

        /// <summary>Encuadre estable para una grabación: centra los hombros y normaliza su ancho.</summary>
        public void FijarEncuadre(MuestraLandmarks m)
        {
            float sx = 0, sy = 0, sw = 0;
            int n = 0;
            foreach (var c in m.Cuadros)
            {
                if (!MedidorActividad.Presente(c, FormatoLandmarks.IdxHombroIzq) || !MedidorActividad.Presente(c, FormatoLandmarks.IdxHombroDer)) continue;
                var a = P(c, FormatoLandmarks.IdxHombroIzq);
                var b = P(c, FormatoLandmarks.IdxHombroDer);
                sx += (a.x + b.x) / 2; sy += (a.y + b.y) / 2; sw += Vector2.Distance(a, b);
                n++;
            }
            if (n > 0)
            {
                centroAvatar = new Vector2(sx / n, sy / n);
                escalaAvatar = Mathf.Max(0.05f, sw / n);
            }
            else
            {
                centroAvatar = new Vector2(0.5f, 0.6f);
                escalaAvatar = 0.3f;
            }
        }

        static Vector2 P(float[] c, int idx) => new Vector2(c[idx * 3], c[idx * 3 + 1]);

        bool Posicion(int idx, Rect r, out Vector2 p)
        {
            p = default;
            if (Cuadro == null || !MedidorActividad.Presente(Cuadro, idx)) return false;
            float x = Cuadro[idx * 3], y = Cuadro[idx * 3 + 1];
            if (ModoAvatar)
            {
                // Hombros al 36 % del ancho y a un tercio de la altura: deja margen para manos sobre la cabeza.
                float s = 0.36f * Mathf.Min(r.width, r.height * 0.8f) / escalaAvatar;
                p = new Vector2(r.center.x + (x - centroAvatar.x) * s, r.yMin + 0.33f * r.height - (y - centroAvatar.y) * s);
            }
            else
            {
                float u = (float)(x / Aspecto);
                if (Espejo) u = 1f - u;
                p = new Vector2(r.xMin + u * r.width, r.yMax - y * r.height);
            }
            return true;
        }

        Color ColorDe(int idx)
        {
            if (idx >= FormatoLandmarks.InicioManoIzq && idx < FormatoLandmarks.InicioManoDer) return Tema.ManoIzq;
            if (idx >= FormatoLandmarks.InicioManoDer && idx < FormatoLandmarks.InicioCara) return Tema.ManoDer;
            return Tema.Cuerpo * color;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (Cuadro == null) return;
            var r = GetPixelAdjustedRect();
            float g = ModoAvatar ? Mathf.Max(4f, r.width / 70f) : Grosor;

            if (ModoAvatar) DibujarCuerpoYCabeza(vh, r, g);

            var huesos = FormatoLandmarks.Huesos;
            for (int k = 0; k + 1 < huesos.Length; k += 2)
            {
                if (Posicion(huesos[k], r, out var a) && Posicion(huesos[k + 1], r, out var b))
                {
                    bool esMano = huesos[k] >= FormatoLandmarks.InicioManoIzq;
                    Linea(vh, a, b, esMano ? g * 0.7f : g, ColorDe(huesos[k]));
                }
            }
            for (int i = FormatoLandmarks.InicioManoIzq; i < FormatoLandmarks.InicioCara; i++)
                if (Posicion(i, r, out var p)) Circulo(vh, p, g * 0.55f, ColorDe(i), 6);

            var colorCara = ModoAvatar ? Tema.Texto : Tema.ConAlfa(Tema.Cuerpo, 0.7f);
            foreach (var trazo in new[] { cejaDer, cejaIzq, ojoDer, ojoIzq, bocaExt, bocaInt })
                Trazo(vh, r, trazo, g * 0.45f, colorCara);
        }

        void DibujarCuerpoYCabeza(VertexHelper vh, Rect r, float g)
        {
            var cuerpo = Tema.ConAlfa(Tema.SuperficieClara, 1f);
            bool hi = Posicion(FormatoLandmarks.IdxHombroIzq, r, out var a);
            bool hd = Posicion(FormatoLandmarks.IdxHombroDer, r, out var b);
            if (hi && hd)
            {
                // Torso: trapecio desde los hombros hasta el borde inferior
                var abajoA = new Vector2(a.x + (a.x - b.x) * 0.08f, r.yMin);
                var abajoB = new Vector2(b.x - (a.x - b.x) * 0.08f, r.yMin);
                Cuadrilatero(vh, a, b, abajoB, abajoA, cuerpo);
            }
            int idxNariz = FormatoLandmarks.IdxCaraReferencia;
            int idxMenton = FormatoLandmarks.InicioCara + Array.IndexOf(FormatoLandmarks.CaraIndicesMediapipe, 152);
            int idxFrente = FormatoLandmarks.InicioCara + Array.IndexOf(FormatoLandmarks.CaraIndicesMediapipe, 10);
            if (Posicion(idxNariz, r, out var nariz))
            {
                float radio = g * 9f;
                Vector2 centro = nariz;
                if (Posicion(idxMenton, r, out var menton) && Posicion(idxFrente, r, out var frente))
                {
                    centro = (menton + frente) / 2f;
                    radio = Vector2.Distance(menton, frente) * 0.62f;
                }
                if (hi && hd) Linea(vh, (a + b) / 2f, centro, radio * 0.7f, cuerpo);
                Circulo(vh, centro, radio, Tema.Hex(0x33414E), 28);
            }
            else if (Posicion(FormatoLandmarks.IdxNariz, r, out var narizPose))
            {
                Circulo(vh, narizPose, g * 9f, Tema.Hex(0x33414E), 28);
            }
        }

        void Trazo(VertexHelper vh, Rect r, int[] indices, float grosor, Color c)
        {
            for (int i = 0; i + 1 < indices.Length; i++)
                if (Posicion(indices[i], r, out var a) && Posicion(indices[i + 1], r, out var b)) Linea(vh, a, b, grosor, c);
        }

        static void Linea(VertexHelper vh, Vector2 a, Vector2 b, float grosor, Color c)
        {
            var d = b - a;
            if (d.sqrMagnitude < 1e-6f) return;
            var n = new Vector2(-d.y, d.x).normalized * (grosor / 2f);
            Cuadrilatero(vh, a - n, a + n, b + n, b - n, c);
        }

        static void Cuadrilatero(VertexHelper vh, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, Color c)
        {
            int i = vh.currentVertCount;
            Vertice(vh, p0, c); Vertice(vh, p1, c); Vertice(vh, p2, c); Vertice(vh, p3, c);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i + 2, i + 3, i);
        }

        static void Circulo(VertexHelper vh, Vector2 centro, float radio, Color c, int lados)
        {
            int i0 = vh.currentVertCount;
            Vertice(vh, centro, c);
            for (int k = 0; k <= lados; k++)
            {
                float ang = k * Mathf.PI * 2f / lados;
                Vertice(vh, centro + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * radio, c);
            }
            for (int k = 1; k <= lados; k++) vh.AddTriangle(i0, i0 + k, i0 + k + 1);
        }

        static void Vertice(VertexHelper vh, Vector2 p, Color c)
        {
            var v = UIVertex.simpleVert;
            v.position = p;
            v.color = c;
            vh.AddVert(v);
        }
    }
}
