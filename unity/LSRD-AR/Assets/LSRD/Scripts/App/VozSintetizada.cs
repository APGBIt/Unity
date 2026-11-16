using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using LSRD.Nucleo;
using UnityEngine;

namespace LSRD.App
{
    /// <summary>Voz sintetizada del sistema (Android TextToSpeech, iOS AVSpeechSynthesizer).</summary>
    public sealed class VozSintetizada : MonoBehaviour
    {
        public bool Activa = true;
        public float Velocidad = 1f;
        public bool Hablando => Time.realtimeSinceStartup < finEstimado;
        public string Estado { get; private set; } = "Sin iniciar";
        float finEstimado;
        int contador;

#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaObject tts;
        bool lista;

        sealed class OyenteInicio : AndroidJavaProxy
        {
            readonly VozSintetizada voz;
            public OyenteInicio(VozSintetizada voz) : base("android.speech.tts.TextToSpeech$OnInitListener") { this.voz = voz; }
            public void onInit(int estado) { voz.AlIniciar(estado); }
        }

        void Start()
        {
            try
            {
                var jugador = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                var actividad = jugador.GetStatic<AndroidJavaObject>("currentActivity");
                tts = new AndroidJavaObject("android.speech.tts.TextToSpeech", actividad, new OyenteInicio(this));
                Estado = "Iniciando voz";
            }
            catch (Exception e)
            {
                Estado = "Voz no disponible: " + e.Message;
            }
        }

        void AlIniciar(int estado)
        {
            if (estado != 0) { Estado = "Voz no disponible"; return; }
            var local = new AndroidJavaObject("java.util.Locale", "es", "DO");
            int r = tts.Call<int>("setLanguage", local);
            if (r < 0) tts.Call<int>("setLanguage", new AndroidJavaObject("java.util.Locale", "es"));
            lista = true;
            Estado = "Voz lista";
        }

        void Decir(string texto)
        {
            if (!lista) return;
            tts.Call<int>("setSpeechRate", Velocidad);
            var parametros = new AndroidJavaObject("android.os.Bundle");
            tts.Call<int>("speak", texto, 0, parametros, "lsrd-" + (++contador));
        }

        void OnDestroy()
        {
            if (tts != null) { tts.Call("shutdown"); tts.Dispose(); }
        }
#elif UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void LSRD_Hablar(string texto, string idioma, float velocidad);

        void Start() { Estado = "Voz lista"; }
        void Decir(string texto) { LSRD_Hablar(texto, "es-MX", Velocidad); }
#else
        void Start() { Estado = "Voz simulada (editor)"; }
        void Decir(string texto) { Debug.Log("[LSRD] Voz: " + texto); contador++; }
#endif

        /// <param name="forzar">Habla aunque la voz esté desactivada en ajustes (emergencias).</param>
        public void Hablar(string texto, bool forzar = false)
        {
            if ((!Activa && !forzar) || string.IsNullOrWhiteSpace(texto)) return;
            finEstimado = Time.realtimeSinceStartup + 0.6f + texto.Length * 0.065f / Mathf.Max(0.5f, Velocidad);
            try { Decir(texto); }
            catch (Exception e) { Debug.LogWarning("[LSRD] No se pudo hablar: " + e.Message); }
        }
    }
}
