using System;
using System.Collections.Generic;

namespace LSRD.Nucleo
{
    /// <summary>Un cuadro de puntos clave con su marca de tiempo (ms, monótona).</summary>
    public sealed class CuadroLandmarks
    {
        public double TiempoMs;
        public double Aspecto;
        public readonly float[] Datos = new float[FormatoLandmarks.ValoresPorCuadro];

        public bool HayManos =>
            MedidorActividad.Presente(Datos, FormatoLandmarks.IdxMunecaIzq) ||
            MedidorActividad.Presente(Datos, FormatoLandmarks.IdxMunecaDer);

        public CuadroLandmarks Clonar()
        {
            var c = new CuadroLandmarks { TiempoMs = TiempoMs, Aspecto = Aspecto };
            Array.Copy(Datos, c.Datos, Datos.Length);
            return c;
        }
    }

    /// <summary>Historial circular de los últimos cuadros, para recortar señas ya terminadas.</summary>
    public sealed class BufferCuadros
    {
        readonly CuadroLandmarks[] anillo;
        int inicio, cantidad;

        public BufferCuadros(int capacidad = 360)
        {
            anillo = new CuadroLandmarks[capacidad];
            for (int i = 0; i < capacidad; i++) anillo[i] = new CuadroLandmarks();
        }

        public int Cantidad => cantidad;
        public int Capacidad => anillo.Length;

        public CuadroLandmarks this[int i] => anillo[(inicio + i) % anillo.Length];

        public CuadroLandmarks Ultimo => cantidad == 0 ? null : this[cantidad - 1];
        public CuadroLandmarks Penultimo => cantidad < 2 ? null : this[cantidad - 2];

        public void Limpiar() { inicio = 0; cantidad = 0; }

        public CuadroLandmarks Agregar(double tiempoMs, float[] datos, double aspecto)
        {
            CuadroLandmarks destino;
            if (cantidad < anillo.Length)
            {
                destino = anillo[(inicio + cantidad) % anillo.Length];
                cantidad++;
            }
            else
            {
                destino = anillo[inicio];
                inicio = (inicio + 1) % anillo.Length;
            }
            destino.TiempoMs = tiempoMs;
            destino.Aspecto = aspecto;
            Array.Copy(datos, destino.Datos, destino.Datos.Length);
            return destino;
        }

        /// <summary>Copias de los cuadros con tiempo en [ini − margen, fin + margen].</summary>
        public List<CuadroLandmarks> Recortar(double iniMs, double finMs, double margenMs = 0.0)
        {
            var salida = new List<CuadroLandmarks>();
            for (int i = 0; i < cantidad; i++)
            {
                var c = this[i];
                if (c.TiempoMs >= iniMs - margenMs && c.TiempoMs <= finMs + margenMs) salida.Add(c.Clonar());
            }
            return salida;
        }
    }
}
