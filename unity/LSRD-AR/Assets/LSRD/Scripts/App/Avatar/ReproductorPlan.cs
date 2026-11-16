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
    /// Convierte el plan de traducción (señas y deletreos) en lo que muestra el avatar: grabaciones
    /// de referencia, clips 3D o, si la seña aún no tiene grabación, una tarjeta con la palabra.
    /// </summary>
    public sealed class ReproductorPlan : MonoBehaviour
    {
        public AvatarEsquematico Esquematico;
        public AvatarHumanoide Humanoide;
        public BibliotecaSenas Biblioteca;
        public Text Rotulo;
        public Text Tarjeta;
        public float Velocidad = 1f;
        public bool Reproduciendo { get; private set; }
        public event Action AlTerminar;
        public Func<MuestraLandmarks> Reposo;

        struct Paso
        {
            public string Glosa;
            public string Texto;
            public string Palabra;
            public bool EsLetra;
        }

        readonly Queue<Paso> pasos = new Queue<Paso>();
        List<ItemPlan> ultimo;
        float finTarjeta = -1f;

        public void Reproducir(List<ItemPlan> plan)
        {
            Detener();
            if (plan == null || plan.Count == 0) return;
            ultimo = plan;
            foreach (var item in plan)
            {
                if (item.EsDeletreo)
                {
                    foreach (var letra in item.Letras)
                        pasos.Enqueue(new Paso { Glosa = letra, Texto = VotacionDactilologia.GlosaALetra(letra).ToUpperInvariant(), Palabra = item.Palabra, EsLetra = true });
                }
                else
                {
                    pasos.Enqueue(new Paso { Glosa = item.Glosa, Texto = item.Espanol, Palabra = item.Espanol });
                }
            }
            Reproduciendo = true;
            Siguiente();
        }

        public void Repetir()
        {
            if (ultimo != null) Reproducir(ultimo);
        }

        public void Detener()
        {
            pasos.Clear();
            finTarjeta = -1f;
            Esquematico?.Detener();
            if (Tarjeta != null) Tarjeta.text = "";
            Reproduciendo = false;
        }

        void Siguiente()
        {
            if (Tarjeta != null) Tarjeta.text = "";
            if (pasos.Count == 0)
            {
                Reproduciendo = false;
                Esquematico?.MostrarReposo(Reposo != null ? Reposo() : Biblioteca?.Referencia());
                AlTerminar?.Invoke();
                return;
            }
            var p = pasos.Dequeue();
            if (Rotulo != null) Rotulo.text = p.EsLetra ? $"{p.Palabra}  ·  {p.Texto}" : p.Texto;
            float v = Mathf.Max(0.25f, Velocidad);
            if (Humanoide != null && Humanoide.Tiene(p.Glosa))
            {
                Humanoide.Velocidad = v;
                Humanoide.Reproducir(p.Glosa, Siguiente);
                return;
            }
            var grabacion = Biblioteca?.Obtener(p.Glosa);
            if (grabacion != null && Esquematico != null)
            {
                Esquematico.Velocidad = v;
                Esquematico.Reproducir(grabacion, Siguiente);
                return;
            }
            if (Tarjeta != null) Tarjeta.text = p.Texto;
            finTarjeta = Time.realtimeSinceStartup + (p.EsLetra ? 0.55f : 1.0f) / v;
        }

        void Update()
        {
            if (finTarjeta > 0 && Time.realtimeSinceStartup >= finTarjeta)
            {
                finTarjeta = -1f;
                Siguiente();
            }
        }
    }
}
