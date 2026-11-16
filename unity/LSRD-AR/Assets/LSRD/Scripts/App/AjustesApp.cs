using System.Globalization;
using UnityEngine;

namespace LSRD.App
{
    /// <summary>Ajustes de la app, guardados en el dispositivo (PlayerPrefs).</summary>
    public sealed class AjustesApp
    {
        public string UrlServidor = "http://192.168.1.10:8000";
        public string Clave = "";
        public bool CamaraFrontal = true;
        public bool EspejoPantalla = true;
        public int RotacionForzada = -1;          // -1 = automática
        public bool MostrarEsqueleto = true;
        public float UmbralConfianza = 0.6f;
        public float UmbralMinimo = 0.35f;
        public float PausaFraseS = 1.5f;
        public float VelocidadAvatar = 1f;
        public bool EscuchaAutomatica;
        public float UmbralVoz = 0.02f;
        public bool HablarTraduccion = true;
        public bool Vibrar = true;
        public bool UsarGpuMediaPipe;
        public bool DetectorSimulado;
        public string CodigoFirmante = "";

        const string Prefijo = "lsrd.";

        public static AjustesApp Cargar()
        {
            var a = new AjustesApp();
            a.UrlServidor = PlayerPrefs.GetString(Prefijo + "url", a.UrlServidor);
            a.Clave = PlayerPrefs.GetString(Prefijo + "clave", a.Clave);
            a.CamaraFrontal = Bool("frontal", a.CamaraFrontal);
            a.EspejoPantalla = Bool("espejo", a.EspejoPantalla);
            a.RotacionForzada = PlayerPrefs.GetInt(Prefijo + "rotacion", a.RotacionForzada);
            a.MostrarEsqueleto = Bool("esqueleto", a.MostrarEsqueleto);
            a.UmbralConfianza = PlayerPrefs.GetFloat(Prefijo + "umbral", a.UmbralConfianza);
            a.UmbralMinimo = PlayerPrefs.GetFloat(Prefijo + "umbral_min", a.UmbralMinimo);
            a.PausaFraseS = PlayerPrefs.GetFloat(Prefijo + "pausa", a.PausaFraseS);
            a.VelocidadAvatar = PlayerPrefs.GetFloat(Prefijo + "velocidad", a.VelocidadAvatar);
            a.EscuchaAutomatica = Bool("escucha", a.EscuchaAutomatica);
            a.UmbralVoz = PlayerPrefs.GetFloat(Prefijo + "umbral_voz", a.UmbralVoz);
            a.HablarTraduccion = Bool("hablar", a.HablarTraduccion);
            a.Vibrar = Bool("vibrar", a.Vibrar);
            a.UsarGpuMediaPipe = Bool("gpu", a.UsarGpuMediaPipe);
            a.DetectorSimulado = Bool("simulado", a.DetectorSimulado);
            a.CodigoFirmante = PlayerPrefs.GetString(Prefijo + "firmante", a.CodigoFirmante);
            return a;
        }

        public void Guardar()
        {
            PlayerPrefs.SetString(Prefijo + "url", UrlServidor.Trim().TrimEnd('/'));
            PlayerPrefs.SetString(Prefijo + "clave", Clave);
            SetBool("frontal", CamaraFrontal);
            SetBool("espejo", EspejoPantalla);
            PlayerPrefs.SetInt(Prefijo + "rotacion", RotacionForzada);
            SetBool("esqueleto", MostrarEsqueleto);
            PlayerPrefs.SetFloat(Prefijo + "umbral", UmbralConfianza);
            PlayerPrefs.SetFloat(Prefijo + "umbral_min", UmbralMinimo);
            PlayerPrefs.SetFloat(Prefijo + "pausa", PausaFraseS);
            PlayerPrefs.SetFloat(Prefijo + "velocidad", VelocidadAvatar);
            SetBool("escucha", EscuchaAutomatica);
            PlayerPrefs.SetFloat(Prefijo + "umbral_voz", UmbralVoz);
            SetBool("hablar", HablarTraduccion);
            SetBool("vibrar", Vibrar);
            SetBool("gpu", UsarGpuMediaPipe);
            SetBool("simulado", DetectorSimulado);
            PlayerPrefs.SetString(Prefijo + "firmante", CodigoFirmante);
            PlayerPrefs.Save();
        }

        static bool Bool(string clave, bool defecto) => PlayerPrefs.GetInt(Prefijo + clave, defecto ? 1 : 0) == 1;
        static void SetBool(string clave, bool valor) => PlayerPrefs.SetInt(Prefijo + clave, valor ? 1 : 0);

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "servidor={0} frontal={1} rotacion={2} simulado={3}",
                UrlServidor, CamaraFrontal, RotacionForzada, DetectorSimulado);
    }
}
