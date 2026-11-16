using System.Collections.Generic;
using System.IO;
using LSRD.App;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LSRD.Editor
{
    /// <summary>Menú «LSRD»: deja el proyecto listo para ejecutar y compilar en Android/iOS.</summary>
    public static class PreparadorProyecto
    {
        const string RutaEscena = "Assets/LSRD/Escenas/LSRD.unity";
        const string CarpetaRecursos = "Assets/LSRD/Resources/LSRD";

        [MenuItem("LSRD/Preparar proyecto", priority = 1)]
        public static void Preparar()
        {
            var informe = new List<string>
            {
                CopiarModeloMediaPipe(),
                CopiarVocabulario(),
                ConfigurarJugador(),
                CrearEscena(),
            };
            var texto = string.Join("\n\n", informe);
            Debug.Log("[LSRD] " + texto);
            EditorUtility.DisplayDialog("LSRD · Preparar proyecto", texto, "Aceptar");
        }

        [MenuItem("LSRD/Abrir escena", priority = 2)]
        public static void AbrirEscena()
        {
            if (!File.Exists(RutaEscena)) CrearEscena();
            EditorSceneManager.OpenScene(RutaEscena);
        }

        [MenuItem("LSRD/Copiar vocabulario del repositorio", priority = 20)]
        public static void CopiarVocabularioMenu() => EditorUtility.DisplayDialog("LSRD", CopiarVocabulario(), "Aceptar");

        [MenuItem("LSRD/Abrir carpeta de datos de la app", priority = 21)]
        public static void AbrirDatos() => EditorUtility.RevealInFinder(Application.persistentDataPath);

        static string CopiarModeloMediaPipe()
        {
#if LSRD_MEDIAPIPE
            const string origen = "Packages/com.github.homuler.mediapipe/PackageResources/MediaPipe/holistic_landmarker.bytes";
            var modelo = AssetDatabase.LoadAssetAtPath<TextAsset>(origen);
            if (modelo == null) return "⚠ No se encontró holistic_landmarker.bytes en el paquete de MediaPipe. Use el paquete .tgz de las versiones publicadas (trae los modelos).";
            var carpeta = CarpetaRecursos + "/MediaPipe";
            Directory.CreateDirectory(carpeta);
            File.WriteAllBytes(carpeta + "/holistic_landmarker.bytes", modelo.bytes);
            AssetDatabase.ImportAsset(carpeta + "/holistic_landmarker.bytes");
            return "✔ Modelo holístico de MediaPipe copiado a Resources.";
#else
            return "⚠ MediaPipe Unity Plugin no está instalado. Descargue com.github.homuler.mediapipe-0.16.3.tgz de " +
                   "github.com/homuler/MediaPipeUnityPlugin/releases y agréguelo con Package Manager › + › Add package from tarball. " +
                   "Mientras tanto la app usa el detector simulado.";
#endif
        }

        static readonly string[] ArchivosVocabulario =
            { "vocabulario_mvp.csv", "frases_mvp.csv", "emergencias.csv", "cobertura_diccionario_oficial.csv" };

        static string CopiarVocabulario()
        {
            var raizRepo = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
            var origen = Path.Combine(raizRepo, "vocabulario");
            var destino = CarpetaRecursos + "/Datos";
            Directory.CreateDirectory(destino);
            int copiados = 0;
            foreach (var nombre in ArchivosVocabulario)
            {
                var archivo = Path.Combine(origen, nombre);
                if (!File.Exists(archivo)) continue;
                File.Copy(archivo, Path.Combine(destino, nombre), true);
                copiados++;
            }
            AssetDatabase.Refresh();
            return copiados == ArchivosVocabulario.Length
                ? "✔ Vocabulario y frases copiados desde la carpeta vocabulario/ del repositorio."
                : "⚠ No se encontró la carpeta vocabulario/ del repositorio; se usa la copia que ya está en Resources.";
        }

        static string ConfigurarJugador()
        {
            PlayerSettings.companyName = "LSRD";
            PlayerSettings.productName = "LSRD Atención";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            // Servidor en la red local durante el piloto. En producción use HTTPS y cambie a NotAllowed.
            PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;

            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;

            PlayerSettings.iOS.cameraUsageDescription = "La cámara lee la lengua de señas para traducirla.";
            PlayerSettings.iOS.microphoneUsageDescription = "El micrófono escucha al empleado para que el avatar lo traduzca a señas.";
            return "✔ Ajustes del reproductor: IL2CPP/ARM64, Android 8+, permisos de cámara y micrófono, HTTP local permitido.\n" +
                   "Para Android incluya libc++_shared.so según el README del plugin de MediaPipe.";
        }

        static string CrearEscena()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(RutaEscena));
            var escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Camara principal").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.tag = "MainCamera";
            new GameObject("LSRD").AddComponent<ControladorApp>();
            EditorSceneManager.SaveScene(escena, RutaEscena);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(RutaEscena, true) };
            return "✔ Escena creada en " + RutaEscena + " y agregada a la compilación. Pulse Play para probar.";
        }
    }
}
