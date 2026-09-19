M5 PARTNERS 2V2 ONLINE SELECTOR REPORT
=====================================

Validado el 15 de septiembre de 2026. Proyecto: `D:/Fredy/development/2026/domino/client/DominoGame`.

```text
SOURCE_SHA=82c1a985ee31a631ede0b233f1f1a25adf84bd4d
BRANCH=main
ROOT_CAUSE=La base disponible era I3; no contenía la implementación M5. Firestore publicaba v3 con solo PARTNERS_2V2 y DUEL_1V1. Faltaban el documento y binding online de parejas. También existían un selector fijo de dos tarjetas y límites DUEL en el flujo online.
FIRST_POINT_PARTNERS_2V2_ONLINE_DISAPPEARS=FIRESTORE_SOURCE / PUBLISHED_CATALOG_V3

CATALOG_VERSION_BEFORE=3
CATALOG_VERSION_AFTER=4
CATALOG_VERSION=4
PUBLISHED_VERSION=4
PUBLISHED_MODE_KEYS=PARTNERS_2V2,DUEL_1V1,PARTNERS_2V2_ONLINE
FIRESTORE_MODE_KEYS=PARTNERS_2V2,DUEL_1V1,PARTNERS_2V2_ONLINE
SPRING_CATALOG_VERSION=4
SPRING_MODE_COUNT=3
SPRING_MODE_KEYS=PARTNERS_2V2,DUEL_1V1,PARTNERS_2V2_ONLINE
API_CATALOG_VERSION=4
API_MODE_COUNT=3
API_MODE_KEYS=PARTNERS_2V2,DUEL_1V1,PARTNERS_2V2_ONLINE
UNITY_RAW_CATALOG_VERSION=4
UNITY_RAW_MODE_COUNT=3
UNITY_RAW_MODE_KEYS=PARTNERS_2V2,DUEL_1V1,PARTNERS_2V2_ONLINE
UNITY_VALIDATED_MODE_KEYS=PARTNERS_2V2,DUEL_1V1,PARTNERS_2V2_ONLINE
UNITY_CATALOG_SOURCE=Remote

ACTIVE=true
SCHEMA_SUPPORTED=YES
RULESET_SUPPORTED=YES
TOPOLOGY_SUPPORTED=YES
PLAYER_COUNT_SUPPORTED=YES
ONLINE_EXECUTION_SUPPORTED=YES
CAPABILITIES_SUPPORTED=YES
FINAL_VISIBLE=YES
FILTER_REJECTION_REASON=NONE

VISIBLE_MODE_COUNT=3
PARTNERS_2V2_VISIBLE=PASS
DUEL_1V1_VISIBLE=PASS
PARTNERS_2V2_ONLINE_VISIBLE=PASS
PARTNERS_2V2_ONLINE_HUMANS=4
PARTNERS_2V2_ONLINE_BOTS=0
SAME_RULESET_AS_LOCAL_PARTNERS=YES
RULESET_ID=double-nine-partners
RULESET_VERSION=1
RULESET_DUPLICATED=NO
SELECTOR_DATA_DRIVEN=YES
SELECTOR_SUPPORTS_N_MODES=YES
N_MODE_VALIDATION=3_TO_5_TO_3_CARDS_PASS
PORTRAIT_SCROLL=PASS
ONLINE_2V2_MATCHMAKING_ENTRY=PASS
SEARCHING_FOR_PLAYERS=PASS
REQUIRED_PLAYERS=4
LESS_THAN_FOUR_CREATES_MATCH=NO
FOUR_PLAYER_PAIRING=PASS
FOUR_DISTINCT_UIDS=PASS
FOUR_REMOTE_HUMANS=PASS
TEAMS=[0,2] vs [1,3]
TURN_ORDER=[0,3,2,1]
DEAL_PARITY=PASS
TURN_PARITY=PASS
STARTER_PARITY=PASS
OPENING_PARITY=PASS
PASS_PARITY=PASS
TRANQUE_PARITY=PASS
SCORING_PARITY=PASS
TIE_PARITY=PASS
TARGET_PARITY=PASS
RULE_PARITY=PASS
PARTNERS_2V2_LOCAL_REGRESSION=PASS
DUEL_I3_REGRESSION=PASS
MONETIZATION_REGRESSION=PASS
PORTRAIT_REGRESSION=PASS
UNITY_VERSION=6000.0.41f1
UNITY_COMPILATION=PASS
UNITY_PLAY_MODE=PASS
CONSOLE_ERRORS=0
WINDOWS_DEVELOPMENT_BUILD=PASS
BACKEND_TESTS=454 PASS / 1 OPTIONAL REAL POLICY TEST SKIPPED
I3_1_STARTED=NO
I4_STARTED=NO
I5_STARTED=NO
H7_STARTED=NO
COMMIT=NONE
PUSH=NONE
CLOUD_RUN_DEPLOYED=NO
ADS_SETTINGS_USER_CHANGE_PRESERVED=YES
API_SETTINGS_USER_CHANGE_PRESERVED=YES
DEVELOPMENT_AUTHENTICATION_MERGED_TO_MAIN=NO
```

La auditoría inicial se realizó antes de modificar código. La comparación de ramas y el catálogo real confirmó que no era únicamente un problema de visibilidad. Se implementaron los cambios necesarios sobre esta base, conservando los modos y RuleSets previos.

La publicación explícita creó `gameCatalogs/4`, `gameModes/partners-2v2-online` y `gameModeRuleBindings/partners-2v2-online-default-v1`, y actualizó el puntero existente a v4 en una transacción. El publicador verifica conflictos y conserva el contenido histórico de v3 y los RuleSets. No se creó `double-nine-partners-online`.

El flujo comprobado fue Firestore → `GameCatalogService.resolve()` → `GET /api/v1/game-modes` autenticado en `http://127.0.0.1:8080` → `GameCatalogCodec.Read()` → modos activos ordenados → tarjetas reutilizadas. El catálogo real y la captura final contienen el tercer modo. Se accede desde **Elige tu partida**, desplazando la lista hasta **2 contra 2 online → Jugar online → Buscar partida**. Los ejecutables antiguos deben recompilarse; el proyecto Unity actualizado y la build aislada de validación sí contienen M5.

Evidencia de catálogo y presentación
-----------------------------------

- Auditoría antes: [Firestore v3](D:/Fredy/development/2026/domino/client/Validation/Generated/m5-firestore-before.log), [API y Unity antes](D:/Fredy/development/2026/domino/client/Validation/Generated/m5-catalog-before.txt).
- Publicación y lectura posterior: [publicación v4](D:/Fredy/development/2026/domino/client/Validation/Generated/m5-publish-v4.log), [Firestore y Spring v4](D:/Fredy/development/2026/domino/client/Validation/Generated/m5-firestore-after.log), [API y selector real](D:/Fredy/development/2026/domino/client/Validation/Generated/m5-catalog-after.txt).
- [Captura del selector normal con el catálogo real](D:/Fredy/development/2026/domino/client/Validation/Generated/m5-selector-real-es.png).
- [Matriz visual M5](D:/Fredy/development/2026/domino/client/Validation/Generated/m5-visual-result.txt): 1.605 comprobaciones, EN/ES, cuatro asientos y nueve tamaños. Incluye manos propias visibles, adversarios ocultos, reserva de quince y crecimiento/reducción del selector de 3 a 5 a 3 tarjetas. Tamaños: 1080×1920, 1170×2532, 1179×2556, 1290×2796, 1206×2622, 1320×2868, 1080×2400, 1440×3120, 1536×2048.
- Regresiones visuales reejecutadas: I3 270 comprobaciones y DUEL online 360, ambas con cero errores: [I3](D:/Fredy/development/2026/domino/client/Validation/Generated/i3-ui-result.txt), [DUEL](D:/Fredy/development/2026/domino/client/Validation/Generated/i1-unity-result.txt).

Cuatro clientes reales
----------------------

Un Editor y tres procesos Windows, autenticación Firebase real, WebSocket real, Redis local y Firestore real. El controlador de validación pulsa los controles normales; no elige UID, adversario, Match ID ni asiento. La autenticación de desarrollo de su rama separada se aplicó únicamente a la copia generada de validación. Cada cliente creó una identidad anónima mediante el flujo existente y realizó bootstrap normal. No se borraron cuentas remotas.

| Cliente | Proceso | Huella UID | Asiento | Resultado | MATCH_FOUND |
| --- | --- | --- | ---: | --- | ---: |
| A | Unity Editor | A5813AB94EBB961E | 1 | 32 checks PASS | 1 |
| B | Windows Development | 3B2E564B6ECABF02 | 0 | 33 checks PASS | 1 |
| C | Windows Development | 3B6AE8DF425C8649 | 3 | 33 checks PASS | 1 |
| D | Windows Development | D11C89CB329E7F14 | 2 | 31 checks PASS | 1 |

Match: `56c4f508-e0b6-4a7c-955b-a6645f0e5ec9`. Los primeros tres permanecieron buscando; el cuarto desencadenó la asignación. Se comprobó que todos recibieron diez fichas y que solo veían su propia mano. Ocho jugadas quedaron confirmadas por el servidor.

La [inspección posterior de Firestore](D:/Fredy/development/2026/domino/client/Validation/Generated/m5-real-inspection.log), sin escrituras, verificó una creación `COMMITTED`, cuatro asignaciones, cuatro participantes `REMOTE_HUMAN`, cuatro repartos privados, reserva de quince, secuencias continuas y ocho jugadas. La proyección de eventos entrega solo un reparto privado a cada participante. Resultados: [A](D:/Fredy/development/2026/domino/client/Validation/Generated/M5Real/A-result.txt), [B](D:/Fredy/development/2026/domino/client/Validation/Generated/M5Real/B-result.txt), [C](D:/Fredy/development/2026/domino/client/Validation/Generated/M5Real/C-result.txt), [D](D:/Fredy/development/2026/domino/client/Validation/Generated/M5Real/D-result.txt).

Los procesos Windows se ejecutaron ocultos y sus capturas resultaron negras; esas imágenes no se usan como prueba visual. La evidencia visual procede del selector real renderizado en Unity y de la matriz de Play Mode. La ejecución y los controles de los cuatro clientes sí se comprobaron en los runtimes reales. No se afirma validación en teléfonos físicos ni una partida online completa hasta 200; la prueba real cubre entrada, reparto y ocho jugadas, y la paridad determinista cubre partidas completas.

Reglas, concurrencia y regresiones
---------------------------------

`M5ParityFixtures.cs` ejecuta el motor local existente y exporta la baraja, reservas, manos y resultados de cada acción. El test Kotlin aplica las mismas elecciones de mezcla al motor online y compara todas las transiciones: **100 partidas completas, 679 rondas, 27.946 turnos**. No se ordenan manos ni se cambian reglas para facilitar la comparación. Casos adicionales comprueban empate entre equipos, empate de mínimos dentro del mismo equipo, empates repetidos y reinicio del multiplicador tras puntuar.

Se conserva salida fija del asiento 0, reparto en orden `[0,1,2,3]`, turnos `[0,3,2,1]`, diez fichas, quince reservadas sin robo, pegue con puntos de los rivales +10, tranca por mínimo individual con puntos de los rivales sin bono, empate entre equipos que mantiene ×2 y meta 200. No se añade Capicúa. El RuleSet de parejas v1 no tiene `turnPolicy`; por ello este modo no incorpora reloj, autoplay ni sustitución por bots. DUEL conserva sus reglas y reloj propios.

Las pruebas Redis usan namespaces aislados: tres personas no crean partida, parejas y DUEL no se mezclan, 100 UIDs se reservan en 25 grupos disjuntos bajo concurrencia, y recuperación/fallo de una reserva trata correctamente a los cuatro miembros. La misma transacción Firestore existente guarda partida, estado, eventos, manos, asignaciones y recibo; se generalizó su cardinalidad.

El [backend](D:/Fredy/development/2026/domino/client/Validation/Generated/m5-backend.log) pasó 454 pruebas y omitió una prueba opcional de política económica real. También pasaron: parejas local 251.685 comprobaciones, DUEL local 14.723, cliente online 48, cliente matchmaking I3/M5 75, Player Foundation 469 y Guest Auth 43. Monetización H2/H3/H4/H5/H6: 46/30/35/27/26 comprobaciones. No se modificó código de monetización, no se solicitaron anuncios y no se ejecutaron créditos de recompensas.

Reproducción de la comparación: ejecutar `client/Validation/RunM5ParityFixtures.ps1`; después, en `server/domino`, ejecutar los tests con `DOMINO_M5_PARITY=true` y `DOMINO_REDIS_TESTS=true`. La comparación entre lenguajes es opt-in porque requiere las trazas generadas; no se versionan dumps grandes. `exportPartnersFixtures` genera las vistas para `Domino.Online.Editor.PartnersViewValidation.Run`, que exige una copia aislada bajo `Validation/Generated`. `inspectPartnersMatch -PmatchId=<id>` solo lee datos.

Archivos principales
--------------------

- [Publicador v4](D:/Fredy/development/2026/domino/server/domino/src/main/kotlin/com/teamfho/domino/catalog/GameCatalogV4Publisher.kt) y [validador del catálogo](D:/Fredy/development/2026/domino/server/domino/src/main/kotlin/com/teamfho/domino/catalog/GameCatalogValidator.kt).
- [Reserva Redis](D:/Fredy/development/2026/domino/server/domino/src/main/kotlin/com/teamfho/domino/matchmaking/RedisMatchmakingStore.kt), [servicio matchmaking](D:/Fredy/development/2026/domino/server/domino/src/main/kotlin/com/teamfho/domino/matchmaking/MatchmakingService.kt) y modelo de reserva.
- [Motor online](D:/Fredy/development/2026/domino/server/domino/src/main/kotlin/com/teamfho/domino/online/OnlineEngine.kt), servicio, modelos y repositorio online.
- [Selector dinámico](D:/Fredy/development/2026/domino/client/DominoGame/Assets/_Domino/Scripts/UI/StartMenuView.cs), codec/snapshot de catálogo, definición de modo, entrada matchmaking y [presentación online](D:/Fredy/development/2026/domino/client/DominoGame/Assets/_Domino/Scripts/UI/OnlineBoardPresentation.cs).
- [Traducciones](D:/Fredy/development/2026/domino/client/DominoGame/Assets/_Domino/Editor/Localization/Translations.json) y sus tres tablas oficiales actualizadas.
- Tests `PartnersOnlineTests`, `PartnersMatchmakingTests`, `PartnersViewValidation`, `M5ParityFixtures`, tests de cliente online/matchmaking y utilidades de exportación/inspección.
- [Harness de cuatro clientes](D:/Fredy/development/2026/domino/client/Validation/M5NetworkValidation.cs), fuera de los Assets del producto. Build aislada: `builds/m5-validation-client`; no sustituye `builds/duel-client-b`.

Los cambios preexistentes de `AdsSettings.asset` y `ApiSettings.asset` se conservaron byte por byte. Se desactivaron anuncios solo en la copia de validación. La revisión de espacios de los cambios M5 pasa; `git diff --check` global sigue señalando un espacio previo en el asset de Ads del usuario, que no se corrigió. `main` conserva el SHA inicial; no hay commit, push ni integración de la rama de autenticación de desarrollo.
