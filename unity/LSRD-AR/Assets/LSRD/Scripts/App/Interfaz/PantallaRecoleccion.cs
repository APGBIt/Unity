using System;
using System.Collections.Generic;
using LSRD.Nucleo;
using UnityEngine;
using UnityEngine.UI;

namespace LSRD.App
{
    /// <summary>
    /// Modo recolección: graba ejemplos de cada seña con personas sordas que dieron su
    /// consentimiento. Solo se guardan puntos clave (no video) y un perfil seudónimo.
    /// </summary>
    public sealed class PantallaRecoleccion
    {
        public const string VersionConsentimiento = "1.0";

        public static readonly string[] Regiones =
        {
            "Ozama (Gran Santo Domingo)", "Cibao Norte", "Cibao Sur", "Cibao Nordeste", "Cibao Noroeste",
            "Valdesia", "Enriquillo", "El Valle", "Yuma", "Higuamo", "Fuera del país",
        };
        public static readonly string[] Manos = { "Derecha", "Izquierda", "Ambas" };
        public static readonly string[] Edades = { "18-24", "25-34", "35-44", "45-54", "55-64", "65+" };
        public static readonly string[] Identidades = { "Sorda", "Hipoacúsica", "Oyente (intérprete o familiar)" };
        public static readonly string[] Anios = { "Menos de 2", "2-5", "5-10", "Más de 10" };
        public static readonly string[] Conjuntos = { "Núcleo (prioridad 1)", "Todo el vocabulario", "Solo letras", "Solo números" };

        public readonly RectTransform Raiz;
        public readonly RawImage VistaCamara;
        public readonly GraficoEsqueleto Esqueleto;
        public readonly GraficoEsqueleto Revision;

        public event Action AlVolver;
        public event Action<PerfilFirmante, bool, int, int> AlComenzar;   // perfil, consentimiento avatar, conjunto, repeticiones
        public event Action AlAceptar;
        public event Action AlRepetir;
        public event Action AlSaltar;
        public event Action AlTerminar;
        public event Action AlEnviar;

        readonly RectTransform pasoConsentimiento, pasoCaptura;
        readonly Toggle consPuntos, consAvatar, consMayor;
        readonly InputField codigo;
        readonly Dropdown region, mano, edad, identidad, anios, conjunto, repeticiones;
        readonly Button comenzar;
        readonly Text glosa, espanol, progreso, estado, envio;
        readonly RectTransform botonesEspera, botonesRevision, zonaRevision;
        readonly AspectRatioFitter ajusteCamara;

        public PantallaRecoleccion(Transform padre, string codigoGuardado)
        {
            Raiz = UIFabrica.Nodo(padre, "PantallaRecoleccion");
            UIFabrica.Estirar(Raiz);
            Raiz.gameObject.AddComponent<Image>().color = Tema.Fondo;

            // ------------------------------------------------ paso 1: consentimiento y perfil
            pasoConsentimiento = UIFabrica.Nodo(Raiz, "Consentimiento");
            UIFabrica.Estirar(pasoConsentimiento);
            var lista = UIFabrica.Desplazable(pasoConsentimiento, "Formulario", 18, 40);
            UIFabrica.Estirar((RectTransform)lista.parent);
            Titulo(lista, "Recolección de datos");
            Parrafo(lista,
                "Antes de grabar, explique este consentimiento en LSRD con un intérprete y responda las preguntas de la persona. " +
                "La app guarda SOLO los puntos clave de cuerpo, manos y cara (no guarda video ni fotos) junto con un código " +
                "seudónimo, región, mano dominante, rango de edad e identidad. No se guardan nombres ni datos de contacto. " +
                "Los datos se usan para entrenar y evaluar el reconocimiento de LSRD en este proyecto, y la persona puede pedir " +
                "que se borren en cualquier momento indicando su código. Solo participan personas mayores de 18 años.");
            consMayor = UIFabrica.Interruptor(lista, "La persona tiene 18 años o más", false, _ => Validar(), Tema.Pequeno);
            consPuntos = UIFabrica.Interruptor(lista, "Acepta que se guarden sus puntos clave para entrenar el modelo", false, _ => Validar(), Tema.Pequeno);
            consAvatar = UIFabrica.Interruptor(lista, "Acepta (opcional) que sus movimientos animen el avatar de la app", false, null, Tema.Pequeno);
            Subtitulo(lista, "Perfil (seudónimo)");
            codigo = UIFabrica.Campo(lista, "Código del participante (ej. P017)", codigoGuardado, _ => Validar());
            codigo.onValueChanged.AddListener(_ => Validar());
            region = Opcion(lista, "Región", Regiones);
            mano = Opcion(lista, "Mano dominante", Manos);
            edad = Opcion(lista, "Rango de edad", Edades);
            identidad = Opcion(lista, "Identidad", Identidades);
            anios = Opcion(lista, "Años usando LSRD", Anios);
            Subtitulo(lista, "Sesión");
            conjunto = Opcion(lista, "Señas a grabar", Conjuntos);
            repeticiones = Opcion(lista, "Repeticiones por seña", new[] { "3", "5", "8", "10" }, 1);
            comenzar = UIFabrica.Boton(lista, "Comenzar sesión", Comenzar, Tema.Acento, Tema.Fondo, Tema.Normal, 120);
            UIFabrica.Boton(lista, "Volver al menú", () => AlVolver?.Invoke(), Tema.SuperficieClara, null, Tema.Normal, 110);
            envio = UIFabrica.Texto(lista, "", Tema.Pequeno, Tema.TextoSecundario);
            UIFabrica.Elemento(envio, 90);
            UIFabrica.Boton(lista, "Enviar grabaciones pendientes al servidor", () => AlEnviar?.Invoke(), Tema.SuperficieClara, null, Tema.Pequeno, 100);

            // ------------------------------------------------ paso 2: captura
            pasoCaptura = UIFabrica.Nodo(Raiz, "Captura");
            UIFabrica.Estirar(pasoCaptura);
            var marco = UIFabrica.Nodo(pasoCaptura, "MarcoCamara");
            UIFabrica.Anclar(marco, new Vector2(0, 0.42f), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, Vector2.zero);
            marco.gameObject.AddComponent<RectMask2D>();
            var cam = UIFabrica.Nodo(marco, "Camara");
            VistaCamara = cam.gameObject.AddComponent<RawImage>();
            VistaCamara.color = Tema.Fondo;
            ajusteCamara = cam.gameObject.AddComponent<AspectRatioFitter>();
            ajusteCamara.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            ajusteCamara.aspectRatio = 9f / 16f;
            var esq = UIFabrica.Nodo(cam, "Esqueleto");
            UIFabrica.Estirar(esq);
            Esqueleto = esq.gameObject.AddComponent<GraficoEsqueleto>();

            zonaRevision = UIFabrica.Panel(marco, "Revision", Tema.Superficie).rectTransform;
            UIFabrica.Estirar(zonaRevision, 40, 40, 40, 40);
            var rev = UIFabrica.Nodo(zonaRevision, "Avatar");
            UIFabrica.Estirar(rev, 16, 16, 16, 70);
            Revision = rev.gameObject.AddComponent<GraficoEsqueleto>();
            Revision.ModoAvatar = true;
            var titRev = UIFabrica.Texto(zonaRevision, "Revise la grabación", Tema.Pequeno, Tema.Acento, TextAnchor.UpperCenter, FontStyle.Bold);
            UIFabrica.Anclar(titRev.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(0, 56));
            zonaRevision.gameObject.SetActive(false);

            var panel = UIFabrica.Panel(pasoCaptura, "Indicacion", Tema.Superficie).rectTransform;
            UIFabrica.Anclar(panel, new Vector2(0, 0), new Vector2(1, 0.42f), new Vector2(0.5f, 0), Vector2.zero, Vector2.zero);
            panel.offsetMin = new Vector2(16, 16);
            panel.offsetMax = new Vector2(-16, -12);
            UIFabrica.Vertical(panel.gameObject, 10, 28);
            glosa = UIFabrica.Texto(panel, "", Tema.Titulo, Tema.Texto, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFabrica.Elemento(glosa, 100);
            espanol = UIFabrica.Texto(panel, "", Tema.Normal, Tema.TextoSecundario, TextAnchor.MiddleCenter);
            UIFabrica.Elemento(espanol, 60);
            progreso = UIFabrica.Texto(panel, "", Tema.Pequeno, Tema.Acento, TextAnchor.MiddleCenter);
            UIFabrica.Elemento(progreso, 50);
            estado = UIFabrica.Texto(panel, "", Tema.Pequeno, Tema.Texto, TextAnchor.MiddleCenter);
            UIFabrica.Elemento(estado, 90);

            botonesEspera = Fila(panel, "Espera");
            UIFabrica.Boton(botonesEspera, "Saltar seña", () => AlSaltar?.Invoke(), Tema.SuperficieClara, null, Tema.Pequeno, 0);
            UIFabrica.Boton(botonesEspera, "Terminar sesión", () => AlTerminar?.Invoke(), Tema.SuperficieClara, Tema.Peligro, Tema.Pequeno, 0);
            botonesRevision = Fila(panel, "Revision");
            UIFabrica.Boton(botonesRevision, "Repetir", () => AlRepetir?.Invoke(), Tema.SuperficieClara, null, Tema.Normal, 0);
            UIFabrica.Boton(botonesRevision, "Aceptar", () => AlAceptar?.Invoke(), Tema.Acento, Tema.Fondo, Tema.Normal, 0);
            botonesRevision.gameObject.SetActive(false);

            MostrarPaso(false);
            Validar();
        }

        static void Titulo(Transform p, string t) => UIFabrica.Elemento(UIFabrica.Texto(p, t, Tema.Titulo, Tema.Texto, TextAnchor.MiddleLeft, FontStyle.Bold), 110);
        static void Subtitulo(Transform p, string t) => UIFabrica.Elemento(UIFabrica.Texto(p, t, Tema.Normal, Tema.Acento, TextAnchor.LowerLeft, FontStyle.Bold), 90);

        static void Parrafo(Transform p, string t)
        {
            var x = UIFabrica.Texto(p, t, Tema.Pequeno, Tema.TextoSecundario, TextAnchor.UpperLeft);
            x.verticalOverflow = VerticalWrapMode.Overflow;
            UIFabrica.Elemento(x, 420);
        }

        static Dropdown Opcion(Transform p, string etiqueta, IList<string> opciones, int valor = 0)
        {
            UIFabrica.Elemento(UIFabrica.Texto(p, etiqueta, Tema.Pequeno, Tema.TextoSecundario, TextAnchor.LowerLeft), 50);
            return UIFabrica.Desplegable(p, opciones, valor, null);
        }

        static RectTransform Fila(Transform p, string nombre)
        {
            var f = UIFabrica.Nodo(p, nombre);
            UIFabrica.Horizontal(f.gameObject, 16);
            UIFabrica.Elemento(f, 120);
            return f;
        }

        void Validar()
        {
            if (comenzar == null) return;
            comenzar.interactable = consMayor.isOn && consPuntos.isOn && !string.IsNullOrWhiteSpace(codigo.text);
        }

        void Comenzar()
        {
            var perfil = new PerfilFirmante
            {
                Codigo = codigo.text.Trim().ToUpperInvariant(),
                Region = Regiones[region.value],
                ManoDominante = Manos[mano.value],
                RangoEdad = Edades[edad.value],
                Identidad = Identidades[identidad.value],
                AniosLsrd = Anios[anios.value],
            };
            int[] reps = { 3, 5, 8, 10 };
            AlComenzar?.Invoke(perfil, consAvatar.isOn, conjunto.value, reps[repeticiones.value]);
        }

        // ---------------------------------------------------------------- actualización

        public void MostrarPaso(bool captura)
        {
            pasoConsentimiento.gameObject.SetActive(!captura);
            pasoCaptura.gameObject.SetActive(captura);
        }

        public void ConfigurarCamara(Texture textura, double aspecto, bool espejoPantalla)
        {
            VistaCamara.texture = textura;
            VistaCamara.color = textura != null ? Color.white : Tema.Fondo;
            VistaCamara.uvRect = espejoPantalla ? new Rect(1, 0, -1, 1) : new Rect(0, 0, 1, 1);
            ajusteCamara.aspectRatio = (float)Math.Max(0.1, aspecto);
            Esqueleto.Espejo = espejoPantalla;
        }

        public void MostrarSesion(SesionRecoleccion s, VocabularioLocal vocab)
        {
            if (s.FaseActual == SesionRecoleccion.Fase.Terminada)
            {
                glosa.text = "¡Sesión terminada!";
                espanol.text = $"{s.Guardadas} grabaciones guardadas. Gracias.";
                progreso.text = "";
                estado.text = "Puede enviarlas al servidor desde el formulario.";
                botonesEspera.gameObject.SetActive(true);
                botonesRevision.gameObject.SetActive(false);
                zonaRevision.gameObject.SetActive(false);
                return;
            }
            var g = s.GlosaActual;
            bool fondo = g == SesionRecoleccion.Fondo;
            glosa.text = fondo ? "MOVIMIENTOS NATURALES" : g;
            espanol.text = fondo
                ? "Muévase con naturalidad SIN señar: acomódese, rásquese, baje las manos."
                : vocab != null && vocab.PorGlosa.TryGetValue(g, out var e) ? e.Espanol : "";
            progreso.text = $"Seña {s.Indice + 1} de {s.Glosas.Count} · repetición {s.RepeticionActual + 1} de {s.Repeticiones} · guardadas {s.Guardadas}";
            bool revision = s.FaseActual == SesionRecoleccion.Fase.Revision;
            botonesEspera.gameObject.SetActive(!revision);
            botonesRevision.gameObject.SetActive(revision);
            zonaRevision.gameObject.SetActive(revision);
        }

        public void Estado(string texto) => estado.text = texto;

        public void Envio(string texto) => envio.text = texto;
    }
}
