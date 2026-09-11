# Domino Portrait Visual V2

Base Git: `5125ee3246a38932272d09facf4ff008dc04251f`. Unity 6000.0.41f1.

Este cambio aplica el concepto Modern Social Premium sobre el proyecto existente. Se conserva el trabajo previo sin commit. La segunda instrucción sobre la cadena tiene prioridad: **BoardLayout no se modifica**.

## Resultado visual

- Tapete verde esmeralda/petróleo, iluminación suave, trama de fieltro de contraste mínimo, borde oscuro con varios niveles y sombra. Una textura procedural de 128×128 se comparte y se genera una sola vez; no hay blur ni imágenes externas nuevas.
- El compañero flota sobre el centro del borde superior. Los rivales ocupan exactamente el centro vertical de la mesa, parcialmente sobre sus bordes, con paneles oscuros semitransparentes, avatar, nombre, contador, rol y pila compacta de reversos.
- El HUD mantiene marca, puntuación relativa al equipo local, ronda/meta y menú. El aviso inferior dentro del tapete muestra el nombre del jugador activo y la indicación contextual, fuera del área de cadena.
- Mano de diez fichas en una sola fila con solapamiento mínimo en los márgenes de las fichas. Conserva el orden recibido. Tamaño de reposo 2.1; selección 1.07×, elevación y frente visual. El arrastre conserva el tamaño relativo de la mano en vez de saltar al antiguo tamaño de Landscape.
- La reserva mantiene sus quince reversos reales y el contador. Las versiones pequeñas usan el monograma existente de TeamFHO o «TF» en marfil; las fichas grandes conservan el diseño completo. El selector de estilos y la preferencia del usuario se mantienen.
- Capas de fichas con el mismo origen: mesa/cadena, paneles de jugadores, reversos, HUD y mano local. Cambiar de padre visual no cambia coordenadas ni identidad lógica. Lavado, reparto, juego y reinicio reutilizan las mismas vistas.

## Cadena aprobada

SHA-256 de BoardLayout antes y después:

`DC1A2BE9C0EBE7EE7D290AD7739AC329C102CAB013A52F5EA115B9CB93AA216C`

La referencia anterior está registrada en `Validation/Generated/PortraitV2Baseline/approved-chain.png`, junto con copia del archivo y hashes de origen. Se ajustan solamente los límites suministrados por BoardView: 64 % del ancho interior, margen superior de 190 unidades y margen inferior de 140 unidades. La construcción, conexiones, dobles, giros, separación y restricciones de escala de BoardLayout son las mismas.

La zona lateral incluye espacio para el zoom de los extremos. Las pruebas comprueban cadenas de 1 a 40 fichas contra paneles, aviso de turno y límites, además de comprobar que las fichas no se superponen. No se introduce detección de obstáculos en el algoritmo.

## Perspectiva, idiomas y reglas

`SeatPerspectiveMapper` no cambia: local abajo, compañero arriba y rivales a ambos lados. Los eventos y manos siguen indexados por el seat lógico original. Los cuatro seats se prueban en Play Mode.

Se añaden dos entradas a Unity Localization: `game.your_turn_named` y `game.turn_named`. El catálogo tiene 83 claves en inglés y español. Los roles usan sus claves existentes. No se cambia el motor, configuración de reglas, puntuación, orden de turnos, equipos, algoritmo de bots ni backend.

## Comprobaciones y evidencias

Las pruebas usan una copia aislada del proyecto y Unity real, sin cerrar la sesión del usuario. Evidencias bajo `Validation/Generated`:

- `portrait-v2-validation.log`: cuatro perspectivas, nueve resoluciones, interacción, ocultación, disposición de paneles, límites y solapamientos de la cadena.
- `portrait-v2-match.log`: smoke existente ejecutado en Portrait hasta terminar una partida a 200, con captura de una cadena de al menos veinte fichas.
- `portrait-v2-localization.log`: catálogo EN/ES, valores dinámicos, cambio de idioma conservando estado, menús, HUD y resultados.
- `portrait-v2-device-profiles.log`: reproducción de los perfiles existentes y sus áreas seguras.
- `portrait-v2-changes.diff`: cambios de esta tarea respecto a la instantánea de trabajo inicial, que ya contenía cambios anteriores sin commit.

Las trazas de regresión de dominio y puntuación se mantienen. Las comprobaciones de UI no ejecutan lógica nueva de dominó.

Resultados: compilación SUCCESS; 139.388 comprobaciones de perspectiva/presentación, 1.392 de localización y 11.175 de perfiles (60 casos). La partida completa terminó en ocho rondas con 212–113; se registró una cadena real de 21 fichas. Las sesiones Play Mode terminaron con cero errores de consola. La compilación independiente conserva tres advertencias existentes sobre referencias serializadas asignadas por la escena.

Las comprobaciones automáticas de no invasión incluyen un margen adicional de 20 unidades alrededor de cada ficha para el resaltado del extremo. Las capturas de mano y cadena se revisaron visualmente; el tamaño definitivo de la mano es 2.1 para que el borde de la ficha seleccionada no oculte los puntos vecinos.

## Límites

- No se ha medido rendimiento ni interacción táctil en teléfonos físicos; 60 FPS sigue siendo el objetivo.
- La matriz reproduce la geometría de Device Simulator; no sustituye una revisión manual en su ventana. Android usa perfiles sintéticos y las siluetas exactas de Dynamic Island no están modeladas.
- En una tablet Portrait corta la cadena puede aprovechar el ancho, según el algoritmo aprobado y el área disponible; no se fuerza un algoritmo vertical diferente.
- No se instalaron DOTween ni Feel: no estaban disponibles. Se conservaron las coroutines y el feedback nativo existente.

Sin commit y sin push. Abrir `DominoClient.unity`, elegir `Domino > Presentation > Portrait preview (1080 x 1920)`, pulsar Play e iniciar la partida por equipos.
