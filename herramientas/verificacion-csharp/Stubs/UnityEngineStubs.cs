// Firmas mínimas de la API de Unity que usa la app LSRD, para COMPILAR (no ejecutar) los
// scripts fuera del editor. Reproducen las firmas públicas reales de Unity 6.
#pragma warning disable CS0067, CS0108, CS0114, CS0649, CS1591
using System;
using System.Collections;
using System.Collections.Generic;

namespace UnityEngine
{
    public class Object
    {
        public string name { get; set; }
        public static void Destroy(Object obj) { }
        public static void Destroy(Object obj, float t) { }
        public static void DontDestroyOnLoad(Object target) { }
        public static T Instantiate<T>(T original, Transform parent) where T : Object => original;
        public static T Instantiate<T>(T original) where T : Object => original;
        public static T FindFirstObjectByType<T>() where T : Object => null;
        public static bool operator ==(Object a, Object b) => ReferenceEquals(a, b);
        public static bool operator !=(Object a, Object b) => !ReferenceEquals(a, b);
        public static implicit operator bool(Object o) => !ReferenceEquals(o, null);
        public override bool Equals(object o) => ReferenceEquals(this, o);
        public override int GetHashCode() => 0;
    }

    public class GameObject : Object
    {
        public GameObject() { }
        public GameObject(string name) { }
        public GameObject(string name, params Type[] components) { }
        public Transform transform => null;
        public T AddComponent<T>() where T : Component => null;
        public Component AddComponent(Type t) => null;
        public T GetComponent<T>() => default;
        public T GetComponentInChildren<T>() => default;
        public T GetComponentInChildren<T>(bool includeInactive) => default;
        public T[] GetComponentsInChildren<T>(bool includeInactive) => null;
        public void SetActive(bool value) { }
        public bool activeSelf => true;
        public bool activeInHierarchy => true;
        public string tag { get; set; }
    }

    public class Component : Object
    {
        public GameObject gameObject => null;
        public Transform transform => null;
        public T GetComponent<T>() => default;
        public T GetComponentInChildren<T>() => default;
        public T GetComponentInChildren<T>(bool includeInactive) => default;
        public T[] GetComponentsInChildren<T>() => null;
        public T[] GetComponentsInChildren<T>(bool includeInactive) => null;
        public string tag { get; set; }
    }

    public class Behaviour : Component { public bool enabled { get; set; } public bool isActiveAndEnabled => true; }

    public class Coroutine { }
    public class YieldInstruction { }
    public class AsyncOperation : YieldInstruction { public bool isDone => true; public float progress => 1; }

    public class MonoBehaviour : Behaviour
    {
        public Coroutine StartCoroutine(IEnumerator routine) => null;
        public void StopCoroutine(Coroutine c) { }
        public void StopAllCoroutines() { }
    }

    public class ScriptableObject : Object { public static T CreateInstance<T>() where T : ScriptableObject => null; }

    public class Transform : Component, IEnumerable
    {
        public Vector3 position { get; set; }
        public Vector3 localPosition { get; set; }
        public Quaternion rotation { get; set; }
        public Vector3 localEulerAngles { get; set; }
        public Vector3 localScale { get; set; }
        public Transform parent { get; set; }
        public int childCount => 0;
        public Transform GetChild(int i) => null;
        public void SetParent(Transform p, bool worldPositionStays) { }
        public void SetParent(Transform p) { }
        public void LookAt(Vector3 worldPosition) { }
        public Vector3 TransformPoint(Vector3 p) => p;
        public Vector3 InverseTransformPoint(Vector3 p) => p;
        public void SetAsLastSibling() { }
        public IEnumerator GetEnumerator() { yield break; }
    }

    public class RectTransform : Transform
    {
        public Vector2 anchorMin { get; set; }
        public Vector2 anchorMax { get; set; }
        public Vector2 pivot { get; set; }
        public Vector2 anchoredPosition { get; set; }
        public Vector2 sizeDelta { get; set; }
        public Vector2 offsetMin { get; set; }
        public Vector2 offsetMax { get; set; }
        public Rect rect => default;
    }

    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => default;
        public static Vector2 one => new Vector2(1, 1);
        public float magnitude => 0;
        public float sqrMagnitude => 0;
        public Vector2 normalized => this;
        public static float Distance(Vector2 a, Vector2 b) => 0;
        public static Vector2 Lerp(Vector2 a, Vector2 b, float t) => a;
        public static Vector2 operator +(Vector2 a, Vector2 b) => a;
        public static Vector2 operator -(Vector2 a, Vector2 b) => a;
        public static Vector2 operator -(Vector2 a) => a;
        public static Vector2 operator *(Vector2 a, float d) => a;
        public static Vector2 operator *(float d, Vector2 a) => a;
        public static Vector2 operator /(Vector2 a, float d) => a;
        public static implicit operator Vector3(Vector2 v) => default;
        public static implicit operator Vector2(Vector3 v) => default;
        public static implicit operator Vector4(Vector2 v) => default;
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public Vector3(float x, float y) { this.x = x; this.y = y; z = 0; }
        public static Vector3 zero => default;
        public static Vector3 one => default;
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => a;
        public static float Distance(Vector3 a, Vector3 b) => 0;
        public static Vector3 operator +(Vector3 a, Vector3 b) => a;
        public static Vector3 operator -(Vector3 a, Vector3 b) => a;
        public static Vector3 operator *(Vector3 a, float d) => a;
        public static Vector3 operator /(Vector3 a, float d) => a;
    }

    public struct Vector4
    {
        public float x, y, z, w;
        public Vector4(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
    }

    public struct Quaternion
    {
        public static Quaternion identity => default;
        public static Quaternion Euler(float x, float y, float z) => default;
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public Color(float r, float g, float b) { this.r = r; this.g = g; this.b = b; a = 1; }
        public static Color white => default;
        public static Color black => default;
        public static Color clear => default;
        public static Color operator *(Color a, Color b) => a;
        public static Color operator *(Color a, float b) => a;
        public static implicit operator Color32(Color c) => default;
    }

    public struct Color32
    {
        public byte r, g, b, a;
        public Color32(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static implicit operator Color(Color32 c) => default;
    }

    public struct Rect
    {
        public Rect(float x, float y, float width, float height) { this.x = x; this.y = y; this.width = width; this.height = height; }
        public float x, y, width, height;
        public float xMin => x;
        public float yMin => y;
        public float xMax => x + width;
        public float yMax => y + height;
        public Vector2 center => default;
    }

    public struct Vector2Int { public int x, y; }

    public static class Mathf
    {
        public const float PI = 3.14159274f;
        public static float Clamp(float v, float a, float b) => v;
        public static int Clamp(int v, int a, int b) => v;
        public static float Clamp01(float v) => v;
        public static float Max(float a, float b) => a;
        public static int Max(int a, int b) => a;
        public static float Min(float a, float b) => a;
        public static int Min(int a, int b) => a;
        public static float Abs(float v) => v;
        public static float Sqrt(float v) => v;
        public static float Sin(float v) => v;
        public static float Cos(float v) => v;
        public static float Lerp(float a, float b, float t) => a;
        public static int RoundToInt(float v) => 0;
    }

    public static class Time
    {
        public static float deltaTime => 0;
        public static float unscaledDeltaTime => 0;
        public static float time => 0;
        public static float realtimeSinceStartup => 0;
        public static double realtimeSinceStartupAsDouble => 0;
    }

    public static class Debug
    {
        public static void Log(object m) { }
        public static void LogWarning(object m) { }
        public static void LogError(object m) { }
    }

    public enum UserAuthorization { WebCam = 1, Microphone = 2 }

    public static class Application
    {
        public static string persistentDataPath => "";
        public static string dataPath => "";
        public static string streamingAssetsPath => "";
        public static string version => "";
        public static int targetFrameRate { get; set; }
        public static AsyncOperation RequestUserAuthorization(UserAuthorization mode) => null;
        public static bool HasUserAuthorization(UserAuthorization mode) => true;
    }

    public static class SleepTimeout { public const int NeverSleep = -1; public const int SystemSetting = -2; }

    public static class Screen
    {
        public static int sleepTimeout { get; set; }
        public static int width => 0;
        public static int height => 0;
    }

    public static class Handheld { public static void Vibrate() { } }

    public static class SystemInfo { public static string deviceModel => ""; public static bool supportsAsyncGPUReadback => true; }

    public static class PlayerPrefs
    {
        public static string GetString(string k, string d) => d;
        public static void SetString(string k, string v) { }
        public static int GetInt(string k, int d) => d;
        public static void SetInt(string k, int v) { }
        public static float GetFloat(string k, float d) => d;
        public static void SetFloat(string k, float v) { }
        public static void Save() { }
    }

    public static class Resources
    {
        public static T Load<T>(string path) where T : Object => null;
        public static T[] LoadAll<T>(string path) where T : Object => null;
        public static T GetBuiltinResource<T>(string path) where T : Object => null;
    }

    public class TextAsset : Object
    {
        public string text => "";
        public byte[] bytes => null;
    }

    public class Font : Object { }
    public enum FontStyle { Normal, Bold, Italic, BoldAndItalic }
    public enum TextAnchor { UpperLeft, UpperCenter, UpperRight, MiddleLeft, MiddleCenter, MiddleRight, LowerLeft, LowerCenter, LowerRight }
    public enum HorizontalWrapMode { Wrap, Overflow }
    public enum VerticalWrapMode { Truncate, Overflow }

    public class Texture : Object
    {
        public virtual int width { get; set; }
        public virtual int height { get; set; }
        public TextureWrapMode wrapMode { get; set; }
    }

    public enum TextureWrapMode { Repeat, Clamp, Mirror, MirrorOnce }
    public enum TextureFormat { Alpha8 = 1, RGB24 = 3, RGBA32 = 4, ARGB32 = 5, BGRA32 = 14 }

    public class Texture2D : Texture
    {
        public Texture2D(int width, int height, TextureFormat textureFormat, bool mipChain) { }
        public void SetPixels32(Color32[] colors) { }
        public void Apply() { }
    }

    public enum RenderTextureFormat { ARGB32 = 0, Default = 7 }

    public class RenderTexture : Texture
    {
        public RenderTexture(int width, int height, int depth, RenderTextureFormat format) { }
        public RenderTexture(int width, int height, int depth) { }
        public bool Create() => true;
        public void Release() { }
    }

    public struct WebCamDevice
    {
        public string name => "";
        public bool isFrontFacing => false;
    }

    public sealed class WebCamTexture : Texture
    {
        public WebCamTexture(string deviceName, int requestedWidth, int requestedHeight, int requestedFPS) { }
        public static WebCamDevice[] devices => null;
        public string deviceName { get; set; }
        public bool isPlaying => false;
        public bool didUpdateThisFrame => false;
        public int videoRotationAngle => 0;
        public bool videoVerticallyMirrored => false;
        public void Play() { }
        public void Stop() { }
    }

    public class Shader : Object { public static Shader Find(string n) => null; }

    public class Material : Object
    {
        public Material(Shader shader) { }
        public void SetVector(string name, Vector4 value) { }
    }

    public static class Graphics
    {
        public static void Blit(Texture source, RenderTexture dest, Material mat) { }
        public static void Blit(Texture source, RenderTexture dest) { }
    }

    public enum SpriteMeshType { FullRect, Tight }

    public class Sprite : Object
    {
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot) => null;
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit) => null;
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit, uint extrude, SpriteMeshType meshType, Vector4 border) => null;
    }

    public sealed class AudioClip : Object
    {
        public int samples => 0;
        public bool GetData(float[] data, int offsetSamples) => true;
    }

    public sealed class Microphone
    {
        public static string[] devices => null;
        public static AudioClip Start(string deviceName, bool loop, int lengthSec, int frequency) => null;
        public static int GetPosition(string deviceName) => 0;
        public static void End(string deviceName) { }
    }

    public enum CameraClearFlags { Skybox = 1, SolidColor = 2, Depth = 3, Nothing = 4 }

    public class Camera : Behaviour
    {
        public CameraClearFlags clearFlags { get; set; }
        public Color backgroundColor { get; set; }
        public float fieldOfView { get; set; }
        public RenderTexture targetTexture { get; set; }
    }

    public enum LightType { Spot, Directional, Point }
    public class Light : Behaviour { public LightType type { get; set; } }
    public class Animator : Behaviour { }
    public class AnimationClip : Object { public float length => 0; }

    public enum RenderMode { ScreenSpaceOverlay, ScreenSpaceCamera, WorldSpace }

    public sealed class Canvas : Behaviour { public RenderMode renderMode { get; set; } }
    public sealed class CanvasRenderer : Component { }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class RequireComponent : Attribute { public RequireComponent(Type t) { } }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class CreateAssetMenuAttribute : Attribute
    {
        public string menuName { get; set; }
        public string fileName { get; set; }
        public int order { get; set; }
    }

    [AttributeUsage(AttributeTargets.Field)] public sealed class SerializeField : Attribute { }

    public enum RuntimeInitializeLoadType { AfterSceneLoad, BeforeSceneLoad }

    public class AndroidJavaObject : IDisposable
    {
        public AndroidJavaObject(string className, params object[] args) { }
        public T Call<T>(string methodName, params object[] args) => default;
        public void Call(string methodName, params object[] args) { }
        public T GetStatic<T>(string fieldName) => default;
        public void Dispose() { }
    }

    public class AndroidJavaClass : AndroidJavaObject { public AndroidJavaClass(string className) : base(className) { } }

    public class AndroidJavaProxy { public AndroidJavaProxy(string javaInterface) { } }
}

namespace UnityEngine.Android
{
    public struct Permission
    {
        public const string Camera = "android.permission.CAMERA";
        public const string Microphone = "android.permission.RECORD_AUDIO";
        public static bool HasUserAuthorizedPermission(string permission) => true;
        public static void RequestUserPermission(string permission) { }
    }
}

namespace UnityEngine.Rendering
{
    public struct AsyncGPUReadbackRequest
    {
        public bool done => true;
        public bool hasError => false;
    }
}

namespace UnityEngine.SceneManagement
{
    public struct Scene { public string name => ""; }
}

namespace UnityEngine.Events
{
    public delegate void UnityAction();
    public delegate void UnityAction<T0>(T0 arg0);

    public class UnityEvent
    {
        public void AddListener(UnityAction call) { }
        public void RemoveAllListeners() { }
        public void Invoke() { }
    }

    public class UnityEvent<T0>
    {
        public void AddListener(UnityAction<T0> call) { }
        public void RemoveAllListeners() { }
        public void Invoke(T0 a) { }
    }
}

namespace UnityEngine.EventSystems
{
    public abstract class UIBehaviour : MonoBehaviour
    {
        protected virtual void Awake() { }
        protected virtual void OnEnable() { }
        protected virtual void OnDisable() { }
        protected virtual void Start() { }
    }

    public class BaseInputModule : UIBehaviour { }
    public class StandaloneInputModule : BaseInputModule { }
    public class EventSystem : UIBehaviour { }
    public class BaseEventData { }
    public class PointerEventData : BaseEventData { }
    public interface IEventSystemHandler { }
    public interface IPointerDownHandler : IEventSystemHandler { void OnPointerDown(PointerEventData eventData); }
    public interface IPointerUpHandler : IEventSystemHandler { void OnPointerUp(PointerEventData eventData); }
}

namespace UnityEngine.Networking
{
    public interface IMultipartFormSection { }

    public class MultipartFormFileSection : IMultipartFormSection
    {
        public MultipartFormFileSection(string name, byte[] data, string fileName, string contentType) { }
    }

    public class UploadHandler : IDisposable { public string contentType { get; set; } public void Dispose() { } }
    public sealed class UploadHandlerRaw : UploadHandler { public UploadHandlerRaw(byte[] data) { } }
    public class DownloadHandler : IDisposable { public string text => ""; public byte[] data => null; public void Dispose() { } }
    public sealed class DownloadHandlerBuffer : DownloadHandler { }
    public sealed class UnityWebRequestAsyncOperation : AsyncOperation { }

    public class UnityWebRequest : IDisposable
    {
        public enum Result { InProgress, Success, ConnectionError, ProtocolError, DataProcessingError }
        public UnityWebRequest(string url, string method) { }
        public static UnityWebRequest Get(string uri) => null;
        public static UnityWebRequest Post(string uri, List<IMultipartFormSection> multipartFormSections) => null;
        public static string EscapeURL(string s) => s;
        public UploadHandler uploadHandler { get; set; }
        public DownloadHandler downloadHandler { get; set; }
        public int timeout { get; set; }
        public Result result => Result.Success;
        public string error => "";
        public long responseCode => 0;
        public void SetRequestHeader(string name, string value) { }
        public UnityWebRequestAsyncOperation SendWebRequest() => null;
        public void Dispose() { }
    }
}

namespace UnityEngine.Playables
{
    public struct PlayableGraph
    {
        public static PlayableGraph Create(string name) => default;
        public void Play() { }
        public void Destroy() { }
        public bool IsValid() => true;
        public bool Connect<U, V>(U source, int sourceOutputPort, V destination, int destinationInputPort)
            where U : struct, IPlayable where V : struct, IPlayable => true;
        public void Disconnect<U>(U input, int inputPort) where U : struct, IPlayable { }
    }

    public interface IPlayable { }
    public interface IPlayableOutput { }

    public struct Playable : IPlayable { }

    public static class PlayableExtensions
    {
        public static bool IsValid<U>(this U playable) where U : struct, IPlayable => true;
        public static void Destroy<U>(this U playable) where U : struct, IPlayable { }
        public static Playable GetInput<U>(this U playable, int inputPort) where U : struct, IPlayable => default;
        public static void SetInputWeight<U>(this U playable, int inputIndex, float weight) where U : struct, IPlayable { }
        public static void SetSpeed<U>(this U playable, double value) where U : struct, IPlayable { }
        public static void SetTime<U>(this U playable, double value) where U : struct, IPlayable { }
        public static double GetTime<U>(this U playable) where U : struct, IPlayable => 0;
    }

    public static class PlayableOutputExtensions
    {
        public static void SetSourcePlayable<U, V>(this U output, V value) where U : struct, IPlayableOutput where V : struct, IPlayable { }
    }
}

namespace UnityEngine.Animations
{
    using UnityEngine.Playables;

    public struct AnimationPlayableOutput : IPlayableOutput
    {
        public static AnimationPlayableOutput Create(PlayableGraph graph, string name, Animator target) => default;
    }

    public struct AnimationMixerPlayable : IPlayable
    {
        public static AnimationMixerPlayable Create(PlayableGraph graph, int inputCount) => default;
    }

    public struct AnimationClipPlayable : IPlayable
    {
        public static AnimationClipPlayable Create(PlayableGraph graph, AnimationClip clip) => default;
        public AnimationClip GetAnimationClip() => null;
    }
}

namespace UnityEngine.UI
{
    using UnityEngine.EventSystems;
    using UnityEngine.Events;

    public struct UIVertex
    {
        public Vector3 position;
        public Color32 color;
        public static UIVertex simpleVert => default;
    }

    public class VertexHelper : IDisposable
    {
        public int currentVertCount => 0;
        public void Clear() { }
        public void AddVert(UIVertex v) { }
        public void AddTriangle(int a, int b, int c) { }
        public void Dispose() { }
    }

    public abstract class Graphic : UIBehaviour
    {
        public virtual Color color { get; set; }
        public virtual bool raycastTarget { get; set; }
        public RectTransform rectTransform => null;
        public Rect GetPixelAdjustedRect() => default;
        public virtual void SetVerticesDirty() { }
        protected virtual void OnPopulateMesh(VertexHelper vh) { }
    }

    public abstract class MaskableGraphic : Graphic { }

    public class Image : MaskableGraphic
    {
        public enum Type { Simple, Sliced, Tiled, Filled }
        public Sprite sprite { get; set; }
        public Type type { get; set; }
        public float pixelsPerUnitMultiplier { get; set; }
    }

    public class RawImage : MaskableGraphic
    {
        public Texture texture { get; set; }
        public Rect uvRect { get; set; }
    }

    public class Text : MaskableGraphic
    {
        public Font font { get; set; }
        public virtual string text { get; set; }
        public int fontSize { get; set; }
        public TextAnchor alignment { get; set; }
        public FontStyle fontStyle { get; set; }
        public HorizontalWrapMode horizontalOverflow { get; set; }
        public VerticalWrapMode verticalOverflow { get; set; }
        public bool supportRichText { get; set; }
        public bool resizeTextForBestFit { get; set; }
        public int resizeTextMinSize { get; set; }
        public int resizeTextMaxSize { get; set; }
        public float lineSpacing { get; set; }
    }

    public struct ColorBlock
    {
        public Color normalColor { get; set; }
        public Color highlightedColor { get; set; }
        public Color pressedColor { get; set; }
        public Color selectedColor { get; set; }
        public Color disabledColor { get; set; }
    }

    public class Selectable : UIBehaviour
    {
        public Graphic targetGraphic { get; set; }
        public ColorBlock colors { get; set; }
        public bool interactable { get; set; }
    }

    public class Button : Selectable
    {
        public class ButtonClickedEvent : UnityEvent { }
        public ButtonClickedEvent onClick { get; set; }
    }

    public class Toggle : Selectable
    {
        public class ToggleEvent : UnityEvent<bool> { }
        public Graphic graphic;
        public ToggleEvent onValueChanged = new ToggleEvent();
        public bool isOn { get; set; }
        public void SetIsOnWithoutNotify(bool value) { }
    }

    public class InputField : Selectable
    {
        public enum ContentType { Standard, Autocorrected, IntegerNumber, DecimalNumber, Alphanumeric, Name, EmailAddress, Password, Pin, Custom }
        public enum LineType { SingleLine, MultiLineSubmit, MultiLineNewline }
        public class SubmitEvent : UnityEvent<string> { }
        public class OnChangeEvent : UnityEvent<string> { }
        public string text { get; set; }
        public Text textComponent { get; set; }
        public Graphic placeholder { get; set; }
        public ContentType contentType { get; set; }
        public LineType lineType { get; set; }
        public SubmitEvent onEndEdit { get; set; }
        public OnChangeEvent onValueChanged { get; set; }
    }

    public class Dropdown : Selectable
    {
        public class OptionData { public OptionData(string text) { } }
        public class DropdownEvent : UnityEvent<int> { }
        public RectTransform template { get; set; }
        public int value { get; set; }
        public DropdownEvent onValueChanged { get; set; }
        public void ClearOptions() { }
        public void AddOptions(List<string> options) { }
        public void SetValueWithoutNotify(int input) { }
    }

    public class Slider : Selectable
    {
        public class SliderEvent : UnityEvent<float> { }
        public float minValue { get; set; }
        public float maxValue { get; set; }
        public float value { get; set; }
        public SliderEvent onValueChanged { get; set; }
        public void SetValueWithoutNotify(float input) { }
    }

    public class ScrollRect : UIBehaviour
    {
        public enum MovementType { Unrestricted, Elastic, Clamped }
        public RectTransform content { get; set; }
        public RectTransform viewport { get; set; }
        public bool horizontal { get; set; }
        public bool vertical { get; set; }
        public MovementType movementType { get; set; }
        public float scrollSensitivity { get; set; }
    }

    public class RectMask2D : UIBehaviour { }
    public class GraphicRaycaster : UIBehaviour { }

    public class CanvasScaler : UIBehaviour
    {
        public enum ScaleMode { ConstantPixelSize, ScaleWithScreenSize, ConstantPhysicalSize }
        public enum ScreenMatchMode { MatchWidthOrHeight, Expand, Shrink }
        public ScaleMode uiScaleMode { get; set; }
        public Vector2 referenceResolution { get; set; }
        public ScreenMatchMode screenMatchMode { get; set; }
        public float matchWidthOrHeight { get; set; }
    }

    public class LayoutElement : UIBehaviour
    {
        public float minHeight { get; set; }
        public float minWidth { get; set; }
        public float preferredHeight { get; set; }
        public float preferredWidth { get; set; }
        public float flexibleWidth { get; set; }
        public float flexibleHeight { get; set; }
    }

    public abstract class LayoutGroup : UIBehaviour
    {
        public RectOffset padding { get; set; }
        public TextAnchor childAlignment { get; set; }
    }

    public abstract class HorizontalOrVerticalLayoutGroup : LayoutGroup
    {
        public float spacing { get; set; }
        public bool childForceExpandWidth { get; set; }
        public bool childForceExpandHeight { get; set; }
        public bool childControlWidth { get; set; }
        public bool childControlHeight { get; set; }
    }

    public class VerticalLayoutGroup : HorizontalOrVerticalLayoutGroup { }
    public class HorizontalLayoutGroup : HorizontalOrVerticalLayoutGroup { }

    public class ContentSizeFitter : UIBehaviour
    {
        public enum FitMode { Unconstrained, MinSize, PreferredSize }
        public FitMode horizontalFit { get; set; }
        public FitMode verticalFit { get; set; }
    }

    public class AspectRatioFitter : UIBehaviour
    {
        public enum AspectMode { None, WidthControlsHeight, HeightControlsWidth, FitInParent, EnvelopeParent }
        public AspectMode aspectMode { get; set; }
        public float aspectRatio { get; set; }
    }

    public static class DefaultControls
    {
        public struct Resources
        {
            public Sprite standard, background, inputField, knob, checkmark, dropdown, mask;
        }
        public static GameObject CreateDropdown(Resources resources) => null;
        public static GameObject CreateSlider(Resources resources) => null;
    }
}

namespace UnityEngine
{
    public class RectOffset { public RectOffset(int left, int right, int top, int bottom) { } }
}
