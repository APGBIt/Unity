using System.Collections;
using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace LSRD.App
{
    /// <summary>Solicitud de permisos de cámara y micrófono en Android e iOS.</summary>
    public static class Permisos
    {
        public static bool Camara
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return Permission.HasUserAuthorizedPermission(Permission.Camera);
#elif UNITY_IOS && !UNITY_EDITOR
                return Application.HasUserAuthorization(UserAuthorization.WebCam);
#else
                return true;
#endif
            }
        }

        public static bool Microfono
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return Permission.HasUserAuthorizedPermission(Permission.Microphone);
#elif UNITY_IOS && !UNITY_EDITOR
                return Application.HasUserAuthorization(UserAuthorization.Microphone);
#else
                return true;
#endif
            }
        }

        public static IEnumerator SolicitarCamara()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                Permission.RequestUserPermission(Permission.Camera);
                yield return Esperar(() => Permission.HasUserAuthorizedPermission(Permission.Camera));
            }
#elif UNITY_IOS && !UNITY_EDITOR
            yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
#endif
            yield break;
        }

        public static IEnumerator SolicitarMicrofono()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                Permission.RequestUserPermission(Permission.Microphone);
                yield return Esperar(() => Permission.HasUserAuthorizedPermission(Permission.Microphone));
            }
#elif UNITY_IOS && !UNITY_EDITOR
            yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
#endif
            yield break;
        }

        static IEnumerator Esperar(System.Func<bool> condicion, float maximoS = 20f)
        {
            float t = 0f;
            while (!condicion() && t < maximoS)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }
    }
}
