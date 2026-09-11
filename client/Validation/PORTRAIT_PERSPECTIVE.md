# Domino — Portrait y perspectiva local

SOURCE_SHA: `5125ee3246a38932272d09facf4ff008dc04251f`

Unity: 6000.0.41f1. Escena: `DominoGame/Assets/_Domino/Scenes/DominoClient.unity`.

## Alcance de esta tarea

El árbol ya tenía cambios sin commit de modo offline, localización y corrección de puntuación. Se conservaron. Esta tarea modifica presentación, configuración de orientación y la identidad del participante local de la sesión; no modifica el motor, las reglas, los equipos, la puntuación ni el algoritmo de los bots.

## Perspectiva

`SeatPerspectiveMapper` es inmutable y deriva el compañero de `GameConfigurationSnapshot.TeamAssignments`. El primer rival que sigue al local en `TurnOrder` se presenta a la derecha; el otro, a la izquierda. No se ordenan de nuevo manos, jugadores, puntuaciones ni eventos.

| Local | Abajo | Arriba | Izquierda | Derecha |
|---|---|---|---|---|
| 0 | Fredy (0) | Maria (2) | Alex (1) | John (3) |
| 1 | Alex (1) | John (3) | Maria (2) | Fredy (0) |
| 2 | Maria (2) | Fredy (0) | John (3) | Alex (1) |
| 3 | John (3) | Alex (1) | Fredy (0) | Maria (2) |

API de entrada: `DominoClientController.StartMatch(mode, localPlayerSeat)`. La sobrecarga de un argumento mantiene el local 0. `BoardView.Initialize(..., configuration, localPlayerSeat)` configura únicamente la perspectiva visual. Se establece al crear la sesión, no se cambia durante una partida. Los eventos siguen usando los seats del motor. La sesión identifica al humano; los otros tres seats siguen usando el mismo bot existente.

El marcador «Nosotros / Ellos» consulta el equipo del local y sus índices de puntuación originales. Los nombres propios conservan su identidad y los roles usan las claves existentes `player.you`, `player.partner` y `player.opponent`.

## Decisión visual

- Portrait fijo en móvil. Portrait invertido y Landscape desactivados para evitar cambiar la posición de los controles durante la partida. El editor puede continuar probando Landscape al redimensionar Game View.
- CanvasScaler: Scale With Screen Size, referencia 1080 × 1920 y ajuste equilibrado de ancho/alto. El layout utiliza el tamaño real del área segura para distribuir la altura adicional. La referencia fija la densidad visual, no la resolución del dispositivo.
- Mano: dos filas de cinco. Se eligió frente al abanico porque muestra ambas mitades sin ocultar puntos y deja una superficie individual de toque. Mantiene el orden recibido; recalcula las filas al jugar fichas.
- Selección: elevación, escala 1.07 respecto al tamaño de reposo y orden visual al frente. Arrastre conserva las validaciones y, para una cadena de una ficha, resuelve los extremos según su orientación real.
- Rivales compactos: avatar, nombre, contador y diez reversos superpuestos. El compañero tiene presentación horizontal y acento del equipo local. Se mantienen los 15 reversos reservados y la identidad TeamFHO.
- El tablero deriva su espacio del RectTransform real, con margen para la reserva, indicaciones y resaltado. BoardLayout recibe sus dimensiones, crece sobre el eje largo, gira la cadena y conserva dobles transversales. El ajuste uniforme evita deformar fichas.
- La clase parcial `PortraitBoardPresentation.cs` agrupa el layout de `BoardView`; no añade otro controlador ni una arquitectura paralela.

## Validación

- Compilación estática con referencias de Unity: SUCCESS; tres advertencias ya existentes sobre campos serializados asignados desde la escena.
- Pruebas del motor: 2.981.089 comprobaciones, 1.000 juegos; traza histórica sin cambios.
- Puntuación: 32.679 comprobaciones y 250 rondas diferenciales; misma traza de puntuación vigente.
- Mapper: los cuatro seats y una configuración con parejas y orden de turnos distintos, sin asumir nombres ni pares de asientos.
- Play Mode: SUCCESS, 109.769 comprobaciones y 0 errores de consola. Reparto de 40 fichas, 10 por seat, 15 reservadas, revelado exclusivo del local, bloqueo de interacción/bots durante preparación, selección, arrastre rechazado fuera de mesa y aceptado en extremo legal, evento con seat lógico y una ronda completa con resultado/lavado.
- Geometría de cadena: entre 1 y 40 fichas, límites del espacio real y ausencia de superposiciones en el patrón de prueba.
- Resoluciones Play Mode: 1080×1920, 1080×2160, 1080×2340, 1080×2400, 1080×2520, 1170×2532, 1284×2778, 1600×2560, 1536×2048. Inglés y español alternados.
- Perfiles Device Simulator: reproducción de 60 geometrías/orientaciones de los 15 perfiles existentes, 11.175 comprobaciones, incluidos notch/márgenes inferiores. Ahora se comprueban límites en Portrait además de Landscape.

Evidencias generadas (ignoradas por Git): `Validation/Generated/portrait-verified.log`, `portrait-unity-result.txt`, capturas `portrait-*.png`, `portrait-device-profiles.log` y `DeviceMatrix/result.txt`.

## Límites de la validación

Las pruebas se ejecutan en una copia aislada del proyecto con Unity real. Las capturas de Play Mode se revisan visualmente. La matriz de perfiles reproduce sus datos de área segura; no equivale a manipular manualmente la ventana Device Simulator. Los perfiles Android son escenarios sintéticos y las siluetas exactas de Dynamic Island no están incluidas. Falta validación táctil y de rendimiento en dispositivos físicos; no se generó APK/IPA ni se incorporó networking.

En tabletas cortas el área de cadena puede usar su eje horizontal, manteniendo la aplicación en Portrait. Es una adaptación al espacio disponible, no una rotación del dominio.

## Reproducir

Abrir la escena, elegir `Domino > Presentation > Portrait preview (1080 x 1920)`, entrar en Play y seleccionar la partida por equipos. Para probar otra perspectiva usar la sobrecarga `StartMatch(mode, seat)` antes de iniciar la sesión.

Pruebas locales: `Validation/RunTests.ps1` y `Validation/Compile.ps1`. En la copia aislada, ejecutar `Domino.Editor.Phase1Validation.RunPortrait`; para las áreas seguras, `Domino.Editor.DisplayProfileValidation.Run`.

COMMIT=NONE. PUSH=NONE.
