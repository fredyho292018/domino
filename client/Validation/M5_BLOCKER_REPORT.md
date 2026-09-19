M5 BLOCKER REPORT
=================

Auditoría y corrección del 15 de septiembre de 2026 sobre `main`, SHA `82c1a985ee31a631ede0b233f1f1a25adf84bd4d`. Proyecto real: `D:/Fredy/development/2026/domino/client/DominoGame`.

La tercera tarjeta **sí existía**, pero estaba completamente fuera del área visible del selector. El catálogo publicado ya era v4. Este seguimiento no publica otro catálogo ni modifica backend, reglas, RuleSets o matchmaking.

```text
PUBLISHED_CATALOG_VERSION=4
PUBLISHED_MODE_COUNT=3
PUBLISHED_MODE_KEYS=PARTNERS_2V2,DUEL_1V1,PARTNERS_2V2_ONLINE
SPRING_CATALOG_VERSION=4
SPRING_MODE_COUNT=3
SPRING_MODE_KEYS=PARTNERS_2V2,DUEL_1V1,PARTNERS_2V2_ONLINE
API_CATALOG_VERSION=4
API_MODE_COUNT=3
API_MODE_KEYS=PARTNERS_2V2,DUEL_1V1,PARTNERS_2V2_ONLINE
UNITY_RAW_CATALOG_VERSION=4
UNITY_RAW_MODE_COUNT=3
UNITY_RAW_MODE_KEYS=PARTNERS_2V2,DUEL_1V1,PARTNERS_2V2_ONLINE
UNITY_VALIDATED_MODE_COUNT=3
UNITY_VALIDATED_MODE_KEYS=PARTNERS_2V2,DUEL_1V1,PARTNERS_2V2_ONLINE
UNITY_CATALOG_SOURCE_BEFORE=CACHE
UNITY_CATALOG_SOURCE_AFTER=REMOTE
UNITY_CACHE_VERSION=4
UNITY_BUNDLED_VERSION=2
FILTER_REJECTION_REASON=NONE
ACTIVE=true
SCHEMA_SUPPORTED=YES
RULESET_SUPPORTED=YES
TOPOLOGY_SUPPORTED=YES
PLAYER_COUNT_SUPPORTED=YES
ONLINE_SUPPORTED=YES
CAPABILITIES_SUPPORTED=YES

VISIBLE_MODE_COUNT_BEFORE=2 (uno completo y otro parcialmente visible)
SELECTOR_MODEL_COUNT_BEFORE=3
FIRST_POINT_PARTNERS_2V2_ONLINE_DISAPPEARS=SELECTOR_VIEWPORT_CLIPPING
ROOT_CAUSE=La tercera tarjeta queda debajo de la máscara inicial sin barra ni indicación de más modos; el refresco del catálogo también devolvía la lista al inicio.
FIX_IMPLEMENTED=Barra de desplazamiento, contador localizado, botón Ver más modos, conservación del desplazamiento al refrescar.
SELECTOR_DATA_DRIVEN=YES
SELECTOR_SUPPORTS_N_MODES=YES
N_MODES_TEST=3_TO_5_TO_3_PASS
VISIBLE_MODE_COUNT_AFTER=3 (accesibles mediante desplazamiento)
PARTNERS_2V2_VISIBLE=PASS
DUEL_1V1_VISIBLE=PASS
PARTNERS_2V2_ONLINE_VISIBLE=PASS
PARTNERS_2V2_ONLINE_HUMANS=4
PARTNERS_2V2_ONLINE_BOTS=0
MODE_ID=partners-2v2-online
EXECUTION_MODE=ONLINE
SAME_RULESET=YES
RULESET_ID=double-nine-partners
RULESET_VERSION=1
FOUR_PLAYER_MATCHMAKING_ENTRY=PASS
PORTRAIT=PASS
UNITY_COMPILATION=PASS
UNITY_PLAY_MODE=PASS
VISUAL_CHECKS=1751 PASS
CONSOLE_ERRORS=0 (matriz aislada y sesión final del Editor real)
I3_1_STARTED=NO
COMMIT=NONE
PUSH=NONE
```

Los IDs de documento son `partners-2v2`, `duel-1v1` y `partners-2v2-online`; las keys de protocolo son los identificadores en mayúsculas del reporte.

Evidencia del primer punto
-------------------------

Se identificó antes de modificar la navegación. En el Editor del proyecto real, escena `Assets/_Domino/Scenes/DominoClient.unity`, Game View **750 × 1334**:

| Elemento | Límites verticales en pantalla, origen abajo | Resultado |
| --- | --- | --- |
| Viewport | 331,66–894,16 | Zona visible |
| PARTNERS_2V2 | 591,28–894,16 | Visible |
| DUEL_1V1 | 266,76–569,64 | Parcialmente visible |
| PARTNERS_2V2_ONLINE | −57,76–245,12 | Totalmente fuera; `culled=true` |

La respuesta autenticada del API, su validación independiente, el catálogo activo y el diccionario de tarjetas contenían los tres modos. No había `Take(2)`, array de dos tarjetas ni rechazo por capacidades en este punto. La rueda ya llegaba al `ScrollRect`; el defecto era la falta de indicaciones y el reinicio del desplazamiento.

- [Firestore y Spring, lectura real sin escrituras](D:/Fredy/development/2026/domino/client/Validation/Generated/m5-blocker-firestore.log): documento y binding existentes, publicado v4, `WRITES=0`.
- [Diagnóstico estable del Editor antes](D:/Fredy/development/2026/domino/client/Validation/Generated/M5Blocker/live-stable-before.json).
- [Captura antes](D:/Fredy/development/2026/domino/client/Validation/Generated/M5Blocker/live-stable-before.png).

En la sesión final del Editor real se hizo el recorrido por eventos de puntero: abrir selector → un clic en **Ver más modos** → clic en la tercera tarjeta **Jugar online** → **Buscar oponente**. El resultado fue `matchmakingMode=PARTNERS_2V2_ONLINE`, `matchmakingState=SEARCHING`, texto **Buscando jugadores…**, `localSessionStarted=false` y `consoleErrors=0`. Se canceló después desde el botón normal, regresando al selector con la tercera tarjeta todavía a la vista. No se escribió un Match ID ni se abrió una partida local o la cola DUEL.

- [Selector final real](D:/Fredy/development/2026/domino/client/Validation/Generated/M5Blocker/final-live-third.png) y [estado](D:/Fredy/development/2026/domino/client/Validation/Generated/M5Blocker/final-live-third.json).
- [Entrada real](D:/Fredy/development/2026/domino/client/Validation/Generated/M5Blocker/final-live-entry.json), [búsqueda real](D:/Fredy/development/2026/domino/client/Validation/Generated/M5Blocker/final-live-search.json), [captura buscando](D:/Fredy/development/2026/domino/client/Validation/Generated/M5Blocker/final-live-search.png) y [cancelación](D:/Fredy/development/2026/domino/client/Validation/Generated/M5Blocker/final-live-cancel.json).

Corrección y validación
----------------------

`StartMenuView.RefreshCatalogCards()` mantiene la colección genérica ordenada. El nuevo botón avanza el 90 % de la altura del viewport, con límite en el final. La barra y el botón solo aparecen si existe contenido fuera del viewport; el botón se desactiva al final. No se encogen las tarjetas. El refresco preserva y limita el desplazamiento actual. El contador y la acción tienen keys oficiales EN/ES.

La prueba `PartnersViewValidation` usa raycasts y eventos de puntero reales de Unity para el botón y el arrastre. Comprueba nueve tamaños, ambos idiomas, acceso al tercer y quinto modo, posición conservada tras refresh, textos y controles dentro del área segura. Espera el evento asíncrono de localización antes de comprobar el idioma, sin modificar la localización del producto.

- [Resultado de las 1.751 comprobaciones](D:/Fredy/development/2026/domino/client/Validation/Generated/m5-visual-result.txt).
- [Selector ES](D:/Fredy/development/2026/domino/client/Validation/Generated/m5-selector-es.png) y [EN](D:/Fredy/development/2026/domino/client/Validation/Generated/m5-selector-en.png).
- [Compilación adicional](D:/Fredy/development/2026/domino/client/Validation/Generated/m5-blocker-compile.log).

Tamaños: 1080×1920, 1170×2532, 1179×2556, 1290×2796, 1206×2622, 1320×2868, 1080×2400, 1440×3120 y 1536×2048. La matriz también comprueba cuatro asientos, mano privada, rivales ocultos, cadena y quince fichas reservadas.

La validación previa de M5 se conserva como evidencia de los componentes que no se modificaron en este seguimiento: 454 tests backend; 100 partidas comparadas con el motor local; Redis con tres esperando y cuatro emparejados; un Editor y tres clientes Windows reales con ocho jugadas. No se afirma haber repetido esa prueba de cuatro clientes tras esta corrección de navegación. Detalles y límites en [reporte M5 previo](D:/Fredy/development/2026/domino/client/Validation/PARTNERS_ONLINE_M5_REPORT.md). Sus capturas y conteos de navegación anteriores quedan sustituidos por los de este reporte.

Regresiones previas conservadas: PARTNERS local 251.685 comprobaciones, DUEL local 14.723, cliente online 48, matchmaking 75; I3 visual 270 y DUEL online visual 360, ambas con cero errores. El nuevo recorrido no cambia el callback que elige el modo online ni el RuleSet.

Los cambios de implementación M5 anteriores siguen pendientes. Se conservaron byte por byte los ajustes del usuario en AdsSettings y ApiSettings. No se borró caché, no se modificaron identidades ni se integró la rama de autenticación de desarrollo.

Archivos de este seguimiento
---------------------------

- [StartMenuView.cs](D:/Fredy/development/2026/domino/client/DominoGame/Assets/_Domino/Scripts/UI/StartMenuView.cs): navegación, contador y posición conservada.
- [Translations.json](D:/Fredy/development/2026/domino/client/DominoGame/Assets/_Domino/Editor/Localization/Translations.json): `menu.choose_modes` y `menu.more_modes`; tablas oficiales EN/ES y Shared Data reimportadas. El importador también regenera identificadores internos de los selectores de idioma en Localization Settings, sin cambiar sus valores.
- [PartnersViewValidation.cs](D:/Fredy/development/2026/domino/client/DominoGame/Assets/_Domino/Scripts/Online/Editor/PartnersViewValidation.cs): navegación con eventos de Unity y conservación de posición.
- Este reporte.

El diagnóstico temporal `M5LiveProbe.cs` y su `.meta` se retiraron. Se salió de Play Mode y se completó la recompilación del Editor después de retirarlos. La ventana de Unity queda abierta, lista para pulsar Play. El SHA inicial no cambió y no hay archivos staged.
