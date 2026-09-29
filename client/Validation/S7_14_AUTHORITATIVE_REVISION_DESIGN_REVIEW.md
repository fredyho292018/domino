# SERVER-7 / S7-14 — Authoritative revision design review

**DECISION=REMOVE_PRIOR_RUNTIME_UNSAFE bajo el contrato actual.** La opción A mantiene validación de dominio sobre un estado transaccional consistente, pero elimina una precondición adicional demostrada por código y pruebas: un comando nuevo solo puede aplicar sobre la misma `lastSequence` que el servidor observó antes de iniciar la transacción. Serialización no equivale a esa precondición. No existe otra fuente autoritativa ya disponible para sustituirla en B, y el recibo de C no existe para un comando nuevo.

Se conserva **3 rondas / 4 documentos por comando nuevo exitoso sin retry**. No se prepara implementación S7-15. Revisión estática del SHA `5a74d7917fc7fb0baa04bbdf2c8fc45de5a13f14`; no se ejecutaron tests, carga, consultas al servidor ni cambios de configuración. Se crea únicamente este informe. Las conclusiones sobre pruebas describen su código y evidencia anterior, no una nueva ejecución.

## 1. Lectura previa y flujo de revisión

En [OnlineMatchService.kt:58](D:/Fredy/development/2026/domino/server/domino/src/main/kotlin/com/teamfho/domino/online/OnlineMatchService.kt:58), `command` valida ID, protocolo y forma del comando, luego:

1. Línea 68: `before=read(c.matchId)`. El helper de línea 133 valida el identificador y convierte ausencia en `MATCH_NOT_FOUND`.
2. `OnlineRepository.read`, línea 119, obtiene `matches/{id}/runtime/authoritative` y decodifica su estado completo. No lee el documento raíz del Match por separado.
3. Línea 70: `OnlineEngine.seat(before,uid)` verifica pertenencia. Campos usados: `match.participants[].playerUid`; el método obtiene `seatIndex`, aunque aquí no se conserva su retorno.
4. Línea 72: `before.match.lastSequence` se pasa como `expectedSequence` a `repository.transact`. El fingerprint liga UID autenticado y comando serializado; no depende del snapshot previo.
5. [OnlineRepository.kt:127](D:/Fredy/development/2026/domino/server/domino/src/main/kotlin/com/teamfho/domino/online/OnlineRepository.kt:127): cada callback lee primero el recibo. Si existe y coincide el fingerprint, retorna su resultado anterior sin leer runtime/work ni comprobar igualdad de revisión.
6. Para un recibo ausente, `tx.getAll(stateRef,workRef)` obtiene snapshots de ese intento. Comprueba integridad de referencias, decodifica runtime y extrae `presenceCheckAt`.
7. Línea 147 exige `before.match.lastSequence==expectedSequence`; falla con `STALE_COMMAND` antes del dominio. El nombre local `before` aquí corresponde al runtime transaccional, no al objeto previo del servicio.
8. El dominio calcula una transición nueva con UID, comando y hora del servidor. `OnlineWrites.validate` verifica eventos consecutivos y demás invariantes. Recibo: primera secuencia `expectedSequence+1`, última secuencia del estado resultante.
9. Estado, eventos, recibo, rounds, History y work se escriben en la misma transacción. Se espera su finalización. Solo después el servicio publica y [RealtimeHandler.kt:179](D:/Fredy/development/2026/domino/server/domino/src/main/kotlin/com/teamfho/domino/realtime/RealtimeHandler.kt:179) encola `COMMAND_ACCEPTED` con `receipt.resultingSequence`.

**No es solo una lectura de revisión:** proporciona validación de existencia/decodificación y pertenencia previa incluso para duplicados. No valida en esa fase turno, terminalidad o abandono; estos se validan dentro del engine. No congela el tablero mostrado en Unity ni el instante de llegada de red.

### Otros consumidores de snapshots previos

No deben alterarse indirectamente al cambiar la interfaz compartida:

| Servicio | Uso del snapshot anterior a `transact` |
|---|---|
| `join`, líneas 54–55 | `lastSequence`; pertenencia/aforo se evalúan por engine dentro de transición; preparación de perfil independiente |
| `command`, líneas 68–72 | pertenencia y `lastSequence` |
| `timeout`, líneas 95–100 | fase, deadline, ronda y turno; ID determinista del sistema y comprobación de esos valores dentro del callback |
| `connection`, líneas 103–112 | asiento, deadline de reconexión, revisión para ID; evaluación preliminar y posterior de transición |
| `abandon`, líneas 118–126 | revisión para ID y evaluación preliminar; incluye entrada singleton de mantenimiento |

`snapshot`, `state` y `events` también leen runtime, pero no son esta prelectura de un comando nuevo. No se propone eliminar esas lecturas. `refreshDiscovery` conserva su propio contrato transaccional.

## 2. Garantía exacta y semántica

Para un **comando sin recibo previo**, sea N la `lastSequence` leída por el servidor en la prelectura. Toda ejecución de dominio que pueda persistir ese comando exige que el runtime transaccional siga en N. N permanece constante en los reintentos de esa invocación. Si otra transición avanza la secuencia, el comando falla; no se reinterpreta automáticamente contra la nueva revisión. Un duplicado con recibo válido es la excepción explícita y devuelve el resultado duradero anterior.

`lastSequence` es **secuencia de eventos de aplicación** usada como revisión, no `updateTime` ni token de concurrencia Firestore. Una transición puede añadir varios eventos: N→N+k, no necesariamente N+1. Un cambio que no avance eventos no queda detectado por igualdad de secuencia por sí sola; la consistencia transaccional sigue siendo necesaria. Por eso tampoco se afirma que cualquier escritura documental deba producir `STALE_COMMAND`.

El [DTO OnlineCommand, línea 16](D:/Fredy/development/2026/domino/server/domino/src/main/kotlin/com/teamfho/domino/online/OnlineModels.kt:16) contiene protocolo, commandId, matchId, tipo y argumentos de jugada. No aporta revisión esperada, UID, asiento o puntuación como autoridad. UID llega de la sesión autenticada. Sin PRIOR_RUNTIME quedan esos datos y el fingerprint, pero no un N observado antes de la transacción.

## 3. Qué garantiza Firestore y qué no

Firestore ofrece aislamiento serializable por tiempo de commit. Su biblioteca Java de servidor utiliza el control de concurrencia configurado en la base; puede haber espera/bloqueo o abortos/reintentos. No se presupone que esta base esté en modo optimista: su modo no se verificó en esta revisión. Lecturas preceden a escrituras y los cambios de una transacción se confirman atómicamente. Nada de ello conserva por sí mismo una revisión de aplicación observada antes de la transacción. [Documentación oficial de concurrencia](https://docs.cloud.google.com/firestore/native/docs/transaction-data-contention).

El uso real es `db.runTransaction(callback, TransactionOptions...setNumberOfAttempts(8))` y espera del futuro. El callback puede repetirse; no son ocho ejecuciones garantizadas ni una promesa de éxito. Runtime/work se releen por intento y no se reutilizan snapshots fallidos. Los conflictos de documentos y creación de recibo se resuelven dentro de Firestore; errores de aplicación como `STALE_COMMAND` no son una orden de aceptar sobre otro estado. [API Java Firestore](https://docs.cloud.google.com/java/docs/reference/google-cloud-firestore/latest/com.google.cloud.firestore.Firestore).

## 4. Opciones A, B y C

**A — transacción solamente: UNSAFE como reemplazo equivalente.** Comparar la revisión de TX_RUNTIME consigo misma es tautológico. Omitir la comparación deja reglas de dominio válidas en el estado del intento, pero pierde la vinculación a N. También habría que derivar del runtime la primera secuencia del recibo; dejar una revisión inventada produciría inconsistencia adicional. Aun reparando ese detalle, los contraejemplos siguientes permanecen.

**B — fuente autoritativa existente: NONE_IDENTIFIED.** Un snapshot ya visto por un proceso puede ser obsoleto y no existe en todas las instancias. Redis no es autoridad. La versión documental del snapshot transaccional describe ese snapshot, no la observación anterior inexistente. El último evento recibido por cliente tampoco sustituye la precondición del servidor. Fijar N desde el primer callback, incluso conservándolo entre retries, mueve la frontera de carrera y no equivale a la prelectura. No se inventa un token nuevo ni un esquema alternativo para forzar la reducción.

**C — revisión en recibo: NOT_AVAILABLE_FOR_NEW_COMMAND.** Un duplicado dispone de secuencias/fingerprint persistidos y puede retornar. Un comando nuevo no tiene recibo del que obtener N; precargar uno exigiría otra fase durable y alteraría el modelo. Un recibo de otro comando no establece la revisión preparada para este. Se rechaza esa sustitución.

Memoria de proceso como única autoridad: **REJECTED**. Redis como revisión: **OUT_OF_SCOPE**. No hay un diseño equivalente viable de dos rondas demostrado con las fuentes actuales.

## 5. Races y contraejemplos

Todas las filas comparan invocaciones que ya observaron N cuando corresponde. Si la prelectura del segundo comando ocurre después del primer commit y observa N+k, el contrato actual puede admitirlo: no es un bloqueo global de solicitudes simultáneas ni del tiempo de llegada.

| Escenario | Contrato actual | A: validar solo snapshot transaccional |
|---|---|---|
| Dos comandos distintos válidos en N | Tras commit A, B con N congelado falla `STALE_COMMAND` | Si B sigue válido en N+k, puede aplicar. Contraejemplo concreto abajo |
| Mismo jugador envía dos PLAY_TILE | Tras el primero, segundo preparado en N falla por revisión | Normalmente segundo falla por turno/ficha; eso no demuestra equivalencia para cambios que no cambian su elegibilidad o un ciclo de turnos |
| Jugadores distintos compiten | El perdedor no consume otra revisión con el mismo N | Una jugada que no era del turno actual puede volverse válida tras el primero y aplicar; pierde el rechazo de la revisión original |
| Turno cambia y actor ya no es válido | Revisión falla; engine también tiene comprobación de turno | Revalidar engine rechaza `NOT_YOUR_TURN` o deadline. Conserva esta seguridad de dominio, no el código/frontera de error |
| Otra transacción termina Match | Revisión rechaza nuevo comando preparado en N | Engine rechaza FINISHED/CANCELLED; no se permite gameplay nuevo terminal. Duplicado válido puede devolver recibo sin volver a jugar |
| Abandono del actor / todos abandonados | Revisión y engine/deadline impiden jugada nueva | Engine revalida ABANDONED, reconnectDeadline y terminalidad. Si el cambio afectó a otro asiento y el comando sigue legal, A puede aceptarlo donde hoy se rechaza |
| Dos duplicados del mismo ID/payload | Una transición durable; el otro, al resolver el conflicto/recibo, devuelve el mismo resultado | Recibo-first y create atómico pueden conservar esta propiedad sin runtime para el duplicado; no sustituyen revisión de comandos diferentes |
| ID igual, distinto actor/payload | Fingerprint rechaza conflicto | Debe conservarse la misma validación; no puede tratarse como duplicado válido |
| Instancias A/B separadas | N por invocación + comparación en Firestore; no requiere JVM compartida | Firestore mantiene serialización, pero ambas pueden confirmar secuencias consecutivas si siguen válidas. Falta la garantía de aplicación |

### Contraejemplo 1: ambos comandos válidos, mismo estado inicial

El servicio/engine compartido soporta `STARTER_SELECTION` de duelo. Con HIGH_TILE_SELECTION, sin selecciones en N: jugador 0 elige candidato 0 y jugador 1 elige candidato 1. Ambos son válidos en N. Engine.select (líneas 125–130) permite la segunda selección tras la primera porque asiento y candidato aún no están usados. A confirma primero y emite progreso, N→N+k. Con revisión congelada, B preparado en N se rechaza. En opción A, B puede leer/reintentar sobre N+k y completar selección/reparto. Los dos commits son serializables y las jugadas legales, pero se ha perdido el contrato de revisión. El hecho de que R3 usara PARTNERS no autoriza romper el engine compartido para DUEL.

### Contraejemplo 2: también aplicable a PARTNERS

Un jugador prepara una jugada legal y la prelectura observa N. Otra instancia persiste desconexión/reconexión de otro asiento, emitiendo un evento, sin cambiar tablero/turno ni elegibilidad del jugador. Actual: su comando nuevo rechaza N≠N+k. A: sigue siendo legal y puede aplicarse. No es corrupción de tablero; sí una aceptación expresamente distinta de la precondición actual. Fijar N dentro del primer callback no protege este intervalo previo.

Estas trazas de código son suficientes para rechazar equivalencia; no dependen de provocar retries reales ni afirmar que cualquier conflicto corrompa estado. Si se quisiera aceptar nuevas semánticas de rebase, sería un cambio de contrato separado, no la optimización autorizada.

## 6. Retry, eventos y efectos secundarios

En la ruta gameplay, cada callback usa runtime/work frescos, ejecuta engine, valida `OnlineWrite`, construye eventos/recibo y acumula escrituras transaccionales. IDs de eventos derivan de la secuencia; `OnlineWrites.validate` comprueba `lastSequence + events.size`, secuencias consecutivas, causación y propiedades de Match. Los intentos abortados no publican sus eventos/History/recibo; publicación y ACK están después de commit. Un retry que encuentra un recibo no vuelve a ejecutar dominio. El presupuesto de retries puede agotarse: no se promete que ambas respuestas lleguen pese a fallos de transporte; una repetición posterior sigue el recibo durable.

**No es pureza matemática completa:** se consulta `clock.instant()` en cada callback; SecureRandom se consume al repartir/seleccionar y puede avanzar incluso en un intento abortado. Se reconstruye el builder local por intento; solo el resultado confirmado es durable. No se debe memoizar una decisión anterior ni emitir WS/ACK/efectos externos dentro del callback. El no determinismo de intentos abortados exige pruebas explícitas de que no se publican y Replay usa exclusivamente los eventos confirmados.

La interfaz genérica tiene además matices fuera de gameplay: `connection` consulta `connected()` dentro del callback; el flujo de reconciliación puede emitir `LegacyReconciliationAudit.decision` al evaluar gates, incluso antes de commit. Esas decisiones de diagnóstico pueden repetirse; no son prueba de migración completada. `auditLegacyCompleted` se llama desde publicación posterior al commit. No se declara que todos los callbacks del repositorio sean puros ni se modifica S7-01R/S7-04/S7-05.

## 7. Pruebas existentes y las necesarias

Inventario estático, sin ejecución nueva:

| Área | Pruebas existentes inspeccionadas |
|---|---|
| Revisión distinta | GroupedCommandReadsEmulatorTests:149, `prior revision change is rejected before domain` |
| Revisión congelada con avance de otra instancia entre retries | GroupedCommandReadsEmulatorTests:157 |
| Dos clientes, duplicado o comandos diferentes | GroupedCommandReadsEmulatorTests:174; FirestoreEmulatorTests:149 |
| Relecturas y ocho intentos configurados | GroupedCommandReadsEmulatorTests:139 |
| Dos turnos desde una revisión / mismo jugador | OnlineMatchTests:98 y :164 |
| Idempotencia, fingerprint y UID | OnlineMatchTests:156; prueba del batch que mantiene atajo de recibo |
| Estado final, History y secuencia | OnlineMatchTests:175; FirestoreEmulatorTests:116 |
| Turno, timeout y frontera de abandono | OnlineTurnTests:38, :84, :116 |
| Dos instancias cancelando / reconexión contra expiración | AllAbandonedEmulatorTests:75, :107 |
| Versionado y gates | AbandonedReconciliationGateTests; SingletonReconciliationTests:26 |

Antes de autorizar cualquier alternativa harían falta aserciones específicas, no suponer que las pruebas generales de finalización cubren todos estos interleavings:

1. Dos selecciones diferentes válidas en N y revisión compartida: B debe respetar el rechazo actual tras commit A.
2. Cambio de conexión de otro asiento entre prelectura/primer callback y entre callbacks; el comando seguiría legal, pero debe conservar la precondición.
3. Retry con cambio de turno y con ciclo que devuelve el turno al actor; no reevaluar silenciosamente contra otra revisión.
4. Mismo y distinto jugador, comandos diferentes preparados en N, en dos instancias separadas.
5. Duplicados concurrentes, fingerprint incompatible y duplicado tras finalización: una transición, mismo recibo válido, cero getAll innecesarios en short circuit.
6. Completion por otra transacción antes del primer callback y durante retry: cero eventos/History extra.
7. Gameplay contra expiración de asiento, cancelación all-abandoned y reconexión antes/después de grace, incluyendo lifecycle version 0 con gate cerrado y version 1 normal.
8. ABORTED después de construir eventos/repartir, agotamiento del presupuesto y fallo de commit: cero publicación externa previa, una sola secuencia durable/History/recibo.
9. Runtime ausente/corrupto y no participante, incluido intento de repetir recibo: conservar precedencia y autorización actuales; igualdad de errores no se presume.

No se añaden esos tests ahora. Los contraejemplos ya rechazan A; no hace falta ejecutar carga para establecerlo.

## 8. Camino conservado, alcance y compatibilidad

`PRIOR_RUNTIME → existencia/pertenencia/fijar N → RECEIPT → duplicado retorna resultado previo; si nuevo, TX_GET_ALL(runtime, turnWork) → comprobar N → dominio → validación → persistencia atómica → publicación → ACK`.

Comando nuevo normal: **3 rondas, 4 documentos**. Duplicado: prelectura + recibo, **2 rondas, 2 documentos**, sin getAll. BeginTransaction/Commit no se contabilizan como rondas de lectura. Retry repite lecturas de la transacción; no repite la prelectura de esa invocación.

Impacto hipotético de eliminar realmente pre-read: **MEDIUM**, únicamente direccional según S7-13 (media ~108,85 ms y p95 213 ms a 60 en la lectura previa). No se resta ese p95 del ACK ni se promete pasar 60. Impacto del camino recomendado sin cambio: ninguno. Riesgo de quitar la protección incorrectamente: **HIGH**.

Componentes que tocaría una propuesta de eliminación, **no autorizada ni preparada para implementar**: `OnlineMatchService.command`; contrato `OnlineRepository.transact` y sus implementaciones Firestore/in-memory; creación/validación de recibos; invariantes del engine; instrumentación `FirestorePhaseTiming` y expectativas de conteos; tests enumerados. Auditar también join/timeout/connection/abandon antes de cambiar una interfaz compartida. Handler ACK debe seguir posterior a persistencia; cliente DTO no se cambia.

**SCHEMA_MIGRATION_REQUIRED=NO para la decisión de conservar.** No se alteran partidas activas, recibos, History, Replay ni lifecycle version/gates. Una opción A podría no requerir cambio de formato documental y aun así ser incompatible en comportamiento; ausencia de migración no demuestra equivalencia.

## 9. Próximo paso tras rechazar eliminación

No se propone un workaround de revisión. El siguiente experimento candidato, **solo propuesto**, es instrumentación diagnóstica que separe la espera de la lectura de RECEIPT de la espera de GET_ALL dentro de las dos rondas transaccionales existentes. S7-13 aún las agrega; acquisition y finalización también crecen. Ese desglose permitiría distinguir si el coste se concentra en una de las dos llamadas antes de decidir otro rediseño. Su autorización e implementación serían una tarea aparte; aquí no se modifica ni ejecuta nada. No se propone simultáneamente CPU A/B ni migración de región.

## 10. Salida requerida

Los flags de seguridad siguientes describen **el camino actual conservado**; la incompatibilidad de A se declara explícitamente y no recibe PASS.

```text
SERVER-7 S7-14 AUTHORITATIVE REVISION DESIGN REVIEW
===================================================
PRIOR_RUNTIME_READ_CALLSITE=OnlineMatchService.command:68 -> OnlineRepository.read:119
PRIOR_RUNTIME_FIELDS_USED=match.lastSequence; participants.playerUid/seatIndex
PRIOR_RUNTIME_REVISION_FIELD=match.lastSequence
PRIOR_RUNTIME_OTHER_PURPOSES=ID_VALIDATION; EXISTENCE/DECODE; PRE_RECEIPT_MEMBERSHIP
EXPECTED_REVISION_DATA_FLOW=PRE_READ_N -> transact(expectedSequence=N) -> EACH_CALLBACK_COMPARE_TX_RUNTIME_N -> RECEIPT_N_PLUS_1_TO_RESULT -> ATOMIC_COMMIT -> PUBLISH -> ACK
CURRENT_REVISION_INVARIANT=NEW_COMMAND_MUST_APPLY_AT_SERVER_PREOBSERVED_SEQUENCE; N_FIXED_ACROSS_RETRIES; VALID_RECEIPT_SHORT_CIRCUITS
REVISION_SEMANTICS=APPLICATION_EVENT_SEQUENCE_NOT_FIRESTORE_DOCUMENT_VERSION
TX_RUNTIME_CONTAINS_REVISION=YES_BUT_NOT_INDEPENDENT_PREOBSERVED_N
COMMAND_CARRIES_EXPECTED_REVISION=NO
CLIENT_SUPPLIES_REVISION=NO
SERVER_DERIVES_REVISION_BEFORE_TRANSACTION=YES
SERVER_AUTHORITATIVE_REVISION=YES
RECEIPT_SHORT_CIRCUIT_REQUIRED=YES
OPTION_A_TRANSACTION_ONLY_REVISION=UNSAFE_FOR_CURRENT_CONTRACT
OPTION_B_EXISTING_AUTHORITATIVE_REVISION_SOURCE=NONE_IDENTIFIED
OPTION_C_RECEIPT_REVISION=UNAVAILABLE_FOR_NEW_COMMAND
PROCESS_LOCAL_REVISION_AS_AUTHORITY=REJECTED
REDIS_AS_REVISION_AUTHORITY=OUT_OF_SCOPE
MULTI_INSTANCE_REVISION_SAFETY=PRESERVED_CURRENT; NOT_EQUIVALENT_IN_A
CONCURRENT_COMMANDS_SAFE=YES_CURRENT; REVISION_CONTRACT_NOT_PRESERVED_IN_A
TURN_CHANGE_RACE_SAFE=YES_CURRENT; A_REJECTS_IF_NO_LONGER_DOMAIN_VALID_BUT_MAY_REBASE
MATCH_COMPLETION_RACE_SAFE=YES_CURRENT; DOMAIN_GUARD_REQUIRED_IN_ANY_VARIANT
ABANDONMENT_RACE_SAFE=YES_CURRENT; DOMAIN_GUARDS_REQUIRED_IN_ANY_VARIANT
CONCURRENT_DUPLICATE_SAFE=YES_CURRENT_WITH_DURABLE_RECEIPT_AND_RETRY_LIMITS
SEQUENCE_GUARANTEE_PRESERVED=YES_CURRENT; ORDER_ALONE_DOES_NOT_REPLACE_FROZEN_REVISION
TRANSACTION_CALLBACK_RETRY_SAFE=GAMEPLAY_DURABLE_EFFECTS_YES; NOT_MATHEMATICALLY_PURE; GENERIC_CALLBACK_CAVEATS_DOCUMENTED
EVENT_CREATION_RETRY_SAFE=YES_CURRENT_ATTEMPT_LOCAL_UNTIL_ATOMIC_COMMIT
RECEIPT_STATE_ATOMICITY=YES
EXISTING_REVISION_TEST_COVERAGE=STATIC_INVENTORY_IN_REPORT; NO_NEW_EXECUTION
MISSING_TESTS=STARTER_AND_CONNECTION_COUNTEREXAMPLES; TURN_RETRY; DUPLICATE; COMPLETION; ABANDONMENT; TWO_INSTANCE; ABORTED_EFFECTS; ERROR_PRECEDENCE
PROPOSED_READ_SEQUENCE=KEEP_PRIOR_RUNTIME -> RECEIPT -> TX_GET_ALL -> FROZEN_REVISION_CHECK -> DOMAIN -> ATOMIC_PERSISTENCE -> ACK
TARGET_REMOTE_READ_ROUNDS=3
TARGET_DOCUMENT_READS_PER_NEW_COMMAND=4_WITH_ONE_ATTEMPT
EXPECTED_PERFORMANCE_IMPACT=MEDIUM_HYPOTHETICAL_REMOVAL; NONE_FROM_RECOMMENDED_NO_CHANGE
CORRECTNESS_RISK=HIGH
PROPOSED_FILES_OR_COMPONENTS=NONE_TO_CHANGE; HYPOTHETICAL_BLAST_RADIUS_IN_REPORT
SCHEMA_MIGRATION_REQUIRED=NO_FOR_RETAINED_DESIGN
BACKWARD_COMPATIBILITY=PRESERVED_BY_NO_CHANGE; A_NOT_BEHAVIORALLY_EQUIVALENT
DECISION=REMOVE_PRIOR_RUNTIME_UNSAFE
S7_15_IMPLEMENTATION_PLAN_READY=NO
NEW_LOAD_EXECUTED=NO
BACKEND_SOURCE_CHANGED=NO
CLIENT_SOURCE_CHANGED=NO
SERVER_CONFIGURATION_CHANGED=NO
COMMIT=NONE
PUSH=NONE
S7_14_SUCCESS=YES
NEXT=S7-14 REVIEW
```
