using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LSRD.App
{
    /// <summary>Colores y tamaños de la interfaz: alto contraste y letra grande.</summary>
    public static class Tema
    {
        public static readonly Color Fondo = Hex(0x0F1419);
        public static readonly Color Superficie = new Color(0.106f, 0.137f, 0.169f, 0.94f);
        public static readonly Color SuperficieClara = Hex(0x26313B);
        public static readonly Color Borde = Hex(0x3A4652);
        public static readonly Color Texto = Hex(0xF4F7FA);
        public static readonly Color TextoSecundario = Hex(0xA9B6C2);
        public static readonly Color Acento = Hex(0x3FB6A8);
        public static readonly Color AcentoOscuro = Hex(0x1F6F66);
        public static readonly Color Advertencia = Hex(0xF2B84B);
        public static readonly Color Peligro = Hex(0xE5484D);
        public static readonly Color Exito = Hex(0x4CC38A);
        public static readonly Color ManoIzq = Hex(0xF2B84B);
        public static readonly Color ManoDer = Hex(0x5BB4F5);
        public static readonly Color Cuerpo = new Color(0.95f, 0.97f, 0.98f, 0.85f);

        public const int Titulo = 60;
        public const int Grande = 48;
        public const int Normal = 36;
        public const int Pequeno = 28;
        public const int Mini = 24;

        static Font fuente;
        public static Font Fuente => fuente != null ? fuente : (fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        public static Color Hex(int rgb, float alfa = 1f) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alfa);

        public static Color ConAlfa(Color c, float a) => new Color(c.r, c.g, c.b, a);
    }

    /// <summary>Construcción de la interfaz por código (no depende de escenas ni prefabs).</summary>
    public static class UIFabrica
    {
        static Sprite redondeado, circulo;

        /// <summary>Rectángulo blanco con esquinas redondeadas, para estirar en 9 partes.</summary>
        public static Sprite Redondeado
        {
            get
            {
                if (redondeado != null) return redondeado;
                const int t = 64, r = 24;
                var tex = new Texture2D(t, t, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "LSRD-Redondeado" };
                var px = new Color32[t * t];
                for (int y = 0; y < t; y++)
                for (int x = 0; x < t; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, r, t - r), cy = Mathf.Clamp(y + 0.5f, r, t - r);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    byte a = (byte)(Mathf.Clamp01(r - d + 0.5f) * 255);
                    px[y * t + x] = new Color32(255, 255, 255, a);
                }
                tex.SetPixels32(px);
                tex.Apply();
                redondeado = Sprite.Create(tex, new Rect(0, 0, t, t), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                    new Vector4(r, r, r, r));
                return redondeado;
            }
        }

        public static Sprite Circulo
        {
            get
            {
                if (circulo != null) return circulo;
                const int t = 64;
                var tex = new Texture2D(t, t, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "LSRD-Circulo" };
                var px = new Color32[t * t];
                float r = t / 2f;
                for (int y = 0; y < t; y++)
                for (int x = 0; x < t; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    px[y * t + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(r - d) * 255));
                }
                tex.SetPixels32(px);
                tex.Apply();
                circulo = Sprite.Create(tex, new Rect(0, 0, t, t), new Vector2(0.5f, 0.5f), 100f);
                return circulo;
            }
        }

        // ---------------------------------------------------------------- estructura

        public static Canvas CrearCanvas(string nombre)
        {
            var go = new GameObject(nombre, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var escala = go.GetComponent<CanvasScaler>();
            escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            escala.referenceResolution = new Vector2(1080, 1920);
            escala.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            escala.matchWidthOrHeight = 0.5f;
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM && LSRD_INPUT_SYSTEM
                es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
                es.AddComponent<StandaloneInputModule>();
#endif
            }
            return canvas;
        }

        public static RectTransform Nodo(Transform padre, string nombre)
        {
            var go = new GameObject(nombre, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(padre, false);
            return rt;
        }

        public static void Estirar(RectTransform rt, float izq = 0, float abajo = 0, float der = 0, float arriba = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(izq, abajo);
            rt.offsetMax = new Vector2(-der, -arriba);
        }

        public static void Anclar(RectTransform rt, Vector2 min, Vector2 max, Vector2 pivote, Vector2 posicion, Vector2 tamano)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.pivot = pivote;
            rt.anchoredPosition = posicion;
            rt.sizeDelta = tamano;
        }

        public static Image Panel(Transform padre, string nombre, Color color, bool redondeado = true, float radio = 1f)
        {
            var rt = Nodo(padre, nombre);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            if (redondeado)
            {
                img.sprite = Redondeado;
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 1f / Mathf.Max(0.1f, radio);
            }
            return img;
        }

        public static VerticalLayoutGroup Vertical(GameObject go, float espacio, int relleno = 0, bool expandirAncho = true)
        {
            var v = go.GetComponent<VerticalLayoutGroup>();
            if (v == null) v = go.AddComponent<VerticalLayoutGroup>();
            v.spacing = espacio;
            v.padding = new RectOffset(relleno, relleno, relleno, relleno);
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = expandirAncho;
            v.childForceExpandHeight = false;
            v.childAlignment = TextAnchor.UpperCenter;
            return v;
        }

        public static HorizontalLayoutGroup Horizontal(GameObject go, float espacio, int relleno = 0, bool expandir = true)
        {
            var h = go.GetComponent<HorizontalLayoutGroup>();
            if (h == null) h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = espacio;
            h.padding = new RectOffset(relleno, relleno, relleno, relleno);
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = expandir;
            h.childForceExpandHeight = true;
            h.childAlignment = TextAnchor.MiddleCenter;
            return h;
        }

        public static LayoutElement Elemento(Component c, float alto = -1, float ancho = -1, float flexAncho = -1, float flexAlto = -1)
        {
            var le = c.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = alto;
            le.minHeight = alto;
            le.preferredWidth = ancho;
            le.flexibleWidth = flexAncho;
            le.flexibleHeight = flexAlto;
            return le;
        }

        // ---------------------------------------------------------------- controles

        public static Text Texto(Transform padre, string texto, int tam = Tema.Normal, Color? color = null,
            TextAnchor alineacion = TextAnchor.MiddleLeft, FontStyle estilo = FontStyle.Normal)
        {
            var rt = Nodo(padre, "Texto");
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Tema.Fuente;
            t.text = texto;
            t.fontSize = tam;
            t.color = color ?? Tema.Texto;
            t.alignment = alineacion;
            t.fontStyle = estilo;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.supportRichText = true;
            t.raycastTarget = false;
            return t;
        }

        public static Button Boton(Transform padre, string texto, Action alPulsar, Color? fondo = null, Color? colorTexto = null,
            int tam = Tema.Normal, float alto = 110)
        {
            var img = Panel(padre, "Boton: " + texto, fondo ?? Tema.SuperficieClara);
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var colores = b.colors;
            colores.highlightedColor = new Color(1f, 1f, 1f, 1f);
            colores.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            colores.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            b.colors = colores;
            var t = Texto(img.transform, texto, tam, colorTexto ?? Tema.Texto, TextAnchor.MiddleCenter, FontStyle.Bold);
            Estirar(t.rectTransform, 16, 4, 16, 4);
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = Mathf.Max(14, tam / 2);
            t.resizeTextMaxSize = tam;
            if (alPulsar != null) b.onClick.AddListener(() => alPulsar());
            if (alto > 0) Elemento(b, alto);
            return b;
        }

        public static void TextoDeBoton(Button b, string texto)
        {
            var t = b.GetComponentInChildren<Text>();
            if (t != null) t.text = texto;
        }

        public static Toggle Interruptor(Transform padre, string texto, bool valor, Action<bool> alCambiar, int tam = Tema.Normal)
        {
            var fila = Nodo(padre, "Interruptor: " + texto);
            Horizontal(fila.gameObject, 24, 0, false).childAlignment = TextAnchor.MiddleLeft;
            Elemento(fila, 96);
            var caja = Panel(fila, "Caja", Tema.SuperficieClara);
            Elemento(caja, 64, 64);
            var marca = Panel(caja.transform, "Marca", Tema.Acento);
            Estirar(marca.rectTransform, 12, 12, 12, 12);
            var tg = fila.gameObject.AddComponent<Toggle>();
            tg.targetGraphic = caja;
            tg.graphic = marca;
            tg.isOn = valor;
            var etiqueta = Texto(fila, texto, tam);
            Elemento(etiqueta, 96, -1, 1);
            if (alCambiar != null) tg.onValueChanged.AddListener(v => alCambiar(v));
            return tg;
        }

        public static InputField Campo(Transform padre, string indicacion, string valor, Action<string> alTerminar,
            InputField.ContentType tipo = InputField.ContentType.Standard, int tam = Tema.Normal, float alto = 104)
        {
            var img = Panel(padre, "Campo: " + indicacion, Tema.SuperficieClara);
            var campo = img.gameObject.AddComponent<InputField>();
            var texto = Texto(img.transform, "", tam);
            texto.supportRichText = false;
            texto.verticalOverflow = VerticalWrapMode.Overflow;
            Estirar(texto.rectTransform, 24, 8, 24, 8);
            var ph = Texto(img.transform, indicacion, tam, Tema.TextoSecundario, TextAnchor.MiddleLeft, FontStyle.Italic);
            Estirar(ph.rectTransform, 24, 8, 24, 8);
            campo.textComponent = texto;
            campo.placeholder = ph;
            campo.targetGraphic = img;
            campo.contentType = tipo;
            campo.text = valor ?? "";
            if (alTerminar != null) campo.onEndEdit.AddListener(v => alTerminar(v));
            Elemento(img, alto);
            return campo;
        }

        public static Dropdown Desplegable(Transform padre, IList<string> opciones, int valor, Action<int> alCambiar, int tam = Tema.Normal)
        {
            var recursos = new DefaultControls.Resources
            {
                standard = Redondeado, background = Redondeado, inputField = Redondeado,
                knob = Circulo, checkmark = Circulo, dropdown = Circulo, mask = Redondeado,
            };
            var go = DefaultControls.CreateDropdown(recursos);
            go.transform.SetParent(padre, false);
            var dd = go.GetComponent<Dropdown>();
            go.GetComponent<Image>().color = Tema.SuperficieClara;
            foreach (var t in go.GetComponentsInChildren<Text>(true))
            {
                t.font = Tema.Fuente;
                t.fontSize = tam;
                t.color = t.transform.parent != null && t.transform.parent.name.Contains("Item") ? Color.black : Tema.Texto;
            }
            if (dd.template != null)
            {
                dd.template.sizeDelta = new Vector2(dd.template.sizeDelta.x, 520);
                var item = dd.template.GetComponentInChildren<Toggle>(true);
                if (item != null) ((RectTransform)item.transform).sizeDelta = new Vector2(0, 90);
            }
            dd.ClearOptions();
            dd.AddOptions(new List<string>(opciones));
            dd.SetValueWithoutNotify(Mathf.Clamp(valor, 0, Mathf.Max(0, opciones.Count - 1)));
            if (alCambiar != null) dd.onValueChanged.AddListener(v => alCambiar(v));
            Elemento(dd, 104);
            return dd;
        }

        public static Slider Deslizador(Transform padre, float min, float max, float valor, Action<float> alCambiar)
        {
            var recursos = new DefaultControls.Resources { standard = Redondeado, background = Redondeado, knob = Circulo };
            var go = DefaultControls.CreateSlider(recursos);
            go.transform.SetParent(padre, false);
            var s = go.GetComponent<Slider>();
            s.minValue = min;
            s.maxValue = max;
            s.SetValueWithoutNotify(valor);
            foreach (var img in go.GetComponentsInChildren<Image>(true))
                img.color = img.gameObject.name == "Fill" ? Tema.Acento : img.gameObject.name == "Handle" ? Tema.Texto : Tema.SuperficieClara;
            if (alCambiar != null) s.onValueChanged.AddListener(v => alCambiar(v));
            Elemento(s, 72);
            return s;
        }

        /// <summary>Área con desplazamiento vertical; devuelve el contenedor donde agregar filas.</summary>
        public static RectTransform Desplazable(Transform padre, string nombre, float espacio = 16, int relleno = 24)
        {
            var vista = Nodo(padre, nombre);
            var img = vista.gameObject.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0.001f);
            vista.gameObject.AddComponent<RectMask2D>();
            var sr = vista.gameObject.AddComponent<ScrollRect>();
            sr.horizontal = false;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 40;
            var contenido = Nodo(vista, "Contenido");
            contenido.anchorMin = new Vector2(0, 1);
            contenido.anchorMax = new Vector2(1, 1);
            contenido.pivot = new Vector2(0.5f, 1);
            contenido.offsetMin = Vector2.zero;
            contenido.offsetMax = Vector2.zero;
            Vertical(contenido.gameObject, espacio, relleno);
            var ajuste = contenido.gameObject.AddComponent<ContentSizeFitter>();
            ajuste.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.content = contenido;
            sr.viewport = vista;
            return contenido;
        }

        public static void Limpiar(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
        }
    }
}
