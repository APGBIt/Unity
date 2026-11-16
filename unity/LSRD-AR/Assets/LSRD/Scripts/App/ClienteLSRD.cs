using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using LSRD.Nucleo;
using UnityEngine;
using UnityEngine.Networking;

namespace LSRD.App
{
    /// <summary>Cliente HTTP del servidor LSRD (traducción, voz, grabaciones y avatar).</summary>
    public sealed class ClienteLSRD
    {
        public string Url;
        public string Clave;
        public int TiempoEsperaS = 20;
        public bool Conectado { get; private set; }
        public bool TraduccionLlm { get; private set; }
        public bool VozDisponible { get; private set; }
        public string UltimoError { get; private set; }
        public long UltimoCodigo { get; private set; }

        public ClienteLSRD(string url, string clave)
        {
            Url = (url ?? "").Trim().TrimEnd('/');
            Clave = clave ?? "";
        }

        UnityWebRequest Preparar(UnityWebRequest req)
        {
            req.timeout = TiempoEsperaS;
            if (!string.IsNullOrEmpty(Clave)) req.SetRequestHeader("X-Clave-LSRD", Clave);
            return req;
        }

        UnityWebRequest PostJson(string ruta, string json)
        {
            var req = new UnityWebRequest(Url + ruta, "POST")
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)) { contentType = "application/json" },
                downloadHandler = new DownloadHandlerBuffer(),
            };
            return Preparar(req);
        }

        IEnumerator Enviar(UnityWebRequest req, Action<Dictionary<string, object>, string> fin)
        {
            using (req)
            {
                yield return req.SendWebRequest();
                UltimoCodigo = req.responseCode;
                if (req.result != UnityWebRequest.Result.Success)
                {
                    string detalle = req.downloadHandler != null ? req.downloadHandler.text : "";
                    UltimoError = $"{req.error} {Resumir(detalle)}".Trim();
                    if (req.result == UnityWebRequest.Result.ConnectionError) Conectado = false;
                    fin(null, UltimoError);
                    yield break;
                }
                Conectado = true;
                Dictionary<string, object> d;
                try
                {
                    d = Json.Objeto(req.downloadHandler.text);
                }
                catch (Exception e)
                {
                    UltimoError = "Respuesta inválida del servidor: " + e.Message;
                    fin(null, UltimoError);
                    yield break;
                }
                fin(d, null);
            }
        }

        static string Resumir(string texto)
        {
            if (string.IsNullOrEmpty(texto)) return "";
            try
            {
                var d = Json.Objeto(texto);
                var detalle = Json.Cad(d, "detail");
                if (detalle != null) return detalle;
            }
            catch (Exception) { /* no era JSON */ }
            return texto.Length > 160 ? texto.Substring(0, 160) : texto;
        }

        public IEnumerator Salud(Action<string> fin)
        {
            if (string.IsNullOrEmpty(Url)) { Conectado = false; fin("No hay servidor configurado"); yield break; }
            yield return Enviar(Preparar(UnityWebRequest.Get(Url + "/salud")), (d, e) =>
            {
                if (d != null)
                {
                    TraduccionLlm = Json.Bool(d, "traduccion_llm");
                    VozDisponible = Json.Bool(d, "voz_a_texto");
                }
                fin(e);
            });
        }

        public IEnumerator TextoAGlosas(string texto, Action<ResultadoGlosas, string> fin)
        {
            var json = "{\"texto\":" + Json.Cadena(texto) + "}";
            yield return Enviar(PostJson("/v1/traducir/texto", json), (d, e) => fin(d != null ? ResultadoGlosas.DesdeJson(d) : null, e));
        }

        public IEnumerator GlosasATexto(IList<string> glosas, Action<ResultadoTexto, string> fin)
        {
            var json = "{\"glosas\":" + Json.Escribir(glosas) + "}";
            yield return Enviar(PostJson("/v1/traducir/glosas", json), (d, e) => fin(d != null ? ResultadoTexto.DesdeJson(d) : null, e));
        }

        public IEnumerator VozASenas(byte[] wav, Action<ResultadoGlosas, string> fin)
        {
            var formulario = new List<IMultipartFormSection> { new MultipartFormFileSection("audio", wav, "voz.wav", "audio/wav") };
            var req = Preparar(UnityWebRequest.Post(Url + "/v1/voz-a-senas", formulario));
            yield return Enviar(req, (d, e) => fin(d != null ? ResultadoGlosas.DesdeJson(d) : null, e));
        }

        public IEnumerator EnviarMuestra(string jsonMuestra, Action<string> fin)
        {
            yield return Enviar(PostJson("/v1/recolecciones", jsonMuestra), (d, e) => fin(e));
        }

        public IEnumerator EnviarReporte(string jsonReporte, Action<string> fin)
        {
            yield return Enviar(PostJson("/v1/retroalimentacion", jsonReporte), (d, e) => fin(e));
        }

        public IEnumerator IndiceAvatar(Action<List<KeyValuePair<string, string>>, string> fin)
        {
            yield return Enviar(Preparar(UnityWebRequest.Get(Url + "/v1/avatar/indice")), (d, e) =>
            {
                var lista = new List<KeyValuePair<string, string>>();
                var items = Json.Lista(d, "senas");
                if (items != null)
                    foreach (var o in items)
                        if (o is Dictionary<string, object> x)
                            lista.Add(new KeyValuePair<string, string>(Json.Cad(x, "glosa", ""), Json.Cad(x, "version", "")));
                fin(lista, e);
            });
        }

        public IEnumerator DescargarAvatar(string glosa, Action<string, string> fin)
        {
            var req = Preparar(UnityWebRequest.Get(Url + "/v1/avatar/" + UnityWebRequest.EscapeURL(glosa)));
            using (req)
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success) fin(null, req.error);
                else fin(req.downloadHandler.text, null);
            }
        }
    }
}
