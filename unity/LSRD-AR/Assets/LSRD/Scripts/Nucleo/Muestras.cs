using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace LSRD.Nucleo
{
    /// <summary>
    /// Una grabación de una seña en formato LSRD-LM v1 (solo puntos clave, sin video).
    /// El JSON es el mismo que lee ml/lsrd_ml/datos.py y que valida el servidor.
    /// </summary>
    public sealed class MuestraLandmarks
    {
        public string Id = Guid.NewGuid().ToString("N");
        public string Glosa = "";
        public string Firmante = "";
        public string Sesion = "";
        public string FechaUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        public string Dispositivo = "";
        public string Revision = "aceptada";
        public bool CamaraFrontal;
        public bool CamaraEspejo;
        public int CamaraAncho, CamaraAlto;
        public bool ConsentimientoPuntos;
        public bool ConsentimientoAvatar;
        public string VersionConsentimiento = "1.0";
        public Dictionary<string, string> Perfil = new Dictionary<string, string>();
        public readonly List<double> TiemposMs = new List<double>();
        public readonly List<float[]> Cuadros = new List<float[]>();

        public int NumeroCuadros => Cuadros.Count;
        public double DuracionMs => TiemposMs.Count < 2 ? 0 : TiemposMs[TiemposMs.Count - 1] - TiemposMs[0];

        public static MuestraLandmarks DesdeCuadros(IList<CuadroLandmarks> cuadros)
        {
            var m = new MuestraLandmarks();
            if (cuadros.Count == 0) return m;
            double t0 = cuadros[0].TiempoMs;
            foreach (var c in cuadros)
            {
                m.TiemposMs.Add(c.TiempoMs - t0);
                m.Cuadros.Add((float[])c.Datos.Clone());
            }
            return m;
        }

        public string AJson()
        {
            int v = FormatoLandmarks.ValoresPorCuadro;
            var plano = new float[Cuadros.Count * v];
            for (int i = 0; i < Cuadros.Count; i++) Array.Copy(Cuadros[i], 0, plano, i * v, v);
            var sb = new StringBuilder(plano.Length * 6 + 1024);
            sb.Append('{');
            Par(sb, "formato", Json.Cadena(FormatoLandmarks.Version)); sb.Append(',');
            Par(sb, "tipo", Json.Cadena("muestra")); sb.Append(',');
            Par(sb, "id", Json.Cadena(Id)); sb.Append(',');
            Par(sb, "glosa", Json.Cadena(Glosa)); sb.Append(',');
            Par(sb, "firmante", Json.Cadena(Firmante)); sb.Append(',');
            Par(sb, "sesion", Json.Cadena(Sesion)); sb.Append(',');
            Par(sb, "fecha_utc", Json.Cadena(FechaUtc)); sb.Append(',');
            Par(sb, "dispositivo", Json.Cadena(Dispositivo)); sb.Append(',');
            Par(sb, "revision", Json.Cadena(Revision)); sb.Append(',');
            Par(sb, "camara", Json.Escribir(new Dictionary<string, object>
            {
                { "frontal", CamaraFrontal }, { "espejo", CamaraEspejo }, { "ancho", CamaraAncho }, { "alto", CamaraAlto },
            })); sb.Append(',');
            Par(sb, "consentimiento", Json.Escribir(new Dictionary<string, object>
            {
                { "puntos_clave", ConsentimientoPuntos }, { "avatar", ConsentimientoAvatar }, { "version", VersionConsentimiento },
            })); sb.Append(',');
            Par(sb, "perfil", Json.Escribir(Perfil.ToDictionary(kv => kv.Key, kv => (object)kv.Value))); sb.Append(',');
            Par(sb, "forma", $"[{Cuadros.Count},{FormatoLandmarks.NPuntos},{FormatoLandmarks.NCoordenadas}]"); sb.Append(',');
            Par(sb, "tiempos_ms", "[" + string.Join(",", TiemposMs.Select(t => Json.Numero(Math.Round(t, 3)))) + "]"); sb.Append(',');
            Par(sb, "cuadros_b64", Json.Cadena(Base64Floats.Codificar(plano)));
            sb.Append('}');
            return sb.ToString();
        }

        static void Par(StringBuilder sb, string clave, string valorJson) => sb.Append(Json.Cadena(clave)).Append(':').Append(valorJson);

        public static MuestraLandmarks DesdeJson(string texto)
        {
            var d = Json.Objeto(texto);
            if (Json.Cad(d, "formato") != FormatoLandmarks.Version)
                throw new FormatException($"Formato {Json.Cad(d, "formato")}; se esperaba {FormatoLandmarks.Version}");
            var forma = Json.Lista(d, "forma");
            if (forma == null || forma.Count != 3) throw new FormatException("Falta 'forma'");
            int n = (int)(double)forma[0];
            if ((int)(double)forma[1] != FormatoLandmarks.NPuntos || (int)(double)forma[2] != FormatoLandmarks.NCoordenadas)
                throw new FormatException("Forma incompatible con " + FormatoLandmarks.Version);
            var plano = Base64Floats.Decodificar(Json.Cad(d, "cuadros_b64", ""));
            int v = FormatoLandmarks.ValoresPorCuadro;
            if (plano.Length != n * v) throw new FormatException("cuadros_b64 no coincide con la forma");
            var m = new MuestraLandmarks
            {
                Id = Json.Cad(d, "id", Guid.NewGuid().ToString("N")),
                Glosa = Json.Cad(d, "glosa", ""),
                Firmante = Json.Cad(d, "firmante", ""),
                Sesion = Json.Cad(d, "sesion", ""),
                FechaUtc = Json.Cad(d, "fecha_utc", ""),
                Dispositivo = Json.Cad(d, "dispositivo", ""),
                Revision = Json.Cad(d, "revision", "aceptada"),
            };
            var cam = Json.Obj(d, "camara");
            m.CamaraFrontal = Json.Bool(cam, "frontal");
            m.CamaraEspejo = Json.Bool(cam, "espejo");
            m.CamaraAncho = (int)Json.Num(cam, "ancho");
            m.CamaraAlto = (int)Json.Num(cam, "alto");
            var con = Json.Obj(d, "consentimiento");
            m.ConsentimientoPuntos = Json.Bool(con, "puntos_clave");
            m.ConsentimientoAvatar = Json.Bool(con, "avatar");
            m.VersionConsentimiento = Json.Cad(con, "version", "");
            var perfil = Json.Obj(d, "perfil");
            if (perfil != null) foreach (var kv in perfil) m.Perfil[kv.Key] = kv.Value as string ?? Convert.ToString(kv.Value, CultureInfo.InvariantCulture);
            var tiempos = Json.Lista(d, "tiempos_ms");
            if (tiempos == null || tiempos.Count != n) throw new FormatException("tiempos_ms no coincide con la forma");
            foreach (var t in tiempos) m.TiemposMs.Add((double)t);
            for (int i = 0; i < n; i++)
            {
                var c = new float[v];
                Array.Copy(plano, i * v, c, 0, v);
                m.Cuadros.Add(c);
            }
            return m;
        }
    }

    /// <summary>Metadatos de un modelo exportado (archivo *_info.json junto al .onnx).</summary>
    public sealed class InfoModelo
    {
        public string Modelo = "";
        public string Formato = "";
        public List<string> Clases = new List<string>();
        public int LongitudSecuencia = FormatoLandmarks.LongitudSecuencia;
        public string ClaseFondo;
        public string Nota = "";
        public double Exactitud = double.NaN;

        public bool EsDemostracion => Nota.IndexOf("DEMOSTRACIÓN", StringComparison.OrdinalIgnoreCase) >= 0;
        /// <summary>Entrenado con pocas personas (videos): sin validar con firmantes nuevos.</summary>
        public bool EsSemilla => Nota.IndexOf("SEMILLA", StringComparison.OrdinalIgnoreCase) >= 0;

        public static InfoModelo DesdeJson(string texto)
        {
            var d = Json.Objeto(texto);
            var i = new InfoModelo
            {
                Modelo = Json.Cad(d, "modelo", ""),
                Formato = Json.Cad(d, "formato", ""),
                Clases = Json.Cadenas(d, "clases"),
                LongitudSecuencia = (int)Json.Num(d, "longitud_secuencia", FormatoLandmarks.LongitudSecuencia),
                ClaseFondo = Json.Cad(d, "clase_fondo"),
                Nota = Json.Cad(d, "nota", ""),
                Exactitud = Json.Num(Json.Obj(d, "metricas"), "exactitud", double.NaN),
            };
            if (i.Formato != FormatoLandmarks.Version)
                throw new FormatException($"El modelo usa {i.Formato}; la app usa {FormatoLandmarks.Version}");
            if (i.Clases.Count == 0) throw new FormatException("El modelo no declara clases");
            return i;
        }
    }
}
