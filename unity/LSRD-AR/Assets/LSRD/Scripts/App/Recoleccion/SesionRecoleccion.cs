using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using LSRD.Nucleo;
using UnityEngine;

namespace LSRD.App
{
    /// <summary>Perfil seudónimo de quien firma (sin nombre ni datos de contacto).</summary>
    public sealed class PerfilFirmante
    {
        public string Codigo = "";
        public string Region = "";
        public string ManoDominante = "";
        public string RangoEdad = "";
        public string Identidad = "";
        public string AniosLsrd = "";

        public Dictionary<string, string> ADiccionario() => new Dictionary<string, string>
        {
            { "region", Region }, { "mano_dominante", ManoDominante }, { "rango_edad", RangoEdad },
            { "identidad", Identidad }, { "anios_lsrd", AniosLsrd },
        };
    }

    /// <summary>
    /// Guía una sesión de grabación: muestra cada glosa, detecta la seña con el mismo segmentador
    /// del reconocimiento, deja revisarla y la guarda (solo puntos clave) para enviarla al servidor.
    /// </summary>
    public sealed class SesionRecoleccion
    {
        public enum Fase { Esperando, Revision, Terminada }

        public const string Fondo = "_FONDO";
        public const double DuracionFondoMs = 2500;

        public event Action AlCambio;
        public Fase FaseActual { get; private set; } = Fase.Esperando;
        public readonly List<string> Glosas;
        public readonly int Repeticiones;
        public int Indice { get; private set; }
        public int RepeticionActual { get; private set; }
        public int Guardadas { get; private set; }
        public MuestraLandmarks UltimaMuestra { get; private set; }
        public string GlosaActual => Indice < Glosas.Count ? Glosas[Indice] : null;
        public string Id { get; } = "S-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        public double ProgresoFondo { get; private set; }
        public bool HayManos { get; private set; }

        readonly PerfilFirmante perfil;
        readonly bool consentimientoAvatar;
        readonly Segmentador segmentador = new Segmentador();
        readonly BufferCuadros buffer = new BufferCuadros(450);
        readonly string carpetaPendientes;
        bool camaraFrontal;
        int anchoCamara, altoCamara;
        double inicioFondo = -1;

        public SesionRecoleccion(List<string> glosas, PerfilFirmante perfil, bool consentimientoAvatar, int repeticiones, string carpetaPendientes)
        {
            Glosas = glosas;
            this.perfil = perfil;
            this.consentimientoAvatar = consentimientoAvatar;
            Repeticiones = Math.Max(1, repeticiones);
            this.carpetaPendientes = carpetaPendientes;
            Directory.CreateDirectory(carpetaPendientes);
            if (Glosas.Count == 0) FaseActual = Fase.Terminada;
        }

        public void InformarCamara(bool frontal, int ancho, int alto)
        {
            camaraFrontal = frontal;
            anchoCamara = ancho;
            altoCamara = alto;
        }

        public void Procesar(CuadroLandmarks c)
        {
            if (FaseActual != Fase.Esperando) return;
            var anterior = buffer.Ultimo;
            double dt = anterior != null ? (c.TiempoMs - anterior.TiempoMs) / 1000.0 : 0.0;
            var previo = anterior?.Datos;
            var actual = buffer.Agregar(c.TiempoMs, c.Datos, c.Aspecto);
            bool activo = MedidorActividad.Medir(actual.Datos, previo, dt, segmentador.Parametros, out double vel);
            HayManos = actual.HayManos;

            if (GlosaActual == Fondo)
            {
                if (inicioFondo < 0) inicioFondo = c.TiempoMs;
                ProgresoFondo = Math.Min(1.0, (c.TiempoMs - inicioFondo) / DuracionFondoMs);
                if (c.TiempoMs - inicioFondo >= DuracionFondoMs)
                    Capturar(buffer.Recortar(inicioFondo, c.TiempoMs));
                return;
            }
            var seg = segmentador.Procesar(c.TiempoMs, activo, vel);
            if (seg.HasValue) Capturar(buffer.Recortar(seg.Value.InicioMs, seg.Value.FinMs));
        }

        void Capturar(List<CuadroLandmarks> cuadros)
        {
            if (cuadros.Count < 6) return;
            var m = MuestraLandmarks.DesdeCuadros(cuadros);
            m.Glosa = GlosaActual;
            m.Firmante = perfil.Codigo;
            m.Sesion = Id;
            m.Dispositivo = SystemInfo.deviceModel;
            m.CamaraFrontal = camaraFrontal;
            m.CamaraEspejo = false;   // la imagen procesada nunca está en espejo
            m.CamaraAncho = anchoCamara;
            m.CamaraAlto = altoCamara;
            m.ConsentimientoPuntos = true;
            m.ConsentimientoAvatar = consentimientoAvatar;
            m.Perfil = perfil.ADiccionario();
            UltimaMuestra = m;
            FaseActual = Fase.Revision;
            AlCambio?.Invoke();
        }

        public string Aceptar()
        {
            if (FaseActual != Fase.Revision || UltimaMuestra == null) return null;
            var nombre = $"{Archivo(UltimaMuestra.Glosa)}__{UltimaMuestra.Id}.json";
            var ruta = Path.Combine(carpetaPendientes, nombre);
            File.WriteAllText(ruta, UltimaMuestra.AJson());
            Guardadas++;
            RepeticionActual++;
            if (RepeticionActual >= Repeticiones) Avanzar();
            else Reanudar();
            return ruta;
        }

        public void Repetir() => Reanudar();

        public void Saltar() => Avanzar();

        void Avanzar()
        {
            Indice++;
            RepeticionActual = 0;
            if (Indice >= Glosas.Count) { FaseActual = Fase.Terminada; UltimaMuestra = null; AlCambio?.Invoke(); return; }
            Reanudar();
        }

        void Reanudar()
        {
            UltimaMuestra = null;
            segmentador.Reiniciar();
            buffer.Limpiar();
            inicioFondo = -1;
            ProgresoFondo = 0;
            FaseActual = Fase.Esperando;
            AlCambio?.Invoke();
        }

        public void Terminar()
        {
            FaseActual = Fase.Terminada;
            AlCambio?.Invoke();
        }

        static string Archivo(string glosa)
        {
            var chars = glosa.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (!(char.IsLetterOrDigit(chars[i]) || chars[i] == '-' || chars[i] == '_')) chars[i] = '_';
            return new string(chars);
        }
    }

    /// <summary>Envía al servidor las muestras guardadas en el teléfono, con reintentos.</summary>
    public sealed class ColaEnvio
    {
        public readonly string Pendientes;
        public readonly string Enviadas;
        public readonly string Rechazadas;
        public bool Enviando { get; private set; }

        /// <summary>Cómo se envía cada archivo; por defecto, como muestra de recolección.</summary>
        readonly Func<ClienteLSRD, string, Action<string>, IEnumerator> enviar;

        public ColaEnvio(string raiz, Func<ClienteLSRD, string, Action<string>, IEnumerator> enviar = null)
        {
            this.enviar = enviar ?? ((c, json, fin) => c.EnviarMuestra(json, fin));
            Pendientes = Path.Combine(raiz, "pendientes");
            Enviadas = Path.Combine(raiz, "enviadas");
            Rechazadas = Path.Combine(raiz, "rechazadas");
            Directory.CreateDirectory(Pendientes);
            Directory.CreateDirectory(Enviadas);
            Directory.CreateDirectory(Rechazadas);
        }

        public int CantidadPendiente => Directory.Exists(Pendientes) ? Directory.GetFiles(Pendientes, "*.json").Length : 0;
        public int CantidadEnviada => Directory.Exists(Enviadas) ? Directory.GetFiles(Enviadas, "*.json").Length : 0;

        public IEnumerator EnviarTodo(ClienteLSRD cliente, Action<int, string> fin)
        {
            if (Enviando) yield break;
            Enviando = true;
            int enviadas = 0;
            string ultimoError = null;
            foreach (var ruta in Directory.GetFiles(Pendientes, "*.json"))
            {
                string error = null;
                yield return enviar(cliente, File.ReadAllText(ruta), e => error = e);
                if (error == null)
                {
                    Mover(ruta, Enviadas);
                    enviadas++;
                }
                else if (cliente.UltimoCodigo == 422)
                {
                    Mover(ruta, Rechazadas);
                    ultimoError = error;
                }
                else
                {
                    ultimoError = error;
                    break;   // sin conexión: se reintenta más tarde
                }
            }
            Enviando = false;
            fin(enviadas, ultimoError);
        }

        static void Mover(string ruta, string carpeta)
        {
            var destino = Path.Combine(carpeta, Path.GetFileName(ruta));
            if (File.Exists(destino)) File.Delete(destino);
            File.Move(ruta, destino);
        }
    }
}
