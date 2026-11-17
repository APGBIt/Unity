# Canal de entrenamiento (Python)

Formato de datos, segmentación, modelos, entrenamiento, evaluación y exportación a ONNX para la app.

```bash
pip install -r requirements.txt
python -m pytest -q
```

## Scripts

| Script | Qué hace |
| --- | --- |
| `scripts/demo_sintetico.py --unity` | Genera datos sintéticos, entrena ambos modelos, evalúa con firmantes no vistos y exporta los modelos de **demostración** a la app. Tarda ~1 minuto en CPU. |
| `scripts/entrenar.py --datos <carpeta> --salida modelos/v1 --unity` | Entrenamiento real con las grabaciones del modo recolección (`*.json`) o de videos (`*.npz`). Excluye clases con menos de 10 tomas o menos de 3 firmantes y escribe reportes en Markdown. |
| `scripts/videos.py` | Canal de videos: `fuentes` (qué fuentes tienen permiso), `recolectar` (descarga e inventario), `proponer` (corta cada video en señas y sugiere la glosa por el título) y `muestras` (anotaciones revisadas → `.npz`). |
| `scripts/entrenar.py … --semilla` | Entrena con pocas personas (videos de diccionario). Mide con tomas apartadas de las mismas personas y marca el modelo como SEMILLA. |
| `scripts/extraer_videos.py` | Convierte videos (con consentimiento) en muestras `.npz` usando MediaPipe Tasks. Necesita un manifiesto CSV y los modelos `.task` de MediaPipe. |
| `scripts/exportar_simulacion.py` | Grabaciones sintéticas para el detector simulado de la app. |
| `scripts/generar_vectores_oro.py` | Vectores de prueba para la paridad Python ↔ C# (y modelos ONNX ↔ Inference Engine). |
| `scripts/generar_csharp.py` | Regenera `FormatoLandmarks.g.cs` en la app a partir de `lsrd_ml/formato.py`. |

## Formato LSRD-LM v1

Cada cuadro tiene 78 puntos × (x, y, z): 7 de la pose (nariz, hombros, codos, muñecas), 21 por mano y 29 de la cara (boca, cejas, ojos, nariz, mentón, frente). Las unidades son "alto de imagen" (x multiplicado por ancho/alto) y un punto ausente vale 0. Detalle en `lsrd_ml/formato.py` y `documentacion/ARQUITECTURA.md`.

## Modelos

- **Señas** (`ModeloSenas`): recibe `[1, 32, 78, 3]` crudo. Dentro del modelo se normaliza (centro y ancho de hombros, forma local de cada mano, velocidades) y un Transformer de 3 capas clasifica la seña. ~1 M de parámetros.
- **Dactilología** (`ModeloDactilologia`): recibe la mano activa `[1, 21, 3]` y clasifica la letra cuadro a cuadro; la app estabiliza con votación.

Ambos se exportan con ONNX opset 15 (rango que soporta Inference Engine), con salidas `logits` y `probs`, y un `*_info.json` con las clases.

## Cómo evaluar sin engañarse

`dividir_por_firmante` separa entrenamiento, validación y prueba **por persona**: ningún firmante aparece en dos particiones. Es la única exactitud que anticipa el uso real. Los reportes incluyen exactitud por firmante de prueba, clases más débiles y confusiones frecuentes. La meta del MVP es ≥ 85 % top-1 con firmantes nuevos.

## Datos sintéticos

`lsrd_ml/sintetico.py` genera cuerpos, manos, trayectorias y expresiones para probar el canal. **No representa la LSRD**: un modelo entrenado con estos datos no reconoce señas reales.
