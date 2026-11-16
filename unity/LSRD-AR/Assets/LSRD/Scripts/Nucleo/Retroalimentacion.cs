using System;
using System.Collections.Generic;
using System.Globalization;

namespace LSRD.Nucleo
{
    /// <summary>
    /// Reporte que la persona decide enviar cuando una traducción o una seña del
    /// avatar no es correcta. Solo lleva texto: nunca audio ni video. En el
    /// servidor queda pendiente de revisión lingüística; no cambia el vocabulario
    /// ni entrena modelos por sí mismo.
    /// </summary>
    public sealed class Reporte
    {
        public const string TraduccionIncorrecta = "traduccion_incorrecta";
        public const string SenaNoReconocida = "sena_no_reconocida";
        public const string AvatarIncorrecto = "avatar_incorrecto";
        public const string Otro = "otro";
        public const string SenasATexto = "senas_a_texto";
        public const string TextoASenas = "texto_a_senas";

        public static readonly string[] Tipos = { TraduccionIncorrecta, SenaNoReconocida, AvatarIncorrecto, Otro };
        public const int MaximoTexto = 500;

        public string Id = "";
        public string Tipo = TraduccionIncorrecta;
        public string Direccion = "";
        public string TextoMostrado = "";
        public List<string> Glosas = new List<string>();
        public string Correccion = "";
        public string Fuente = "";
        public string Modelo = "";
        public string VersionApp = "";
        public string FechaUtc = "";

        public static Reporte Nuevo(string tipo, string direccion, string textoMostrado, IEnumerable<string> glosas,
            string correccion, DateTime? ahoraUtc = null, string id = null)
        {
            if (Array.IndexOf(Tipos, tipo) < 0) throw new ArgumentException("Tipo de reporte desconocido: " + tipo);
            var r = new Reporte
            {
                Id = id ?? "rep-" + Guid.NewGuid().ToString("N"),
                Tipo = tipo,
                Direccion = direccion ?? "",
                TextoMostrado = Recortar(textoMostrado),
                Correccion = Recortar(correccion),
                FechaUtc = (ahoraUtc ?? DateTime.UtcNow).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            };
            if (glosas != null) r.Glosas.AddRange(glosas);
            if (r.Glosas.Count > 60) r.Glosas.RemoveRange(60, r.Glosas.Count - 60);
            return r;
        }

        /// <summary>Un reporte sin texto, sin glosas y sin corrección no dice nada: no se envía.</summary>
        public bool Vacio => TextoMostrado.Length == 0 && Correccion.Length == 0 && Glosas.Count == 0;

        static string Recortar(string s)
        {
            s = (s ?? "").Trim();
            return s.Length > MaximoTexto ? s.Substring(0, MaximoTexto) : s;
        }

        public string AJson() => Json.Escribir(new Dictionary<string, object>
        {
            ["id"] = Id,
            ["tipo"] = Tipo,
            ["direccion"] = Direccion,
            ["texto_mostrado"] = TextoMostrado,
            ["glosas"] = Glosas,
            ["correccion"] = Correccion,
            ["fuente"] = Fuente,
            ["modelo"] = Modelo.Length > 200 ? Modelo.Substring(0, 200) : Modelo,
            ["version_app"] = VersionApp,
            ["fecha_utc"] = FechaUtc,
        });

        public static string Rotulo(string tipo)
        {
            switch (tipo)
            {
                case TraduccionIncorrecta: return "La traducción no es correcta";
                case SenaNoReconocida: return "No reconoció mi seña";
                case AvatarIncorrecto: return "La seña del avatar no es correcta";
                default: return "Otro problema";
            }
        }
    }
}
