using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace LSRD.Nucleo
{
    /// <summary>
    /// Lector y escritor JSON mínimo, sin dependencias. Objetos → Dictionary&lt;string, object&gt;,
    /// listas → List&lt;object&gt;, números → double. Siempre con cultura invariante (los teléfonos
    /// configurados en español usan coma decimal).
    /// </summary>
    public static class Json
    {
        public static object Leer(string texto)
        {
            if (texto == null) throw new ArgumentNullException(nameof(texto));
            var l = new Lector(texto);
            l.Espacios();
            var v = l.Valor();
            l.Espacios();
            if (!l.Fin) throw new FormatException($"JSON con contenido extra en la posición {l.Posicion}");
            return v;
        }

        public static Dictionary<string, object> Objeto(string texto) =>
            Leer(texto) as Dictionary<string, object> ?? throw new FormatException("Se esperaba un objeto JSON");

        // ------------------------------------------------------------ acceso cómodo

        public static Dictionary<string, object> Obj(Dictionary<string, object> d, string clave) =>
            d != null && d.TryGetValue(clave, out var v) ? v as Dictionary<string, object> : null;

        public static List<object> Lista(Dictionary<string, object> d, string clave) =>
            d != null && d.TryGetValue(clave, out var v) ? v as List<object> : null;

        public static string Cad(Dictionary<string, object> d, string clave, string defecto = null) =>
            d != null && d.TryGetValue(clave, out var v) && v is string s ? s : defecto;

        public static double Num(Dictionary<string, object> d, string clave, double defecto = 0) =>
            d != null && d.TryGetValue(clave, out var v) && v is double n ? n : defecto;

        public static bool Bool(Dictionary<string, object> d, string clave, bool defecto = false) =>
            d != null && d.TryGetValue(clave, out var v) && v is bool b ? b : defecto;

        public static List<string> Cadenas(Dictionary<string, object> d, string clave)
        {
            var salida = new List<string>();
            var l = Lista(d, clave);
            if (l == null) return salida;
            foreach (var x in l) if (x is string s) salida.Add(s);
            return salida;
        }

        // ------------------------------------------------------------ escritura

        public static string Cadena(string s)
        {
            if (s == null) return "null";
            var sb = new StringBuilder(s.Length + 2);
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        public static string Numero(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "null";
            return v.ToString("R", CultureInfo.InvariantCulture);
        }

        public static string Escribir(object v)
        {
            var sb = new StringBuilder();
            EscribirEn(sb, v);
            return sb.ToString();
        }

        static void EscribirEn(StringBuilder sb, object v)
        {
            switch (v)
            {
                case null: sb.Append("null"); break;
                case string s: sb.Append(Cadena(s)); break;
                case bool b: sb.Append(b ? "true" : "false"); break;
                case double d: sb.Append(Numero(d)); break;
                case float f: sb.Append(Numero(f)); break;
                case int i: sb.Append(i.ToString(CultureInfo.InvariantCulture)); break;
                case long l: sb.Append(l.ToString(CultureInfo.InvariantCulture)); break;
                case IDictionary<string, object> d:
                    sb.Append('{');
                    bool primero = true;
                    foreach (var kv in d)
                    {
                        if (!primero) sb.Append(',');
                        primero = false;
                        sb.Append(Cadena(kv.Key)).Append(':');
                        EscribirEn(sb, kv.Value);
                    }
                    sb.Append('}');
                    break;
                case System.Collections.IEnumerable e:
                    sb.Append('[');
                    bool p = true;
                    foreach (var x in e)
                    {
                        if (!p) sb.Append(',');
                        p = false;
                        EscribirEn(sb, x);
                    }
                    sb.Append(']');
                    break;
                default:
                    sb.Append(Cadena(Convert.ToString(v, CultureInfo.InvariantCulture)));
                    break;
            }
        }

        sealed class Lector
        {
            readonly string t;
            int i;
            public Lector(string texto) { t = texto; }
            public bool Fin => i >= t.Length;
            public int Posicion => i;

            public void Espacios()
            {
                while (i < t.Length && char.IsWhiteSpace(t[i])) i++;
            }

            public object Valor()
            {
                if (Fin) throw new FormatException("JSON incompleto");
                char c = t[i];
                switch (c)
                {
                    case '{': return LeerObjeto();
                    case '[': return LeerLista();
                    case '"': return LeerCadena();
                    case 't': Esperar("true"); return true;
                    case 'f': Esperar("false"); return false;
                    case 'n': Esperar("null"); return null;
                    default:
                        if (c == '-' || char.IsDigit(c)) return LeerNumero();
                        throw new FormatException($"Carácter inesperado '{c}' en la posición {i}");
                }
            }

            void Esperar(string palabra)
            {
                if (string.CompareOrdinal(t, i, palabra, 0, palabra.Length) != 0)
                    throw new FormatException($"Se esperaba '{palabra}' en la posición {i}");
                i += palabra.Length;
            }

            Dictionary<string, object> LeerObjeto()
            {
                var d = new Dictionary<string, object>();
                i++;
                Espacios();
                if (i < t.Length && t[i] == '}') { i++; return d; }
                while (true)
                {
                    Espacios();
                    if (Fin || t[i] != '"') throw new FormatException($"Se esperaba una clave en la posición {i}");
                    var clave = LeerCadena();
                    Espacios();
                    if (Fin || t[i] != ':') throw new FormatException($"Se esperaba ':' en la posición {i}");
                    i++;
                    Espacios();
                    d[clave] = Valor();
                    Espacios();
                    if (Fin) throw new FormatException("Objeto sin cerrar");
                    if (t[i] == ',') { i++; continue; }
                    if (t[i] == '}') { i++; return d; }
                    throw new FormatException($"Se esperaba ',' o '}}' en la posición {i}");
                }
            }

            List<object> LeerLista()
            {
                var l = new List<object>();
                i++;
                Espacios();
                if (i < t.Length && t[i] == ']') { i++; return l; }
                while (true)
                {
                    Espacios();
                    l.Add(Valor());
                    Espacios();
                    if (Fin) throw new FormatException("Lista sin cerrar");
                    if (t[i] == ',') { i++; continue; }
                    if (t[i] == ']') { i++; return l; }
                    throw new FormatException($"Se esperaba ',' o ']' en la posición {i}");
                }
            }

            string LeerCadena()
            {
                var sb = new StringBuilder();
                i++;
                while (true)
                {
                    if (Fin) throw new FormatException("Cadena sin cerrar");
                    char c = t[i++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }
                    if (Fin) throw new FormatException("Escape incompleto");
                    char e = t[i++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 4 > t.Length) throw new FormatException("Escape \\u incompleto");
                            sb.Append((char)int.Parse(t.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            i += 4;
                            break;
                        default: throw new FormatException($"Escape inválido '\\{e}'");
                    }
                }
            }

            double LeerNumero()
            {
                int ini = i;
                if (t[i] == '-') i++;
                while (i < t.Length && (char.IsDigit(t[i]) || t[i] == '.' || t[i] == 'e' || t[i] == 'E' || t[i] == '+' || t[i] == '-')) i++;
                return double.Parse(t.Substring(ini, i - ini), NumberStyles.Float, CultureInfo.InvariantCulture);
            }
        }
    }

    /// <summary>Arreglos de float32 little-endian en base64 (mismo formato que Python).</summary>
    public static class Base64Floats
    {
        public static string Codificar(float[] valores, int inicio = 0, int cantidad = -1)
        {
            if (cantidad < 0) cantidad = valores.Length - inicio;
            var bytes = new byte[cantidad * 4];
            Buffer.BlockCopy(valores, inicio * 4, bytes, 0, bytes.Length);
            if (!BitConverter.IsLittleEndian) InvertirCada4(bytes);
            return Convert.ToBase64String(bytes);
        }

        public static float[] Decodificar(string texto)
        {
            var bytes = Convert.FromBase64String(texto);
            if (bytes.Length % 4 != 0) throw new FormatException("La longitud no es múltiplo de 4");
            if (!BitConverter.IsLittleEndian) InvertirCada4(bytes);
            var salida = new float[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, salida, 0, bytes.Length);
            return salida;
        }

        static void InvertirCada4(byte[] b)
        {
            for (int k = 0; k + 3 < b.Length; k += 4)
            {
                byte x = b[k]; b[k] = b[k + 3]; b[k + 3] = x;
                x = b[k + 1]; b[k + 1] = b[k + 2]; b[k + 2] = x;
            }
        }
    }
}
