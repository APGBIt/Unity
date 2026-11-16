using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace LSRD.Nucleo
{
    /// <summary>Un paso de lo que firma el avatar: una seña o el deletreo de una palabra.</summary>
    public sealed class ItemPlan
    {
        public bool EsDeletreo;
        public string Glosa = "";
        public string Espanol = "";
        public string Palabra = "";
        public List<string> Letras = new List<string>();

        public static ItemPlan Sena(string glosa, string espanol) => new ItemPlan { Glosa = glosa, Espanol = espanol };
        public static ItemPlan Deletreo(string palabra, List<string> letras) =>
            new ItemPlan { EsDeletreo = true, Palabra = palabra, Letras = letras };

        public override string ToString() => EsDeletreo ? "DACT:" + Palabra : Glosa;
    }

    /// <summary>Respuesta de español → LSRD (del servidor o de la traducción local).</summary>
    public sealed class ResultadoGlosas
    {
        public string Texto = "";
        public List<string> Glosas = new List<string>();
        public List<ItemPlan> Plan = new List<ItemPlan>();
        public bool Pregunta;
        public string Fuente = "";
        public bool Validada;
        public List<string> Advertencias = new List<string>();
        public string Transcripcion;

        public static ResultadoGlosas DesdeJson(Dictionary<string, object> d)
        {
            var r = new ResultadoGlosas
            {
                Texto = Json.Cad(d, "texto", ""),
                Glosas = Json.Cadenas(d, "glosas"),
                Pregunta = Json.Bool(d, "pregunta"),
                Fuente = Json.Cad(d, "fuente", ""),
                Validada = Json.Bool(d, "validada"),
                Advertencias = Json.Cadenas(d, "advertencias"),
                Transcripcion = Json.Cad(Json.Obj(d, "transcripcion"), "texto"),
            };
            var plan = Json.Lista(d, "plan");
            if (plan != null)
            {
                foreach (var o in plan)
                {
                    if (!(o is Dictionary<string, object> item)) continue;
                    if (Json.Cad(item, "tipo") == "deletreo")
                        r.Plan.Add(ItemPlan.Deletreo(Json.Cad(item, "palabra", ""), Json.Cadenas(item, "letras")));
                    else
                        r.Plan.Add(ItemPlan.Sena(Json.Cad(item, "glosa", ""), Json.Cad(item, "espanol", "")));
                }
            }
            return r;
        }
    }

    /// <summary>Respuesta de LSRD → español.</summary>
    public sealed class ResultadoTexto
    {
        public string Texto = "";
        public string Fuente = "";
        public bool Validada;
        public string Confianza = "baja";
        public List<string> Advertencias = new List<string>();

        public static ResultadoTexto DesdeJson(Dictionary<string, object> d) => new ResultadoTexto
        {
            Texto = Json.Cad(d, "texto", ""),
            Fuente = Json.Cad(d, "fuente", ""),
            Validada = Json.Bool(d, "validada"),
            Confianza = Json.Cad(d, "confianza", "baja"),
            Advertencias = Json.Cadenas(d, "advertencias"),
        };
    }

    /// <summary>
    /// Traducción sin conexión: memoria de frases validadas y, si no hay, traducción literal
    /// palabra por palabra (misma lógica que el modo "literal" del servidor).
    /// </summary>
    public sealed class TraductorLocal
    {
        static readonly Regex Num = new Regex(@"^NUM-(\d+)$", RegexOptions.CultureInvariant);
        readonly VocabularioLocal vocab;
        readonly Dictionary<string, FraseRapida> memoriaTexto = new Dictionary<string, FraseRapida>();
        readonly Dictionary<string, FraseRapida> memoriaGlosas = new Dictionary<string, FraseRapida>();

        public TraductorLocal(VocabularioLocal vocab, IEnumerable<FraseRapida> frases = null)
        {
            this.vocab = vocab;
            if (frases == null) return;
            foreach (var f in frases.Where(f => f.Validada))
            {
                memoriaTexto[TextoEspanol.Normalizar(f.Espanol)] = f;
                memoriaGlosas[string.Join(" ", f.Glosas)] = f;
            }
        }

        public ResultadoGlosas TextoAGlosas(string texto)
        {
            texto = (texto ?? "").Trim();
            var r = new ResultadoGlosas { Texto = texto, Pregunta = texto.Contains("?") || texto.Contains("¿") };
            if (texto.Length == 0) { r.Fuente = "vacía"; return r; }
            List<string> glosas;
            if (memoriaTexto.TryGetValue(TextoEspanol.Normalizar(texto), out var f))
            {
                glosas = f.Glosas;
                r.Fuente = "memoria";
                r.Validada = true;
            }
            else
            {
                glosas = Literal(texto);
                r.Fuente = "local";
                r.Advertencias.Add("Traducción literal sin conexión: no sigue la gramática de la LSRD.");
            }
            r.Plan = Plan(glosas, r.Advertencias);
            r.Glosas = r.Plan.Select(p => p.ToString()).ToList();
            return r;
        }

        public List<string> Literal(string texto)
        {
            var tokens = TextoEspanol.Normalizar(texto).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var salida = new List<string>();
            int i = 0;
            while (i < tokens.Length)
            {
                string encontrada = null;
                int n = Math.Min(vocab.MaxPalabras, tokens.Length - i);
                for (; n > 0; n--)
                {
                    encontrada = vocab.Buscar(string.Join(" ", tokens, i, n));
                    if (encontrada != null) break;
                }
                if (encontrada != null)
                {
                    salida.Add(encontrada);
                    i += n;
                    continue;
                }
                var tok = tokens[i++];
                if (tok.All(char.IsDigit)) salida.AddRange(Numero(tok));
                else if (!TextoEspanol.Funcionales.Contains(tok)) salida.Add("DACT:" + tok);
            }
            return salida;
        }

        static IEnumerable<string> Numero(string cifras)
        {
            if (cifras.Length <= 18 && long.TryParse(cifras, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n <= 10)
                return new[] { "NUM-" + n.ToString(CultureInfo.InvariantCulture) };
            return cifras.Select(c => "NUM-" + c);
        }

        /// <summary>Convierte glosas en pasos para el avatar (números cifra a cifra, deletreo de lo desconocido).</summary>
        public List<ItemPlan> Plan(IEnumerable<string> glosas, List<string> advertencias = null)
        {
            var plan = new List<ItemPlan>();
            foreach (var g0 in glosas)
            {
                var g = g0.Trim();
                if (g.StartsWith("DACT:", StringComparison.OrdinalIgnoreCase))
                {
                    var palabra = g.Substring(5).Trim().ToLowerInvariant();
                    var letras = vocab.Deletreo(palabra);
                    if (letras.Count > 0) plan.Add(ItemPlan.Deletreo(palabra, letras));
                    continue;
                }
                var gu = g.ToUpperInvariant();
                var m = Num.Match(gu);
                if (m.Success && !vocab.Existe(gu))
                {
                    foreach (char c in m.Groups[1].Value) plan.Add(ItemPlan.Sena("NUM-" + c, c.ToString()));
                    continue;
                }
                if (vocab.Existe(gu))
                {
                    var e = vocab.PorGlosa[gu];
                    if (e.EsLetra) plan.Add(ItemPlan.Deletreo(e.Espanol, new List<string> { gu }));
                    else plan.Add(ItemPlan.Sena(gu, vocab.EspanolDe(gu)));
                    continue;
                }
                var alterna = vocab.Buscar(TextoEspanol.Normalizar(g.Replace('-', ' ')));
                if (alterna != null)
                {
                    plan.Add(ItemPlan.Sena(alterna, vocab.EspanolDe(alterna)));
                    continue;
                }
                var p = g.Replace('-', ' ').ToLowerInvariant();
                advertencias?.Add($"'{g}' no está en el vocabulario; se deletrea.");
                var ls = vocab.Deletreo(p);
                if (ls.Count > 0) plan.Add(ItemPlan.Deletreo(p, ls));
            }
            return plan;
        }

        public ResultadoTexto GlosasATexto(IList<string> glosas)
        {
            var limpias = glosas.Where(g => !string.IsNullOrWhiteSpace(g) && g != ConstructorFrase.ClaseFondo)
                                .Select(g => g.StartsWith("DACT:", StringComparison.OrdinalIgnoreCase) ? "DACT:" + g.Substring(5).Trim() : g.Trim().ToUpperInvariant())
                                .ToList();
            if (limpias.Count == 0) return new ResultadoTexto { Fuente = "vacía" };
            if (memoriaGlosas.TryGetValue(string.Join(" ", limpias), out var f))
                return new ResultadoTexto { Texto = f.Espanol, Fuente = "memoria", Validada = true, Confianza = "alta" };
            var palabras = limpias.Select(g =>
            {
                var m = Num.Match(g);
                return m.Success ? m.Groups[1].Value : vocab.EspanolDe(g);
            });
            var texto = string.Join(" ", palabras).Trim();
            if (texto.Length > 0) texto = char.ToUpperInvariant(texto[0]) + texto.Substring(1);
            texto = limpias.Any(g => TextoEspanol.Preguntas.Contains(g)) ? "¿" + texto + "?" : texto + ".";
            var r = new ResultadoTexto { Texto = texto, Fuente = "local", Confianza = "baja" };
            r.Advertencias.Add("Traducción literal de las glosas (sin conexión).");
            return r;
        }
    }
}
