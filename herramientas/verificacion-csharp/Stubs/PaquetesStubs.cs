// Firmas de los paquetes externos (MediaPipe Unity Plugin 0.16.3, Unity Inference Engine 2.6,
// Input System) y del editor de Unity, transcritas de su código fuente y documentación.
#pragma warning disable CS0067, CS0649, CS1591
using System;
using System.Collections;
using System.Collections.Generic;

namespace Mediapipe
{
    public class Image : IDisposable
    {
        public void Dispose() { }
    }

    public class GpuResources { }
    public class GlSyncPoint { }

    public static class Protobuf
    {
        public delegate void LogHandler(int level, string filename, int line, string message);
        public static readonly LogHandler DefaultLogHandler = null;
        public static void SetLogHandler(LogHandler logHandler) { }
    }

    public static class Glog
    {
        public static void Initialize(string name) { }
        public static void Shutdown() { }
    }
}

namespace Mediapipe.Unity
{
    public static class GpuManager
    {
        public static GpuResources GpuResources { get; private set; }
        public static bool IsInitialized { get; private set; }
        public static IEnumerator Initialize() { yield break; }
    }
}

namespace Mediapipe.Unity.Experimental
{
    public class TextureFrame : IDisposable
    {
        public void ReadTextureOnCPU(UnityEngine.Texture src, bool flipHorizontally = false, bool flipVertically = false) { }
        public UnityEngine.Rendering.AsyncGPUReadbackRequest ReadTextureAsync(UnityEngine.Texture src, bool flipHorizontally = false, bool flipVertically = false) => default;
        public Image BuildCPUImage() => null;
        public void Release(GlSyncPoint token = null) { }
        public void Dispose() { }
    }

    public class TextureFramePool : IDisposable
    {
        public readonly int textureWidth;
        public readonly int textureHeight;
        public TextureFramePool(int textureWidth, int textureHeight, UnityEngine.TextureFormat textureFormat = UnityEngine.TextureFormat.RGBA32, int poolSize = 10) { }
        public bool TryGetTextureFrame(out TextureFrame outFrame) { outFrame = null; return false; }
        public void Dispose() { }
    }
}

namespace Mediapipe.Tasks.Core
{
    public sealed class BaseOptions
    {
        public enum Delegate { CPU = 0, GPU = 1, EDGETPU_NNAPI = 2 }
        public BaseOptions(Delegate delegateCase = Delegate.CPU, string modelAssetPath = null, byte[] modelAssetBuffer = null) { }
    }
}

namespace Mediapipe.Tasks.Vision.Core
{
    public enum RunningMode { IMAGE = 1, VIDEO = 2, LIVE_STREAM = 3 }

    public class BaseVisionTaskApi : IDisposable
    {
        public void Close() { }
        public void Dispose() { }
    }
}

namespace Mediapipe.Tasks.Components.Containers
{
    public readonly struct NormalizedLandmark
    {
        public readonly float x;
        public readonly float y;
        public readonly float z;
        public readonly float? visibility;
        public readonly float? presence;
    }

    public readonly struct NormalizedLandmarks
    {
        public readonly List<NormalizedLandmark> landmarks;
    }

    public readonly struct Landmarks { }
    public readonly struct Classifications { }
}

namespace Mediapipe.Tasks.Vision.HolisticLandmarker
{
    using Mediapipe.Tasks.Components.Containers;

    public readonly struct HolisticLandmarkerResult
    {
        public readonly NormalizedLandmarks faceLandmarks;
        public readonly NormalizedLandmarks poseLandmarks;
        public readonly Landmarks poseWorldLandmarks;
        public readonly NormalizedLandmarks leftHandLandmarks;
        public readonly Landmarks leftHandWorldLandmarks;
        public readonly NormalizedLandmarks rightHandLandmarks;
        public readonly Landmarks rightHandWorldLandmarks;
        public readonly Classifications faceBlendshapes;
        public readonly Image segmentationMask;
    }

    public sealed class HolisticLandmarkerOptions
    {
        public delegate void ResultCallback(in HolisticLandmarkerResult holisticLandmarkerResult, Image image, long timestampMillisec);

        public HolisticLandmarkerOptions(
            Tasks.Core.BaseOptions baseOptions,
            Vision.Core.RunningMode runningMode = Vision.Core.RunningMode.IMAGE,
            float minFaceDetectionConfidence = 0.5f,
            float minFaceSuppressionThreshold = 0.5f,
            float minFaceLandmarksConfidence = 0.5f,
            float minPoseDetectionConfidence = 0.5f,
            float minPoseSuppressionThreshold = 0.5f,
            float minPoseLandmarksConfidence = 0.5f,
            float minHandLandmarksConfidence = 0.5f,
            bool outputFaceBlendshapes = false,
            bool outputSegmentationMask = false,
            ResultCallback resultCallback = null)
        { }
    }

    public sealed class HolisticLandmarker : Vision.Core.BaseVisionTaskApi
    {
        public static HolisticLandmarker CreateFromOptions(HolisticLandmarkerOptions options, GpuResources gpuResources = null) => null;
        public void DetectAsync(Image image, long timestampMillisec) { }
    }
}

namespace Unity.InferenceEngine
{
    public enum BackendType { GPUCompute = 0, CPU = 1, GPUPixel = 2 }

    public class ModelAsset : UnityEngine.ScriptableObject { }
    public class Model { }

    public static class ModelLoader
    {
        public static Model Load(ModelAsset modelAsset) => null;
    }

    public struct TensorShape
    {
        public TensorShape(int d2, int d1, int d0) { }
        public TensorShape(int d3, int d2, int d1, int d0) { }
    }

    public abstract class Tensor : IDisposable
    {
        public void Dispose() { }
    }

    public class Tensor<T> : Tensor where T : unmanaged
    {
        public Tensor(TensorShape shape, T[] srcData, int dataStart = 0) { }
        public Tensor(TensorShape shape, bool clearOnInit = true) { }
        public T[] DownloadToArray() => null;
    }

    public class Worker : IDisposable
    {
        public Worker(Model model, BackendType backendType) { }
        public void Schedule(params Tensor[] inputs) { }
        public Tensor PeekOutput() => null;
        public Tensor PeekOutput(string name) => null;
        public void Dispose() { }
    }
}

namespace UnityEngine.InputSystem.UI
{
    public class InputSystemUIInputModule : UnityEngine.EventSystems.BaseInputModule { }
}

namespace UnityEditor
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class MenuItem : Attribute
    {
        public MenuItem(string itemName) { }
        public int priority;
    }

    public static class EditorUtility
    {
        public static bool DisplayDialog(string title, string message, string ok) => true;
        public static void RevealInFinder(string path) { }
    }

    public static class AssetDatabase
    {
        public static T LoadAssetAtPath<T>(string path) where T : UnityEngine.Object => null;
        public static void ImportAsset(string path) { }
        public static void Refresh() { }
    }

    public enum UIOrientation { Portrait, PortraitUpsideDown, LandscapeRight, LandscapeLeft, AutoRotation }
    public enum InsecureHttpOption { NotAllowed, DevelopmentOnly, AlwaysAllowed }
    public enum ScriptingImplementation { Mono2x, IL2CPP }
    [Flags] public enum AndroidArchitecture { None = 0, ARMv7 = 1, ARM64 = 2, X86_64 = 8 }
    public enum AndroidSdkVersions { AndroidApiLevelAuto = 0, AndroidApiLevel23 = 23, AndroidApiLevel24 = 24, AndroidApiLevel25 = 25, AndroidApiLevel26 = 26 }

    public static class PlayerSettings
    {
        public static string companyName { get; set; }
        public static string productName { get; set; }
        public static UIOrientation defaultInterfaceOrientation { get; set; }
        public static bool allowedAutorotateToPortrait { get; set; }
        public static bool allowedAutorotateToPortraitUpsideDown { get; set; }
        public static bool allowedAutorotateToLandscapeLeft { get; set; }
        public static bool allowedAutorotateToLandscapeRight { get; set; }
        public static InsecureHttpOption insecureHttpOption { get; set; }
        public static void SetScriptingBackend(Build.NamedBuildTarget buildTarget, ScriptingImplementation backend) { }

        public static class Android
        {
            public static AndroidArchitecture targetArchitectures { get; set; }
            public static AndroidSdkVersions minSdkVersion { get; set; }
        }

        public static class iOS
        {
            public static string cameraUsageDescription { get; set; }
            public static string microphoneUsageDescription { get; set; }
        }
    }

    public class EditorBuildSettingsScene
    {
        public EditorBuildSettingsScene(string path, bool enabled) { }
    }

    public static class EditorBuildSettings
    {
        public static EditorBuildSettingsScene[] scenes { get; set; }
    }
}

namespace UnityEditor.Build
{
    public readonly struct NamedBuildTarget
    {
        public static readonly NamedBuildTarget Android = default;
        public static readonly NamedBuildTarget iOS = default;
    }
}

namespace UnityEditor.SceneManagement
{
    public enum NewSceneSetup { EmptyScene, DefaultGameObjects }
    public enum NewSceneMode { Single, Additive }
    public enum OpenSceneMode { Single, Additive, AdditiveWithoutLoading }

    public static class EditorSceneManager
    {
        public static UnityEngine.SceneManagement.Scene NewScene(NewSceneSetup setup, NewSceneMode mode) => default;
        public static bool SaveScene(UnityEngine.SceneManagement.Scene scene, string dstScenePath) => true;
        public static UnityEngine.SceneManagement.Scene OpenScene(string scenePath, OpenSceneMode mode = OpenSceneMode.Single) => default;
    }
}
