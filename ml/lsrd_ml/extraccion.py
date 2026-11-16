"""Extracción de puntos clave desde videos con MediaPipe Tasks (Python).

La app Unity usa el detector holístico de MediaPipe, que entrega pose, manos
izquierda/derecha y cara en una sola pasada. La versión de MediaPipe para
Python que soporta este proyecto no incluye el detector holístico, así que aquí
se combinan los detectores de pose, manos y cara y se asigna cada mano al lado
del cuerpo por cercanía a la muñeca de la pose (la misma regla del holístico).

Para el entrenamiento se recomienda grabar con el modo recolección de la app:
los datos salen idénticos a los del uso real. Este extractor sirve para videos
grabados con otros medios (con consentimiento).

Modelos necesarios (descargar una vez de https://ai.google.dev/edge/mediapipe):
``pose_landmarker_full.task``, ``hand_landmarker.task``, ``face_landmarker.task``.
"""

from __future__ import annotations

from pathlib import Path
from typing import Optional, Sequence

import numpy as np

from . import formato as F

UMBRAL_VISIBILIDAD_POSE = 0.5


def empaquetar(
    pose: Optional[Sequence],
    mano_izq: Optional[Sequence],
    mano_der: Optional[Sequence],
    cara: Optional[Sequence],
    aspecto: float,
) -> np.ndarray:
    """Arma un cuadro ``[78, 3]`` en unidades corregidas por aspecto.

    Cada punto de entrada debe tener ``x, y, z`` normalizados (0–1) y, para la
    pose, ``visibility``. La app hace lo mismo en ``EmpaquetadorLandmarks.cs``.
    """
    out = np.zeros((F.N_PUNTOS, 3), dtype=np.float32)

    def poner(destino: int, p) -> None:
        out[destino, 0] = float(p.x) * aspecto
        out[destino, 1] = float(p.y)
        out[destino, 2] = float(p.z) * aspecto

    if pose is not None and len(pose) >= 33:
        for k, idx in enumerate(F.POSE_INDICES_MEDIAPIPE):
            p = pose[idx]
            vis = getattr(p, "visibility", None)
            if vis is None or vis >= UMBRAL_VISIBILIDAD_POSE:
                poner(F.INICIO_POSE + k, p)
    for inicio, mano in ((F.INICIO_MANO_IZQ, mano_izq), (F.INICIO_MANO_DER, mano_der)):
        if mano is not None and len(mano) >= F.PUNTOS_MANO:
            for k in range(F.PUNTOS_MANO):
                poner(inicio + k, mano[k])
    if cara is not None and len(cara) >= 468:
        for k, idx in enumerate(F.CARA_INDICES_MEDIAPIPE):
            poner(F.INICIO_CARA + k, cara[idx])
    return out


def asignar_manos(manos: list, pose: Optional[Sequence], etiquetas: list[str]) -> tuple[Optional[Sequence], Optional[Sequence]]:
    """Asigna cada mano detectada a izquierda/derecha de la persona.

    Con pose: por cercanía a las muñecas de la pose (15 = izquierda, 16 = derecha).
    Sin pose: por la etiqueta de MediaPipe, que asume imagen en espejo, así que
    "Left" corresponde a la mano derecha de la persona en un video normal.
    """
    izq = der = None
    if not manos:
        return None, None
    if pose is not None and len(pose) >= 33:
        mi, md = pose[15], pose[16]

        def d(mano, p) -> float:
            return (mano[0].x - p.x) ** 2 + (mano[0].y - p.y) ** 2

        if len(manos) == 1:
            m = manos[0]
            return (m, None) if d(m, mi) <= d(m, md) else (None, m)
        a, b = manos[0], manos[1]
        if d(a, mi) + d(b, md) <= d(a, md) + d(b, mi):
            return a, b
        return b, a
    for mano, etiqueta in zip(manos, etiquetas):
        if etiqueta == "Left":
            der = der or mano
        else:
            izq = izq or mano
    return izq, der


class ExtractorMediaPipe:
    def __init__(self, carpeta_modelos: Path, usar_cara: bool = True) -> None:
        import mediapipe as mp

        v = mp.tasks.vision
        base = mp.tasks.BaseOptions
        carpeta = Path(carpeta_modelos)
        modo = v.RunningMode.VIDEO
        self._mp = mp
        self.pose = v.PoseLandmarker.create_from_options(v.PoseLandmarkerOptions(
            base_options=base(model_asset_path=str(carpeta / "pose_landmarker_full.task")), running_mode=modo))
        self.manos = v.HandLandmarker.create_from_options(v.HandLandmarkerOptions(
            base_options=base(model_asset_path=str(carpeta / "hand_landmarker.task")), running_mode=modo,
            num_hands=2))
        self.cara = None
        if usar_cara:
            self.cara = v.FaceLandmarker.create_from_options(v.FaceLandmarkerOptions(
                base_options=base(model_asset_path=str(carpeta / "face_landmarker.task")), running_mode=modo))

    def procesar(self, rgb: np.ndarray, t_ms: int) -> np.ndarray:
        alto, ancho = rgb.shape[:2]
        imagen = self._mp.Image(image_format=self._mp.ImageFormat.SRGB, data=np.ascontiguousarray(rgb))
        rp = self.pose.detect_for_video(imagen, t_ms)
        rm = self.manos.detect_for_video(imagen, t_ms)
        pose = rp.pose_landmarks[0] if rp.pose_landmarks else None
        etiquetas = [h[0].category_name for h in rm.handedness] if rm.handedness else []
        izq, der = asignar_manos(list(rm.hand_landmarks or []), pose, etiquetas)
        cara = None
        if self.cara is not None:
            rf = self.cara.detect_for_video(imagen, t_ms)
            cara = rf.face_landmarks[0] if rf.face_landmarks else None
        return empaquetar(pose, izq, der, cara, ancho / alto)

    def cerrar(self) -> None:
        for d in (self.pose, self.manos, self.cara):
            if d is not None:
                d.close()


def extraer_video(ruta: Path, extractor: ExtractorMediaPipe) -> tuple[np.ndarray, np.ndarray]:
    """Devuelve ``(cuadros [L, 78, 3], tiempos_ms [L])`` de un video."""
    import cv2

    cap = cv2.VideoCapture(str(ruta))
    fps = cap.get(cv2.CAP_PROP_FPS) or 30.0
    cuadros, tiempos = [], []
    i = 0
    while True:
        ok, bgr = cap.read()
        if not ok:
            break
        t = int(round(i * 1000.0 / fps))
        cuadros.append(extractor.procesar(cv2.cvtColor(bgr, cv2.COLOR_BGR2RGB), t))
        tiempos.append(t)
        i += 1
    cap.release()
    if not cuadros:
        raise ValueError(f"No se pudo leer ningún cuadro de {ruta}")
    return np.stack(cuadros), np.asarray(tiempos, dtype=np.float32)
