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
    /// Avatar 3D humanoide opcional. Si existen Resources/LSRD/AvatarHumanoide (prefab con Animator
    /// humanoide) y Resources/LSRD/ClipsDeSenas (lista glosa → clip de captura de movimiento),
    /// se usa en lugar del esquemático para las señas que tengan clip.
    /// </summary>
    public sealed class AvatarHumanoide : MonoBehaviour
    {
        public RenderTexture Imagen { get; private set; }
        public float Velocidad = 1f;
        public float Transicion = 0.15f;
        public bool Reproduciendo { get; private set; }

        readonly Dictionary<string, AnimationClip> clips = new Dictionary<string, AnimationClip>();
        PlayableGraph grafo;
        AnimationMixerPlayable mezclador;
        AnimationClipPlayable clipActual;
        float tiempoTransicion;
        Action alTerminar;

        public static AvatarHumanoide Crear(Transform padre)
        {
            var prefab = Resources.Load<GameObject>("LSRD/AvatarHumanoide");
            var lista = Resources.Load<ClipsDeSenas>("LSRD/ClipsDeSenas");
            if (prefab == null || lista == null) return null;
            var raiz = new GameObject("LSRD-AvatarHumanoide");
            raiz.transform.SetParent(padre, false);
            raiz.transform.position = new Vector3(0, -500, 0);
            var modelo = Instantiate(prefab, raiz.transform);
            var animador = modelo.GetComponentInChildren<Animator>();
            if (animador == null) { Destroy(raiz); return null; }
            var a = raiz.AddComponent<AvatarHumanoide>();
            foreach (var par in lista.Clips) if (par.Clip != null && !string.IsNullOrEmpty(par.Glosa)) a.clips[par.Glosa] = par.Clip;
            a.Imagen = new RenderTexture(768, 768, 24) { name = "LSRD-Avatar3D" };
            var cam = new GameObject("Camara").AddComponent<Camera>();
            cam.transform.SetParent(raiz.transform, false);
            cam.transform.localPosition = new Vector3(0, 1.35f, 1.6f);
            cam.transform.LookAt(raiz.transform.position + new Vector3(0, 1.3f, 0));
            cam.fieldOfView = 30;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Tema.SuperficieClara;
            cam.targetTexture = a.Imagen;
            var luz = new GameObject("Luz").AddComponent<Light>();
            luz.transform.SetParent(raiz.transform, false);
            luz.type = LightType.Directional;
            luz.transform.rotation = Quaternion.Euler(30, 160, 0);
            a.Configurar(animador);
            return a;
        }

        void Configurar(Animator animador)
        {
            grafo = PlayableGraph.Create("LSRD-Avatar");
            var salida = AnimationPlayableOutput.Create(grafo, "Animacion", animador);
            mezclador = AnimationMixerPlayable.Create(grafo, 2);
            salida.SetSourcePlayable(mezclador);
            grafo.Play();
        }

        public bool Tiene(string glosa) => clips.ContainsKey(glosa);

        public void Reproducir(string glosa, Action fin)
        {
            if (!clips.TryGetValue(glosa, out var clip)) { fin?.Invoke(); return; }
            // La entrada 1 pasa a ser la anterior; la 0, la nueva.
            if (mezclador.GetInput(1).IsValid()) { var viejo = mezclador.GetInput(1); grafo.Disconnect(mezclador, 1); viejo.Destroy(); }
            if (mezclador.GetInput(0).IsValid())
            {
                var anterior = mezclador.GetInput(0);
                grafo.Disconnect(mezclador, 0);
                grafo.Connect(anterior, 0, mezclador, 1);
            }
            clipActual = AnimationClipPlayable.Create(grafo, clip);
            clipActual.SetSpeed(Velocidad);
            clipActual.SetTime(0);
            grafo.Connect(clipActual, 0, mezclador, 0);
            mezclador.SetInputWeight(0, 0f);
            mezclador.SetInputWeight(1, 1f);
            tiempoTransicion = 0f;
            alTerminar = fin;
            Reproduciendo = true;
        }

        void Update()
        {
            if (!Reproduciendo) return;
            tiempoTransicion += Time.deltaTime;
            float w = Transicion > 0 ? Mathf.Clamp01(tiempoTransicion / Transicion) : 1f;
            mezclador.SetInputWeight(0, w);
            mezclador.SetInputWeight(1, 1f - w);
            if (clipActual.IsValid() && clipActual.GetTime() >= clipActual.GetAnimationClip().length)
            {
                Reproduciendo = false;
                var fin = alTerminar;
                alTerminar = null;
                fin?.Invoke();
            }
        }

        void OnDestroy()
        {
            if (grafo.IsValid()) grafo.Destroy();
            if (Imagen != null) Imagen.Release();
        }
    }
}
