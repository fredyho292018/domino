# Domino — matriz de pantallas

Base: `ac4c70f55cbed292791798e9fac39648ecb6f4a4`. Se conservan los cambios visuales V2 pendientes. Unity 6000.0.41f1 (también instalado 6000.1.12f1; no se usa ni modifica). Device Simulator integrado en el editor; paquete oficial `com.unity.device-simulator.devices` 1.0.1, sin actualizar.

## Uso

1. Abrir DominoClient.unity y Device Simulator desde Window → General.
2. Elegir un perfil `Domino ...`, entrar en Play y esperar el reparto y la reserva.
3. Probar Landscape Left y Right. Activar la visualización de Safe Area del Simulator.
4. En el panel «Domino · Safe Area», pulsar «Validar layout actual». Esta comprobación cubre los límites de la mano; revisar también HUD, avatares, mesa, selección, arrastre, cadena y cutouts visualmente.
5. «Domino → Display Validation Matrix» permite localizar los perfiles y esta documentación. No usa reflexión ni APIs privadas para seleccionar dispositivos.

## Alcance y fuentes

Los perfiles son herramientas Editor-only, en `Assets/_Domino/Editor/DeviceSimulator`. `SafeArea` usa el [alias público recomendado por Unity](https://docs.unity3d.com/6000.0/Documentation/Manual/device-simulator-simulated-classes.html). El ancho de composición se limita a 2100 unidades para evitar dispersión ultrawide. No cambia el motor ni se introducen excepciones por modelo.

Los perfiles iPhone `_SafeArea` reproducen insets publicados, no un dibujo exacto del hardware. Medidas documentadas por Keith Harrison con sus diagramas de layout: [iPhone 14](https://useyourloaf.com/blog/iphone-14-screen-sizes/), [iPhone 15](https://useyourloaf.com/blog/iphone-15-screen-sizes/), [iPhone 16](https://useyourloaf.com/blog/iphone-16-screen-sizes/). Se multiplican los puntos por 3 para obtener píxeles. Referencias del fabricante para pantallas: [Apple 16 Pro](https://support.apple.com/en-us/121031), [Apple 15 Pro Max](https://support.apple.com/en-us/111828).

No se han medido contornos exactos de Dynamic Island, sus estados expandidos ni radios físicos: los perfiles iPhone custom no incluyen overlay ni cutout rectangular inventado. **No equivalen a una validación completa de Dynamic Island.** Las áreas reservadas sí excluyen esa franja y el indicador de inicio según la fuente citada.

Los Android `_Synthetic` son escenarios diseñados de prueba, no especificaciones de Pixel/Galaxy. Sus DPI, insets y recortes son parámetros sintéticos explícitos. No deben presentarse como perfiles exactos de modelos comerciales.

## Apple

Resolución vertical; Landscape invierte ancho/alto. DPI 460 en todas estas filas. Insets P=arriba/abajo; L=izquierda/derecha/abajo, en píxeles. Landscape arriba=0. Las cuatro orientaciones están definidas.

| Modelo | Resolución | Ratio aprox. | P | L | Perfil disponible |
|---|---|---|---|---|---|
| iPhone 14 | 1170×2532 | 19.5:9 | 141/102 | 141/141/63 | Oficial equivalente: Apple iPhone 13 Pro; no duplicado |
| iPhone 14 Pro | 1179×2556 | 19.5:9 | 177/102 | 177/177/63 | Custom SafeArea |
| iPhone 14 Pro Max | 1290×2796 | 19.5:9 | 177/102 | 177/177/63 | Custom SafeArea |
| iPhone 15 | 1179×2556 | 19.5:9 | 177/102 | 177/177/63 | Custom SafeArea |
| iPhone 15 Pro | 1179×2556 | 19.5:9 | 177/102 | 177/177/63 | Custom SafeArea |
| iPhone 15 Pro Max | 1290×2796 | 19.5:9 | 177/102 | 177/177/63 | Custom SafeArea |
| iPhone 16 | 1179×2556 | 19.5:9 | 177/102 | 177/177/63 | Custom SafeArea |
| iPhone 16 Pro | 1206×2622 | 19.6:9 | 186/102 | 186/186/63 | Custom SafeArea |
| iPhone 16 Pro Max | 1320×2868 | 19.6:9 | 186/102 | 186/186/63 | Custom SafeArea |

No había modelos posteriores oficiales instalados. Los perfiles del 14/15/16 con la misma resolución se conservan con nombres de modelo explícitos para la selección solicitada; no se duplican perfiles oficiales existentes.

## Pixel y Galaxy: matriz de cobertura pendiente

Pantallas verificadas en [Google](https://support.google.com/pixelphone/answer/7158570?hl=en), [Samsung S24](https://www.samsung.com/uk/support/mobile-devices/comparison-between-the-galaxy-s24-ultra-s24-plus-and-s24/), [Samsung Developer](https://developer.samsung.com/galaxy-emulator-skin/galaxy-s.html) y [Samsung S26](https://news.samsung.com/global/samsung-unveils-galaxy-s26-series-the-most-intuitive-galaxy-ai-phone-yet). Los skins Samsung son para Android Emulator, no perfiles Unity oficiales.

| Modelos | Resolución vertical | PPI verificado | Cobertura provisional | Estado exacto |
|---|---|---|---|---|
| Pixel 9, Pixel 10 | 1080×2424 | 422 | Android 20:9 Standard | PENDIENTE insets/cutouts por versión Android y navegación |
| Pixel 9 Pro, Pixel 10 Pro | 1280×2856 | 495 | Android 20:9 Standard/Large | PENDIENTE insets/cutouts |
| Pixel 9 Pro XL, Pixel 10 Pro XL | 1344×2992 | 486 | Android 20:9 Large | PENDIENTE insets/cutouts |
| Galaxy S24, S25, S26 | 1080×2340 | No usado | Android 19.5:9 Compact | PENDIENTE insets/cutouts |
| Galaxy S24+, S25+, S26+ | 1440×3120 | No usado | Android 19.5:9 + Large | PENDIENTE insets/cutouts |
| Galaxy S24 Ultra, S25 Ultra, S26 Ultra | 1440×3120 | No usado | Android 19.5:9 + Large | PENDIENTE insets/cutouts |

No se crearon archivos con nombres de estos modelos y Safe Areas inventadas. La cobertura de formato **no significa** que se haya validado ese modelo físico.

## Escenarios genéricos

| Perfil | Resolución vertical | DPI sintético | Inset superior/inferior P | Laterales/inferior L | Cutout sintético |
|---|---|---|---|---|---|
| 16:9 | 1080×1920 | 320 | 0/0 | 0/0 | Ninguno |
| 18:9 | 1080×2160 | 400 | 0/0 | 0/0 | Ninguno |
| 19.5:9 Compact | 720×1560 | 320 | 72/36 | 72/36 | Caja 60×60 centrada en borde superior P |
| 20:9 Standard | 1080×2400 | 420 | 90/48 | 90/48 | Igual |
| 20:9 Large | 1440×3200 | 480 | 108/60 | 108/60 | Igual |
| 21:9 UltraWide | 1080×2520 | 420 | 90/48 | 90/48 | Igual |
| Tablet 16:10 | 1600×2560 | 240 | 0/48 | 0/48 | Ninguno |

Los cutouts rotan a izquierda/derecha en Landscape. Los márgenes inferiores simulan una reserva para gestos, no barras reales del sistema. No hay overlay de esquinas redondeadas genéricas.

## Perfiles oficiales ya disponibles

Paquete local: 82 perfiles. Entre los más recientes: Apple iPhone 13 Pro/Pro Max, iPhone 12, Google Pixel 5, Galaxy Note20 Ultra 5G, Z Flip3 5G, Z Fold2 5G, iPad Air (4th generation), iPad Pro 11 y 12.9. Se mantienen intactos con sus overlays y recortes originales. El inventario completo se genera desde los `.device` del paquete, sin descargar ni copiar esos assets.

## Validación y límites

`DisplayProfileValidation.Run` usa APIs públicas de Unity y reproduce las dimensiones/insets sobre un Canvas de prueba en World Space en una copia aislada. Verifica importación, límites geométricos, ambas orientaciones Landscape y ausencia de NaN al pasar por Portrait. Produce capturas y resultados bajo `Validation/Generated/DeviceMatrix`.

**Esta reproducción de geometría no es una sesión interactiva de Device Simulator.** No certifica entrada táctil, cutout físico, radios, selección por perfil ni tamaño táctil real. La revisión dentro del Simulator se hace con el panel público añadido, y queda pendiente hasta ejecutarla. No se usan hacks de selección de dispositivos ni APIs internas nuevas.

Rendimiento en iPhone/Android físicos: NO VALIDADO. Sin commit ni push.

## Resultado de esta ejecución

- Unity importó los 15 perfiles custom; compilación y Play Mode correctos, Console Errors=0.
- Geometría: 5.625 comprobaciones, 60 combinaciones (30 Landscape y 30 Portrait). Landscape PASS en límites de mano, reserva, avatares, nombres, marcador y controles. Portrait solo verifica escala finita, no usabilidad.
- Esquema geométrico independiente: 60 orientaciones con Safe Area válida y sin intersección con cutouts sintéticos.
- Capturas revisadas: iPhone 16 Pro Max y Android compacto; no equivalen a una prueba de interacción real de Device Simulator.
- Device Simulator interactivo: PENDIENTE. Cutouts físicos/Dynamic Island exactos: PENDIENTE. Los modelos Pixel/Galaxy no se declaran aprobados.
- Evidencia: Validation/Generated/DeviceMatrix/result.txt, geometry-results.txt y capturas PNG; Validation/Generated/device-matrix-unity.log.
