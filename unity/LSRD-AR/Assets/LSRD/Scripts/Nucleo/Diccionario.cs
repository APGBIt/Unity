using System;
using System.Collections.Generic;
using System.Linq;

namespace LSRD.Nucleo
{
    /// <summary>Una entrada del diccionario de la app: la seña del vocabulario y su referencia oficial.</summary>
    public sealed class FichaSena
    {
        public EntradaVocabulario Entrada;
        /// <summary>Título de la entrada en el Diccionario Oficial de LSRD; vacío si no se localizó.</summary>
        public string TituloOficial = "";
        public string UrlOficial = "";
        public bool TieneReferenciaOficial => TituloOficial.Length > 0;
        public bool Validada => Entrada.Estado == "validada";
        internal string[] Terminos;
    }

    /// <summary>
    /// Diccionario consultable sin conexión. Reúne el vocabulario del MVP con el
    /// cruce contra el índice del Diccionario Oficial (cobertura_diccionario_oficial.csv).
    /// No contiene material del diccionario oficial: solo el título y la dirección.
    /// </summary>
    public sealed class DiccionarioLocal
    {
        public readonly List<FichaSena> Fichas = new List<FichaSena>();
        public readonly List<string> Categorias = new List<string>();

        public static DiccionarioLocal Crear(VocabularioLocal vocab, string csvCobertura = null)
        {
            var cobertura = new Dictionary<string, Dictionary<string, string>>();
            if (!string.IsNullOrEmpty(csvCobertura))
                foreach (var f in Csv.Leer(csvCobertura))
                    if (f.TryGetValue("glosa", out var g)) cobertura[g.Trim()] = f;

            var d = new DiccionarioLocal();
            foreach (var e in vocab.Entradas)
            {
                if (e.EsFondo) continue;
                var ficha = new FichaSena { Entrada = e };
                if (cobertura.TryGetValue(e.Glosa, out var c) && Valor(c, "resultado") == "encontrada")
                {
                    ficha.TituloOficial = Valor(c, "titulo_oficial");
                    ficha.UrlOficial = Valor(c, "url");
                }
                var terminos = new List<string> { e.Espanol, e.Glosa.Replace('-', ' ') };
                if (e.Sinonimos != null) terminos.AddRange(e.Sinonimos);
                ficha.Terminos = terminos.Select(Clave).Where(t => t.Length > 0).Distinct().ToArray();
                d.Fichas.Add(ficha);
                if (!d.Categorias.Contains(e.Categoria)) d.Categorias.Add(e.Categoria);
            }
            d.Categorias.Sort(string.CompareOrdinal);
            return d;
        }

        static string Valor(Dictionary<string, string> f, string clave) =>
            f.TryGetValue(clave, out var v) && v != null ? v.Trim() : "";

        /// <summary>Minúsculas, sin signos ni tildes (conserva la ñ): «¿Cédula?» → «cedula».</summary>
        public static string Clave(string texto) => TextoEspanol.SinTildes(TextoEspanol.Normalizar(texto));

        /// <summary>
        /// Busca por palabra en español, sinónimo o glosa. Primero las que empiezan
        /// por el texto, luego las que lo contienen; dentro de cada grupo, alfabético.
        /// </summary>
        public List<FichaSena> Buscar(string texto, string categoria = null)
        {
            string q = Clave(texto ?? "");
            var empiezan = new List<FichaSena>();
            var contienen = new List<FichaSena>();
            foreach (var f in Fichas)
            {
                if (!string.IsNullOrEmpty(categoria) && f.Entrada.Categoria != categoria) continue;
                if (q.Length == 0) { empiezan.Add(f); continue; }
                bool empieza = false, contiene = false;
                foreach (var t in f.Terminos)
                {
                    if (t.StartsWith(q, StringComparison.Ordinal)) { empieza = true; break; }
                    if (t.Contains(q)) contiene = true;
                }
                if (empieza) empiezan.Add(f);
                else if (contiene) contienen.Add(f);
            }
            if (q.Length > 0)
            {
                Comparison<FichaSena> alfabetico = (a, b) => string.CompareOrdinal(Clave(a.Entrada.Espanol), Clave(b.Entrada.Espanol));
                empiezan.Sort(alfabetico);
                contienen.Sort(alfabetico);
            }
            empiezan.AddRange(contienen);
            return empiezan;
        }

        public FichaSena PorGlosa(string glosa) => Fichas.FirstOrDefault(f => f.Entrada.Glosa == glosa);
    }
}
