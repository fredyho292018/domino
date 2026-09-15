# I2.2 — Paridad UX y cierre de ronda DUEL

SOURCE_SHA=0e6b3691ac2174d3ce7c00fe7dce9203cfc6d0d2
SOURCE_OF_TRUTH_UX=PARTNERS_2V2_CURRENT_GAMEPLAY
PARTNERS_2V2_UX_AUDITED=YES
COMMIT=NONE
PUSH=NONE

## Comportamiento final

Una jugada confirmada de DUEL usa BoardView.Play, incluidos desplazamiento, rotación, aterrizaje y feedback. Se eliminaron los botones adicionales izquierda/derecha: se utiliza la misma selección, arrastre, cabeceras resaltadas, toque de mesa y botón de acción del tablero local. El temporizador complementa el marcador existente.

Al terminar una ronda, el marcador se actualiza con scores del servidor antes de pulsar Continuar. La mesa permanece visible durante cinco segundos de tiempo real; después aparece RoundRewardView, el mismo resumen usado en 2v2, con ganador, puntos otorgados y marcador. El perdedor puede elegir Ver anuncio cuando la política remota, la sesión y el SDK lo permiten. Nunca se muestra automáticamente. La precarga utiliza RewardedRoundPreload y la visualización/validación/crédito conserva RoundRewardFlow y sus servicios existentes.

El resumen permanece hasta la acción local de Continuar, incluso si el rival ya avanzó. Al continuar, se envía NEXT_ROUND solamente si el servidor aún está en ROUND_FINISHED; en otro caso se presenta el estado confirmado que llegó mientras el resumen estaba abierto. La siguiente ronda reutiliza Deal cuando se recibe desde el inicio; una recuperación a mitad de ronda restaura el estado sin repartir fichas ya jugadas.

La pausa es de presentación. No detiene ni cambia el temporizador autoritativo: si el otro cliente inicia la siguiente ronda, su reloj puede avanzar mientras este cliente conserva el resumen/anuncio.

## Auditoría 2v2 y comparación

| Área | Implementación 2v2 | DUEL online |
|---|---|---|
| TILE_TOUCH_FEEDBACK | BoardView.SetSelected, FeedbackCue.TilePick | Mismos componentes |
| TILE_SELECTION_EFFECT | Elevación 26, escala local x1.07, interpolación exponencial 18, contorno/sombra DominoFace | Mismo BoardView.ToggleSelection para local y online; repetir tap deselecciona |
| VALID_MOVE_FEEDBACK | Cabeceras seleccionadas; zoom 1.16–1.20, color de zona de drop | Misma representación; legalidad local solo orientativa, backend decide |
| INVALID_MOVE_FEEDBACK | Mensaje game.no_match o game.invalid_end; retorno del drag | Mismos mensajes y retorno; sin modal/shake nuevo |
| PLAY_ANIMATION | BoardView.Play, .42 s local / .75 s rival, previsualización .4 s, aterrizaje .14 s | Reutilizado tras confirmación; se aplica también a autoplay confirmado |
| TURN_HIGHLIGHT | PlayerView.SetTurn, notice/banner y FeedbackPresenter.Turn | Mismos componentes, dos asientos |
| PASS_FEEDBACK | BoardView.ShowPass → TableEffects.Knock, dos golpes | PLAYER_PASSED del servidor alimenta el mismo efecto |
| DEAL_ANIMATION | BoardView.Deal, lavado 1.2 s, reparto/organización y reserva | Reutilizado con dos manos y 35 reservadas |
| SCORE_FEEDBACK | Marcador existente + pulso de FeedbackPresenter al celebrar | Misma superficie y ShowWinner; scores autoritativos antes del resumen |
| Round transition | Efectos de ganador, resultado y reparto | Mismos efectos/overlay; pausa solicitada de cinco segundos |
| Audio/haptics | FeedbackPresenter y adaptadores existentes; ningún efecto independiente de error encontrado | Mismas rutas, sin paquetes ni audio nuevos |
| Theme | DominoTileView, DominoFace, TileStyles, DominoVisualTheme, BoardView | Mismos renderers y tema activo |

El tap fuera de las fichas no incorpora una nueva regla de deselección: el patrón local existente permite tocar la mesa para jugar la selección. La lógica de cambio/toggle de selección ahora está compartida en BoardView.ToggleSelection.

## Report additions

DUEL_REUSES_TILE_TOUCH_EFFECT=PASS
DUEL_REUSES_TILE_SELECTION=PASS
DUEL_REUSES_PLAY_ANIMATION=PASS
DUEL_REUSES_INVALID_MOVE_FEEDBACK=PASS
DUEL_REUSES_TURN_INDICATOR=PASS
DUEL_REUSES_PASS_FEEDBACK=PASS
DUEL_REUSES_DEAL_ANIMATION=PASS
DUEL_REUSES_SCORE_FEEDBACK=PASS
DUEL_REUSES_GAMEPLAY_TILE_RENDERER=PASS
DUEL_REUSES_ACTIVE_THEME=PASS
LOCAL_ONLINE_DUEL_UX_PARITY=PARTIAL (controles compartidos; diferencias de autoridad y limitaciones siguientes)
PARTNERS_2V2_REGRESSION=PASS

## Límites y alcance pendiente

- El contrato actual OnlineSnapshot.RoundResult contiene remainingPips, no las manos restantes del adversario. Se muestran sus puntos y se conservan los reversos. El revelado de caras del rival al finalizar NO está implementado: requiere que el servidor publique esas fichas exclusivamente al terminar. No se inventan valores ni se revela información privada anticipadamente.
- No se cambió la regla de auto-pass. La presentación consume PLAYER_PASSED; si el runtime actual requiere PASS manual, el control estándar del tablero ofrece esa acción cuando no hay jugada. No se añadió una decisión automática local.
- Un salto de estado por resync se restaura directamente. Se animan adiciones continuas confirmadas de una ficha; no se reconstruye una historia ausente ni se inventan jugadas intermedias.
- La prueba de UI mostró Ver anuncio deshabilitado por disponibilidad. No se reprodujo un anuncio Google real ni se acreditaron monedas. La interacción real depende de SDK/plataforma/consentimiento y política existentes.
- No se repitió una partida completa con dos procesos conectados en esta tarea; la validación de presentación usa snapshots de prueba y el contrato existente. Las reglas, temporizador, puntuación, backend y RuleSet no se modificaron.

## Validación

UNITY_COMPILATION=PASS
UNITY_PLAY_MODE=PASS — 360 checks, nueve tamaños Portrait/tablet, ambos asientos, EN/ES
CONSOLE_ERRORS=0
ONLINE_CLIENT_TESTS=43 PASS
PARTNERS_GAMEPLAY_REGRESSION=251685 checks PASS; 250 rondas diferenciales / 100 partidas golden
LOCAL_DUEL_REGRESSION=14723 checks PASS; 200 rondas
H5_REWARD_FLOW_REGRESSION=27 checks PASS; REAL_NETWORK=0; LIVE_WALLET_CREDIT=0

Se comprueba: toggle/transferencia de selección, ausencia de botones LEFT/RIGHT, bloqueo fuera de turno, manos privadas, temporizador dentro de safe area, aterrizaje solo tras snapshot confirmado, marcador antes de Continuar, resumen ausente durante la pausa, resumen posterior, permanencia ante avance remoto, cierre al continuar y ausencia de NEXT_ROUND duplicado. El test de cliente verifica el payload anidado real de PLAYER_PASSED y supresión de duplicados.

Evidencia: client/Validation/Generated/UxParity/transition.log, online-client-final.log, partners.log, duel.log, rewards.log y client/Validation/Generated/i1-unity-result.txt. Captura revisada: client/Validation/Generated/online-ux-result.png. Los primeros errores visuales detectados (texto sin importar y superposición Pase/Jugar) fueron corregidos y revalidados.

## Archivos de esta tarea

- Scripts/UI/BoardView.cs: selección compartida; parámetro de disponibilidad del flujo de recompensa en Finish, conservando el valor predeterminado local.
- Scripts/UI/OnlineBoardPresentation.cs: animación confirmada, HUD compartido, pausa, marcador/resumen y oferta opcional al perdedor.
- Scripts/Online/OnlineMatchController.cs: cola de presentación, controles canónicos, permanencia del resumen y feedback de pase.
- Scripts/Online/OnlineMatchClient.cs: evento de presentación de pase desde payload autoritativo; sin cambio de contrato backend.
- Scripts/Client/DominoClientController.cs: llamada a ToggleSelection compartido.
- Scripts/Online/Editor/OnlinePlayModeValidation.cs y client/Validation/OnlineMatchClientTests.cs: comprobaciones de regresión/paridad.
- Scripts/Online/Development/TwoUnityValidation.cs: adaptación del harness a los controles compartidos.
- Editor/Localization/Translations.json y las tres tablas oficiales: result.online_award EN/ES.

Rutas de Scripts/Editor relativas a client/DominoGame/Assets/_Domino. Los cambios anteriores y los assets locales ApiSettings/AdsSettings se preservan; no se hizo commit, push, deploy ni se regeneró el standalone en esta tarea.
