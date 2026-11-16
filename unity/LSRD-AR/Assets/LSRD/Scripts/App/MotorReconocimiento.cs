using System;
using System.Collections.Generic;
using LSRD.Nucleo;
using UnityEngine;

namespace LSRD.App
{
    /// <summary>
    /// Une segmentación, modelos y armado de frases: recibe cuadros de puntos clave y produce
    /// señas reconocidas, letras deletreadas y frases completas.
    /// </summary>
    public sealed class MotorReconocimiento : IDisposable
    {
        public event Action<Prediccion, SenaReconocida> AlSena;
        public event Action<string> AlLetra;
        public event Action<Frase> AlFrase;
        public event Action AlCambio;

        public readonly BufferCuadros Buffer = new BufferCuadros(360);
        public readonly Segmentador Segmentador;
        public readonly ConstructorFrase Frases = new ConstructorFrase();
        public readonly VotacionDactilologia Votacion = new VotacionDactilologia();
        public bool ModoDeletreo;
        public bool Pausado;
        public CuadroLandmarks UltimoCuadro => Buffer.Ultimo;
        public bool HayManos { get; private set; }
        public bool SenaEnCurso => Segmentador.EnSena;
        public string EstadoModelos { get; private set; } = "";
        public bool ModelosDemostracion { get; private set; }

        ClasificadorOnnx senas, dactilologia;
        readonly float[] mano = new float[FormatoLandmarks.PuntosMano * 3];
        int contadorCuadros;
        double inicioPalabra = double.NaN;

        public MotorReconocimiento(AjustesApp ajustes)
        {
            Segmentador = new Segmentador(new ParametrosSegmentacion());
            Configurar(ajustes);
        }

        public void Configurar(AjustesApp ajustes)
        {
            Frases.UmbralConfianza = ajustes.UmbralConfianza;
            Frases.UmbralMinimo = ajustes.UmbralMinimo;
            Frases.PausaFraseMs = ajustes.PausaFraseS * 1000.0;
        }

        public void CargarModelos()
        {
            senas?.Dispose();
            dactilologia?.Dispose();
            var mensajes = new List<string>();
            senas = ClasificadorOnnx.Cargar("senas",
                new[] { 1, FormatoLandmarks.LongitudSecuencia, FormatoLandmarks.NPuntos, FormatoLandmarks.NCoordenadas }, out var e1);
            dactilologia = ClasificadorOnnx.Cargar("dactilologia", new[] { 1, FormatoLandmarks.PuntosMano, 3 }, out var e2);
            if (senas != null) mensajes.Add($"Señas: {senas.Info.Clases.Count} clases");
            else mensajes.Add("Señas: " + e1);
            if (dactilologia != null) mensajes.Add($"Letras: {dactilologia.Info.Clases.Count}");
            else mensajes.Add("Letras: " + e2);
            ModelosDemostracion = (senas != null && senas.Info.EsDemostracion) || (dactilologia != null && dactilologia.Info.EsDemostracion);
            if (ModelosDemostracion) mensajes.Add("MODELO DE DEMOSTRACIÓN (no reconoce LSRD)");
            EstadoModelos = string.Join(" · ", mensajes);
        }

        public bool PuedeReconocerSenas => senas != null;
        public bool PuedeDeletrear => dactilologia != null;

        public void Reiniciar()
        {
            Buffer.Limpiar();
            Segmentador.Reiniciar();
            Frases.Limpiar();
            Votacion.Cerrar();
            inicioPalabra = double.NaN;
        }

        public void Procesar(CuadroLandmarks c)
        {
            if (Pausado) return;
            var anterior = Buffer.Ultimo;
            double dt = anterior != null ? (c.TiempoMs - anterior.TiempoMs) / 1000.0 : 0.0;
            var previo = anterior?.Datos;
            var actual = Buffer.Agregar(c.TiempoMs, c.Datos, c.Aspecto);
            bool activo = MedidorActividad.Medir(actual.Datos, previo, dt, Segmentador.Parametros, out double velocidad);
            HayManos = actual.HayManos;
            contadorCuadros++;

            if (ModoDeletreo) ProcesarDeletreo(actual);
            else ProcesarSenas(actual.TiempoMs, activo, velocidad);

            var frase = Frases.Actualizar(actual.TiempoMs, Segmentador.EnSena || Votacion.PalabraEnCurso.Length > 0);
            if (frase != null) AlFrase?.Invoke(frase);
        }

        void ProcesarSenas(double t, bool activo, double velocidad)
        {
            var seg = Segmentador.Procesar(t, activo, velocidad);
            if (seg.HasValue) Clasificar(seg.Value);
        }

        void Clasificar(Segmento seg)
        {
            if (senas == null) return;
            var cuadros = Buffer.Recortar(seg.InicioMs, seg.FinMs);
            if (cuadros.Count < 4) return;
            var datos = new List<float[]>(cuadros.Count);
            foreach (var c in cuadros) datos.Add(c.Datos);
            float[] probs;
            try
            {
                probs = senas.Predecir(Remuestreo.Aplanar(datos));
            }
            catch (Exception e)
            {
                Debug.LogError("[LSRD] Error al clasificar: " + e);
                return;
            }
            var p = Prediccion.DesdeProbabilidades(probs, senas.Info.Clases, 3);
            p.InicioMs = seg.InicioMs;
            p.FinMs = seg.FinMs;
            var aceptada = Frases.Agregar(p);
            AlSena?.Invoke(p, aceptada);
            AlCambio?.Invoke();
        }

        void ProcesarDeletreo(CuadroLandmarks c)
        {
            if (dactilologia == null) return;
            string glosa = null;
            float prob = 0f;
            if (EmpaquetadorLandmarks.ManoActiva(c.Datos, mano))
            {
                // Se clasifica uno de cada dos cuadros para ahorrar batería.
                if (contadorCuadros % 2 != 0) return;
                try
                {
                    var probs = dactilologia.Predecir(mano);
                    var p = Prediccion.DesdeProbabilidades(probs, dactilologia.Info.Clases, 1);
                    glosa = p.Mejor.Glosa;
                    prob = p.Mejor.Probabilidad;
                }
                catch (Exception e)
                {
                    Debug.LogError("[LSRD] Error en dactilología: " + e);
                    return;
                }
            }
            var letra = Votacion.Procesar(c.TiempoMs, glosa, prob, out var palabra);
            if (letra != null)
            {
                if (double.IsNaN(inicioPalabra)) inicioPalabra = c.TiempoMs;
                AlLetra?.Invoke(letra);
                AlCambio?.Invoke();
            }
            if (palabra != null)
            {
                Frases.AgregarDeletreo(palabra, double.IsNaN(inicioPalabra) ? c.TiempoMs : inicioPalabra, c.TiempoMs);
                inicioPalabra = double.NaN;
                AlCambio?.Invoke();
            }
        }

        /// <summary>Cierra la palabra o frase en curso (botón "Enviar").</summary>
        public Frase Forzar()
        {
            var palabra = Votacion.Cerrar();
            if (palabra != null && UltimoCuadro != null)
                Frases.AgregarDeletreo(palabra, double.IsNaN(inicioPalabra) ? UltimoCuadro.TiempoMs : inicioPalabra, UltimoCuadro.TiempoMs);
            inicioPalabra = double.NaN;
            var seg = Segmentador.Cerrar();
            if (seg.HasValue) Clasificar(seg.Value);
            return Frases.Forzar();
        }

        public void Dispose()
        {
            senas?.Dispose();
            dactilologia?.Dispose();
        }
    }
}
