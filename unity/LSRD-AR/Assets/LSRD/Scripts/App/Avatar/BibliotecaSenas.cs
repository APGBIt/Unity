using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using LSRD.Nucleo;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.UI;

namespace LSRD.App
{
    /// <summary>
    /// Grabaciones de referencia de cada seña para el avatar (de firmantes sordos que dieron
    /// consentimiento para este uso). Vienen incluidas en Resources/LSRD/Avatar o se descargan del
    /// servidor a la carpeta local.
    /// </summary>
    public sealed class BibliotecaSenas
    {
        readonly Dictionary<string, Func<string>> fuentes = new Dictionary<string, Func<string>>();
        readonly Dictionary<string, MuestraLandmarks> cache = new Dictionary<string, MuestraLandmarks>();
        public string CarpetaLocal => Path.Combine(Application.persistentDataPath, "avatar");
        public int Cantidad => fuentes.Count;

        public void Cargar()
        {
            fuentes.Clear();
            cache.Clear();
            foreach (var t in Resources.LoadAll<TextAsset>("LSRD/Avatar"))
            {
                var texto = t;
                fuentes[t.name] = () => texto.text;
            }
            if (Directory.Exists(CarpetaLocal))
            {
                foreach (var ruta in Directory.GetFiles(CarpetaLocal, "*.json"))
                {
                    var r = ruta;
                    fuentes[Path.GetFileNameWithoutExtension(ruta)] = () => File.ReadAllText(r);
                }
            }
        }

        public bool Tiene(string glosa) => !string.IsNullOrEmpty(glosa) && fuentes.ContainsKey(glosa);

        public MuestraLandmarks Obtener(string glosa)
        {
            if (cache.TryGetValue(glosa, out var m)) return m;
            if (!fuentes.TryGetValue(glosa, out var leer)) return null;
            try
            {
                m = MuestraLandmarks.DesdeJson(leer());
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LSRD] Grabación de avatar inválida para {glosa}: {e.Message}");
                fuentes.Remove(glosa);
                return null;
            }
            cache[glosa] = m;
            return m;
        }

        /// <summary>Una grabación cualquiera, para mostrar la postura de reposo.</summary>
        public MuestraLandmarks Referencia()
        {
            foreach (var g in fuentes.Keys)
            {
                var m = Obtener(g);
                if (m != null && m.NumeroCuadros > 0) return m;
            }
            return null;
        }

        public IEnumerator Sincronizar(ClienteLSRD cliente, Action<int, string> fin)
        {
            List<KeyValuePair<string, string>> indice = null;
            string error = null;
            yield return cliente.IndiceAvatar((l, e) => { indice = l; error = e; });
            if (error != null) { fin(0, error); yield break; }
            Directory.CreateDirectory(CarpetaLocal);
            var versiones = new Dictionary<string, string>();
            var rutaVersiones = Path.Combine(CarpetaLocal, "versiones.txt");
            if (File.Exists(rutaVersiones))
                foreach (var linea in File.ReadAllLines(rutaVersiones))
                {
                    var partes = linea.Split('\t');
                    if (partes.Length == 2) versiones[partes[0]] = partes[1];
                }
            int nuevas = 0;
            foreach (var kv in indice)
            {
                if (versiones.TryGetValue(kv.Key, out var v) && v == kv.Value && File.Exists(Path.Combine(CarpetaLocal, kv.Key + ".json"))) continue;
                string texto = null;
                yield return cliente.DescargarAvatar(kv.Key, (t, e) => { texto = t; error = e; });
                if (texto == null) continue;
                File.WriteAllText(Path.Combine(CarpetaLocal, kv.Key + ".json"), texto);
                versiones[kv.Key] = kv.Value;
                nuevas++;
            }
            var lineas = new List<string>();
            foreach (var kv in versiones) lineas.Add(kv.Key + "\t" + kv.Value);
            File.WriteAllLines(rutaVersiones, lineas);
            Cargar();
            fin(nuevas, null);
        }
    }
}
