# Presentación del reparto físico

Source SHA: `31679fe83af47db20c65aae9c73d4a5d3b9a6435`. Al comenzar ya estaban presentes los cambios de configuración de Fase 1 sin commit. Esta tarea conserva esos cambios y no modifica Core, Game, Configuration ni el JSON de reglas. Se compararon hashes de esos scripts antes/después.

## Flujo

La cola existente consume GAME_STARTED, espera `BoardView.Deal` y solo entonces procesa TURN_CHANGED. No se introducen esperas ni eventos nuevos dentro del motor.

Fases locales: `Washing → Dealing → RevealingHand → OrganizingHands → Ready → Playing`.

- Lavado inicial: 1,2 s con las 55 vistas boca abajo, movimiento matemático sin física. No revela valores.
- Reparto: una ficha por vez, 0,245–0,285 s por desplazamiento, aproximadamente 10,6 s para cuarenta fichas. Orden tomado de `configuration.Deal.SeatOrder`.
- Las quince vistas no repartidas esperan boca abajo en el centro durante el reparto. Solo después de las cuarenta llegadas (diez por jugador) se acomodan en 0,35 s en tres filas de cinco en la esquina superior derecha. El contador «RESERVA · 15» aparece entonces y permanece visible durante la ronda; sus posiciones siguen el tamaño de la mesa. No permiten interacción ni robo. Se reutilizan en el siguiente lavado sin crear fichas adicionales.
- Llegada provisional: Fredy mantiene todas las fichas boca abajo; los rivales también. Se usan posiciones derivadas de la mano final con leves desplazamientos y giros.
- Pausa: 0,12 s; revelado de Fredy: aproximadamente 0,32 s; organización conjunta: 0,75 s; pausa READY: 0,08 s.
- El acomodo conserva el orden del modelo, ajusta separación a la composición disponible y termina con posición, escala y rotación exactas.
- Tiempo completo nominal: alrededor de trece segundos, más cuantización por frames.

La selección, JUGAR y la superficie de colocación permanecen bloqueados. Reiniciar sigue disponible para cancelar. El indicador de turno y los bots arrancan después de READY. Un reinicio devuelve al pool tanto fichas en vuelo como en mesa/manos, sin dejar coroutines del controlador activas.

## Recursos

Se preparan 55 vistas una vez al iniciar BoardView. Lavado inicial, reparto, jugadas y rondas siguientes las reutilizan. La vista previa manual del lavado puede ampliar el pool porque conserva intacta la partida visible; las vistas adicionales también se reutilizan. No hay Instantiate/Destroy de fichas dentro de cada tramo de movimiento ni física.

Los nombres de objetos de fichas no incluyen valores. Los rivales muestran únicamente reversos y conteos. Se conserva la identidad TeamFHO.

## Validación

Compilación independiente: SUCCESS. Pruebas de configuración/dominio: 80 comprobaciones de configuración y 2.981.089 de dominio/geometría; digest de cien partidas completas idéntico a la referencia.

`DealingSmokeTest`, ejecutado en una copia aislada con Unity 6000.0.41f1 y renderizado activo, comprueba:

- Orden exacto de fases, lavado entre 1,2 y 1,5 s y organización entre 0,75 y 1 s.
- Cuarenta llegadas, diez por jugador, en el orden configurado.
- Reversos durante desplazamiento y revelado exclusivo de Fredy.
- Conservación del orden de cada mano y posiciones finales del layout.
- Intentos de selección/jugada bloqueados y motor sin jugadas mientras se prepara la ronda.
- Reutilización de las mismas 55 vistas, incluso al reiniciar durante organización.
- Resoluciones reales de Game View: 1600×900, 1950×900, 2000×900, 2100×900 y tablet 1200×900.
- Cambio de resolución durante el reparto y retorno al formato 21:9 antes del acomodo final.

Resultado de la ejecución con resize: SUCCESS, 59.219 comprobaciones y Console Errors = 0 durante la sesión. Capturas revisadas de lavado, reparto parcial y manos listas en móvil/tablet. No equivale a una prueba táctil en dispositivos físicos.

Integración posterior: el smoke test de partida completa terminó cinco rondas hasta la meta 200 usando las vistas reutilizadas, con Console Errors = 0. Evidencia: `Generated/dealing-match-unity.log` y `Generated/phase1-unity-result.txt` de esta ejecución.

Evidencia local ignorada por Git: `Generated/dealing-resize-unity.log`, `Generated/dealing-unity-result.txt`, `Generated/dealing-*.png`. Ejecutar con Unity: `-batchmode -projectPath <copia aislada> -executeMethod Domino.Editor.Phase1Validation.RunDealing -logFile <log>`. El runner acelera solamente la prueba a ×4 y cierra su propia instancia al acabar.

## Diff

Actualización de reserva visible: compilación correcta y Play Mode SUCCESS con 59.789 comprobaciones, Console Errors = 0 (`Generated/reserve-unity.log`). Se verifican las quince vistas activas, ocultas y no seleccionables dentro del safe area en los cinco formatos; capturas de móvil y tablet revisadas. No se modificaron reglas ni motor para este ajuste.

`Generated/dealing-animation.diff` contiene únicamente los cambios de esta tarea respecto al working tree previo; `git diff` normal también incluye la Fase 1 pendiente de revisión. Sin commit ni push.
