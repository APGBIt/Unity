using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using LSRD.Nucleo;
using UnityEngine;

namespace LSRD.App
{
    /// <summary>Fuente de puntos clave: el detector real (MediaPipe) o un simulador para desarrollar sin cámara.</summary>
    public interface IDetectorLandmarks
    {
        bool Listo { get; }
        string Estado { get; }
        /// <summary>Se invoca en el hilo principal con cada cuadro detectado.</summary>
        event Action<CuadroLandmarks> AlCuadro;
        IEnumerator Iniciar(FuenteCamara camara, AjustesApp ajustes);
        void Detener();
    }
}
