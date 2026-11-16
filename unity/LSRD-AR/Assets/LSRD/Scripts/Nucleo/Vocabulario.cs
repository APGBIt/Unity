using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LSRD.Nucleo
{
    /// <summary>Normalización de texto en español (misma lógica que servidor/app/vocabulario.py).</summary>
    public static class TextoEspanol
    {
        const string Puntuacion = "¿?¡!.,;:\"“”«»()[]…";

        public static string Normalizar(string texto)
        {
            if (string.IsNullOrEmpty(texto)) return "";
            var sb = new StringBuilder(texto.Length);
            foreach (char c0 in texto)
            {
                char c = char.ToLowerInvariant(c0);
                sb.Append(Puntuacion.IndexOf(c) >= 0 ? ' ' : c);
            }
            return string.Join(" ", sb.ToString().Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries));
        }

        /// <summary>Quita tildes y diéresis; conserva la ñ.</summary>
        public static string SinTildes(string texto)
        {
            var sb = new StringBuilder(texto.Length);
            foreach (char c in texto)
            {
                switch (c)
                {
                    case 'á': case 'à': case 'â': case 'ä': sb.Append('a'); break;
                    case 'é': case 'è': case 'ê': case 'ë': sb.Append('e'); break;
                    case 'í': case 'ì': case 'î': case 'ï': sb.Append('i'); break;
                    case 'ó': case 'ò': case 'ô': case 'ö': sb.Append('o'); break;
                    case 'ú': case 'ù': case 'û': case 'ü': sb.Append('u'); break;
                    case 'Á': case 'À': case 'Â': case 'Ä': sb.Append('A'); break;
                    case 'É': case 'È': case 'Ê': case 'Ë': sb.Append('E'); break;
                    case 'Í': case 'Ì': case 'Î': case 'Ï': sb.Append('I'); break;
                    case 'Ó': case 'Ò': case 'Ô': case 'Ö': sb.Append('O'); break;
                    case 'Ú': case 'Ù': case 'Û': case 'Ü': sb.Append('U'); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        public static readonly HashSet<string> Preguntas = new HashSet<string>
        {
            "QUE", "DONDE", "CUANDO", "CUANTO", "QUIEN", "COMO", "POR-QUE", "CUAL",
        };

        /// <summary>Palabras que la traducción literal omite (idéntico a FUNCIONALES en Python).</summary>
        public static readonly HashSet<string> Funcionales = new HashSet<string>
        {
            "el", "la", "los", "las", "un", "una", "unos", "unas", "lo", "de", "del", "al", "a", "en", "y", "e", "o", "u",
            "que", "se", "le", "les", "me", "te", "nos", "su", "sus", "mi", "mis", "tu", "tus", "por", "para", "con", "sin",
            "es", "son", "era", "fue", "está", "están", "esta", "este", "estos", "estas", "ese", "esa", "eso", "muy", "como",
            "si", "ya", "pero", "porque", "cuando", "donde", "hay", "ser", "estar", "ha", "he", "han", "hemos", "va", "tengo que",
            "tiene que", "debe", "debo", "deben", "usted", "ustedes",
        };
    }

    /// <summary>Lector CSV con soporte de comillas (RFC 4180).</summary>
    public static class Csv
    {
        public static List<Dictionary<string, string>> Leer(string texto)
        {
            var filas = new List<List<string>>();
            var fila = new List<string>();
            var campo = new StringBuilder();
            bool comillas = false;
            for (int i = 0; i < texto.Length; i++)
            {
                char c = texto[i];
                if (comillas)
                {
                    if (c == '"')
                    {
                        if (i + 1 < texto.Length && texto[i + 1] == '"') { campo.Append('"'); i++; }
                        else comillas = false;
                    }
                    else campo.Append(c);
                    continue;
                }
                switch (c)
                {
                    case '"': comillas = true; break;
                    case ',': fila.Add(campo.ToString()); campo.Clear(); break;
                    case '\r': break;
                    case '\n':
                        fila.Add(campo.ToString()); campo.Clear();
                        filas.Add(fila); fila = new List<string>();
                        break;
                    case '﻿': break;
                    default: campo.Append(c); break;
                }
            }
            if (campo.Length > 0 || fila.Count > 0) { fila.Add(campo.ToString()); filas.Add(fila); }

            var salida = new List<Dictionary<string, string>>();
            if (filas.Count == 0) return salida;
            var encabezado = filas[0];
            for (int r = 1; r < filas.Count; r++)
            {
                if (filas[r].Count == 1 && filas[r][0].Length == 0) continue;
                var d = new Dictionary<string, string>();
                for (int k = 0; k < encabezado.Count; k++) d[encabezado[k].Trim()] = k < filas[r].Count ? filas[r][k] : "";
                salida.Add(d);
            }
            return salida;
        }
    }

    public sealed class EntradaVocabulario
    {
        public string Glosa, Espanol, Categoria, Tipo, Modelo, Estado;
        public string[] Sinonimos;
        public int Prioridad;
        public bool EsLetra => Tipo == "letra";
        public bool EsFondo => Tipo == "fondo";
        public string EspanolCorto => Espanol.Split(new[] { " (" }, StringSplitOptions.None)[0];
    }

    public sealed class FraseRapida
    {
        public string Id, Espanol, Direccion, Estado, Contexto;
        public List<string> Glosas = new List<string>();
        public bool Rapida;
        public bool Validada => Estado == "validada" && Glosas.Count > 0;
    }

    /// <summary>Vocabulario del MVP (copia local de vocabulario_mvp.csv para funcionar sin conexión).</summary>
    public sealed class VocabularioLocal
    {
        public readonly List<EntradaVocabulario> Entradas = new List<EntradaVocabulario>();
        public readonly Dictionary<string, EntradaVocabulario> PorGlosa = new Dictionary<string, EntradaVocabulario>();
        readonly Dictionary<string, string> indice = new Dictionary<string, string>();
        readonly Dictionary<string, string> indiceSinTildes = new Dictionary<string, string>();
        readonly Dictionary<string, string> letras = new Dictionary<string, string>();
        readonly Dictionary<string, string> numeros = new Dictionary<string, string>();
        public int MaxPalabras { get; private set; } = 1;

        public static VocabularioLocal DesdeCsv(string texto)
        {
            var v = new VocabularioLocal();
            foreach (var f in Csv.Leer(texto))
            {
                var e = new EntradaVocabulario
                {
                    Glosa = f["glosa"].Trim(),
                    Espanol = f["espanol"].Trim(),
                    Sinonimos = f["sinonimos"].Split('|').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray(),
                    Categoria = f["categoria"].Trim(),
                    Prioridad = int.TryParse(f["prioridad"], out var p) ? p : 2,
                    Tipo = f["tipo"].Trim(),
                    Modelo = f["modelo"].Trim(),
                    Estado = f["estado"].Trim(),
                };
                v.Entradas.Add(e);
                v.PorGlosa[e.Glosa] = e;
                if (e.Tipo == "letra") { v.letras[e.Espanol] = e.Glosa; continue; }
                if (e.Tipo == "numero") v.numeros[e.Espanol] = e.Glosa;
                if (e.Tipo == "fondo") continue;
                foreach (var s in new[] { e.Espanol }.Concat(e.Sinonimos))
                {
                    var clave = TextoEspanol.Normalizar(s);
                    if (clave.Length == 0 || clave.StartsWith("(")) continue;
                    if (!v.indice.ContainsKey(clave)) v.indice[clave] = e.Glosa;
                    var st = TextoEspanol.SinTildes(clave);
                    if (!v.indiceSinTildes.ContainsKey(st)) v.indiceSinTildes[st] = e.Glosa;
                    v.MaxPalabras = Math.Max(v.MaxPalabras, clave.Split(' ').Length);
                }
            }
            return v;
        }

        public bool Existe(string glosa) => PorGlosa.TryGetValue(glosa, out var e) && !e.EsFondo;

        public string Buscar(string fraseNormalizada)
        {
            if (indice.TryGetValue(fraseNormalizada, out var g)) return g;
            if (TextoEspanol.Funcionales.Contains(fraseNormalizada)) return null;
            return indiceSinTildes.TryGetValue(TextoEspanol.SinTildes(fraseNormalizada), out g) ? g : null;
        }

        public string EspanolDe(string glosa)
        {
            if (glosa.StartsWith("DACT:", StringComparison.Ordinal)) return glosa.Substring(5);
            if (PorGlosa.TryGetValue(glosa, out var e)) return e.EspanolCorto;
            return glosa.ToLowerInvariant().Replace('-', ' ');
        }

        public List<string> Deletreo(string palabra)
        {
            var salida = new List<string>();
            foreach (char c in TextoEspanol.SinTildes(palabra.ToLowerInvariant()))
            {
                var s = c.ToString();
                if (letras.TryGetValue(s, out var g)) salida.Add(g);
                else if (char.IsDigit(c) && numeros.TryGetValue(s, out g)) salida.Add(g);
            }
            return salida;
        }

        public static List<FraseRapida> FrasesDesdeCsv(string texto)
        {
            return Csv.Leer(texto).Select(f => new FraseRapida
            {
                Id = f["id"].Trim(),
                Espanol = f["espanol"].Trim(),
                Glosas = f["glosas"].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList(),
                Direccion = f["direccion"].Trim(),
                Rapida = f["rapida"].Trim() == "1",
                Estado = f["estado"].Trim(),
                Contexto = f.TryGetValue("contexto", out var c) ? c.Trim() : "",
            }).ToList();
        }
    }
}
