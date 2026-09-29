# SERVER-7 S7-18 LOCAL WRITE PREPARATION REVIEW

La frontera `allBufferedWrites` es válida como **tiempo transcurrido de preparación local**, no como tiempo puro de CPU. Dentro se construyen mapas/JSON, se convierten valores a protobuf y se almacenan 20 operaciones en el buffer del SDK. No incluye la espera inicial de la transacción, las lecturas anteriores ni el commit remoto posterior.

La causa concreta de **1.304,984 → 49,058 ms no puede establecerse con la evidencia existente**. Hay trabajo síncrono de serialización y monitores locales reales, pero no mediciones por grupo, perfiles de CPU, eventos de contención ni nombres/timestamps de clases o métodos compilados. S7-18B queda diseñado, **no implementado ni ejecutado**. S7-15 sigue sin estar listo para repetirse.

## Evidencia revisada

Se revisaron las fuentes exactas incluidas en la imagen S7-17, su manifiesto y las capturas existentes. Dependencias confirmadas dentro de `image-domino.jar`: **google-cloud-firestore 3.42.0**, **tools.jackson databind/Kotlin 3.1.5**, **protobuf-java 4.33.2**. El `jackson-databind 2.21.5` también presente no es el mapper `tools.jackson` utilizado por `GameCatalogCodec`.

Se descargó únicamente el [source JAR de Firestore 3.42.0](https://repo.maven.apache.org/maven2/com/google/cloud/google-cloud-firestore/3.42.0/google-cloud-firestore-3.42.0-sources.jar), se inspeccionó bytecode del binario instalado y se consultaron las fuentes Jackson ya disponibles en caché. No se inició la aplicación, un benchmark, una partida, un despliegue o una lectura remota de datos del juego.

Fuentes principales:

- [Fronteras de preparación y commit](D:/Fredy/development/2026/domino/server/domino/src/main/kotlin/com/teamfho/domino/online/OnlineRepository.kt:79).
- [stateMap y workMap](D:/Fredy/development/2026/domino/server/domino/src/main/kotlin/com/teamfho/domino/online/OnlineRepository.kt:111).
- [MatchCodec](D:/Fredy/development/2026/domino/server/domino/src/main/kotlin/com/teamfho/domino/match/MatchRepository.kt:24) y [GameCatalogCodec](D:/Fredy/development/2026/domino/server/domino/src/main/kotlin/com/teamfho/domino/catalog/GameCatalogCodec.kt:10).
- [UpdateBuilder del SDK exacto](D:/Fredy/development/2026/domino/client/Validation/Generated/S718/sdk/com/google/cloud/firestore/UpdateBuilder.java:147), [DocumentSnapshot](D:/Fredy/development/2026/domino/client/Validation/Generated/S718/sdk/com/google/cloud/firestore/DocumentSnapshot.java:81), [UserDataConverter](D:/Fredy/development/2026/domino/client/Validation/Generated/S718/sdk/com/google/cloud/firestore/UserDataConverter.java:114).
- [Commit posterior al callback](D:/Fredy/development/2026/domino/client/Validation/Generated/S718/sdk/com/google/cloud/firestore/ServerSideTransactionRunner.java:231).

## Frontera exacta

Inicio: `CreationTrace.phase("allBufferedWrites")` toma `System.nanoTime()` inmediatamente antes de entrar al bloque cuyo primer statement es `tx.create(root(id), MatchCodec.map(state.match))`.

Fin: toma el segundo timestamp al salir del bloque, después del retorno de `tx.create(creation(id), mapOf("status" to "COMMITTED", "matchId" to id))`. El registro del agregado bajo el monitor de la traza ocurre **después** del timestamp final.

Incluye evaluación de referencias y argumentos, conversiones, llamadas SDK locales, bucles y marcadores M6/M7. El registro interno del subintervalo `assignmentBufferedWrites` también está dentro del intervalo exterior. Incluye cualquier desplanificación del hilo, safepoint, GC o espera de monitor durante ese trabajo. No se midió por separado la perturbación de la propia instrumentación.

Excluye:

- `runTransaction`/BeginTransaction y espera hasta el primer callback.
- Lectura del recibo, cuatro asignaciones y los dos runtimes previos observados.
- Creación del estado del dominio, reparto, generación de eventos, UUID de reserva y aleatorización de jugadores.
- Retorno del callback y `transaction.commit()` del runner; el `.get(30, ...)` exterior está en el hilo que espera la transacción, fuera del stopwatch local del callback.
- Redis `complete`, presencia posterior y publicación/entrega.

Ambas capturas contienen **un callback y una ejecución de `allBufferedWrites`**. No se mezclaron varios intentos en los valores comparados. Si hubiera retries, el contador actual acumularía sus duraciones; ese caso no ocurrió.

`LOCAL_PREPARATION_TIMING_BOUNDARY_VALID=YES`, con la definición anterior. No existe evidencia de inclusión accidental de espera de transacción remota.

## Árbol de llamadas ejecutado

Árbol de llamadas de aplicación y expansión de las rutas SDK pertinentes. Los helpers recursivos se muestran una vez; no es una traza dinámica de cada método interno de Jackson/JVM.

```text
callback(tx), después de todas las lecturas
└─ measured("allBufferedWrites") → CreationTrace.phase
   ├─ ROOT ×1
   │  ├─ root(id) → MatchIds.document → db.document
   │  ├─ MatchCodec.map(match) → GameCatalogCodec.map
   │  │  ├─ mapper.writeValueAsString(match)
   │  │  └─ mapper.readValue(json, Map.class)
   │  └─ tx.create(reference, Map) → performCreate [A]
   ├─ RUNTIME ×1
   │  ├─ root(id).collection("runtime").document("authoritative")
   │  ├─ stateMap(state)
   │  │  ├─ GameCatalogCodec.map(state)
   │  │  │  ├─ mapper.writeValueAsString(OnlineState)
   │  │  │  └─ mapper.readValue(json, Map.class)
   │  │  ├─ filterKeys: excluir abandonmentLifecycleVersion del JSON
   │  │  ├─ mapper.writeValueAsString(filteredMap)
   │  │  └─ mapOf(stateJson, abandonmentLifecycleVersion)
   │  └─ tx.create(reference, Map) → [A]
   ├─ PARTICIPANTS ×4
   │  ├─ referencia players/{seat}
   │  ├─ MatchCodec.map(participant) → JSON → Map
   │  └─ tx.create → [A]
   ├─ INITIAL_EVENTS ×7
   │  ├─ referencia events/{eventId}, ID ya construido
   │  ├─ MatchCodec.map(event) → JSON polimórfico → Map
   │  └─ tx.create → [A]
   ├─ ROUND ×1
   │  ├─ referencia rounds/{roundNumber}
   │  ├─ MatchCodec.map(round) → JSON → Map
   │  └─ tx.create → [A]
   ├─ TURN_WORK ×1
   │  ├─ work(id) → db.document
   │  ├─ workMap: listas, min de fechas, Timestamp, lista de UID
   │  └─ tx.create → [A]
   ├─ mark(M6)
   ├─ ASSIGNMENTS ×4, dentro de assignmentBufferedWrites
   │  ├─ referencia ya construida antes de las lecturas
   │  ├─ mapOf("matchId" to id)
   │  └─ tx.set(Map) → SetOptions.OVERWRITE → performSet [B]
   ├─ mark(M7)
   └─ RECEIPT ×1
      ├─ creation(id) → db.document
      ├─ mapOf("status" to "COMMITTED", "matchId" to id)
      └─ tx.create → [A]

[A] UpdateBuilder.performCreate
 ├─ DocumentSnapshot.fromObject
 │  └─ por campo: CustomClassMapper.convertToPlainJavaTypes
 │     → UserDataConverter.encodeValue recursivo → Value/MapValue/ArrayValue
 ├─ convertToFieldPaths → DocumentTransform.fromFieldPathMap
 ├─ DocumentSnapshot.toPb → Write.Builder/Document.Builder/putAllFields
 ├─ precondición exists(false), transformaciones si las hubiera
 └─ addWrite [C]

[B] UpdateBuilder.performSet (OVERWRITE, sin merge ni field mask)
 ├─ convertToFieldPaths → expandObject
 ├─ DocumentSnapshot.fromObject → conversión de valores/protobuf
 ├─ DocumentTransform.fromFieldPathMap
 ├─ DocumentSnapshot.toPb → Write.Builder
 └─ addWrite [C]

[C] UpdateBuilder.addWrite
 ├─ write.build() → WriteOperation
 ├─ synchronized(writes): comprobar !committed, writes.add, índice
 └─ Transaction.wrapResult → this

DESPUÉS del intervalo/callback:
 ServerSideTransactionRunner.userFunctionCallback → transaction.commit
 → construcción CommitRequest → sendRequest(commitCallable) → transporte
```

Las llamadas trabajan de forma síncrona sobre el hilo del callback. Construir el protobuf en memoria no equivale a enviar una escritura. La codificación de bytes del RPC y su transporte ocurren después; no toda serialización protobuf posible está dentro del intervalo.

## Inventario inicial verificado

El camino PARTNERS con equipos fijos llama `engine.join` → `MatchStarted` → `deal` → `RoundStarted`, cuatro `HandDealt` y `startDeadline` → `TurnStarted`: **7 eventos** y **1 ronda**. La construcción de esos objetos sucede antes del intervalo; su serialización para persistirlos sucede dentro.

| Grupo | Cantidad | Preparación dentro del intervalo | Relevancia estructural |
|---|---:|---|---|
| Root Match | 1 create | JSON→Map del Match completo, participantes anidados y reglas congeladas; conversión recursiva SDK | Alta como candidato; primero en el bloque |
| Runtime | 1 create | OnlineState→JSON→Map→filtrado→JSON; después SDK recibe principalmente una cadena y un entero | Alta como candidato: copia/conversión de Match, manos, reserva, ronda y estado |
| Participantes | 4 create | Cuatro conversiones JSON→Map y protobuf | Candidato; parte de los tipos puede haberse usado ya al serializar root |
| Eventos iniciales | 7 create | Mapas de MatchEvent y payloads polimórficos; cuatro contienen manos | Alta como candidato de nuevos serializadores, no demostrada por tiempo |
| Ronda | 1 create | JSON→Map de MatchRound y protobuf | Candidato menor por estructura; tipo también aparece en runtime |
| Trabajo programado | 1 create | Tres campos: dueAt, presenceCheckAt, uids; fechas y listas locales | Acotado por estructura; sin temporizador ni programación remota dentro de workMap |
| Asignaciones | 4 set | Cuatro mapas de un campo y buffering SDK | Medido directamente: 9,778 / 2,834 ms |
| Recibo | 1 create | Mapa de dos cadenas y buffering SDK | Acotado por estructura; mismo camino create ya usado antes |
| **Total** | **20** | **16 create + 4 set; 0 update** | **Un commit transaccional compartido posterior** |

Son **13 llamadas a `MatchCodec.map`** (root 1 + participantes 4 + eventos 7 + ronda 1), más **1 a `GameCatalogCodec.map` para runtime**. Cada `map` hace una escritura de JSON y una lectura de JSON. Runtime añade otra escritura de JSON después de filtrar. Eso suma **29 operaciones superiores de encode/decode JSON**, además de las conversiones SDK. Es un conteo estático de llamadas, no de bytes, profundidad, allocaciones o tiempo.

Los modelos revisados son datos en memoria. No se identificaron getters/serializadores de aplicación que consulten red o almacenamiento en este camino. `MatchRuleSnapshot.mode()` y `verify()` no son getters llamados por esta conversión; las reglas congeladas se serializan como sus campos, incluido effectiveModeJson.

## Auditoría de esperas e I/O

| Operación | Resultado dentro de allBufferedWrites |
|---|---|
| `Future.get`, `join`, `await` | No encontrados en la ruta alcanzada. Las lecturas y el Future exterior están fuera. |
| Lecturas Firestore síncronas | Ninguna dentro; `DocumentSnapshot.fromObject` construye un objeto local, no lo descarga. |
| Commit/network I/O de Firestore | No en create/set locales; el runner hace commit tras retornar el callback. |
| Redis | Ninguna llamada dentro. |
| Credenciales | Ninguna lectura/refresh explícito dentro; no se llama transporte autenticado en el buffer. |
| Sleeps/backoff | Ninguno dentro; retries/backoff pertenecen al runner exterior. |
| File I/O de aplicación/SDK de escritura | Ninguno identificado en la ruta. La carga inicial de clases puede implicar leer JARs; no se midió. |
| Monitor SDK | **Sí:** `synchronized(writes)` en addWrite por operación. Existencia no prueba espera. |
| Monitores/cachés Jackson | **Sí:** SerializerCache sincroniza búsquedas/resolución y DeserializerCache usa ReentrantLock al crear deserializadores. La caché compartida permite contención en principio. |
| Monitor de la traza | M6/M7 y registro del subintervalo de asignaciones están dentro; el registro exterior queda fuera. |
| Inicialización JVM | Puede tomar locks implícitos de class initialization y producir safepoints; sin evidencia por evento. |
| Executor | No submit/await de una tarea SDK en create/set; se ejecuta inline. El hilo puede ser desplanificado por el SO. |

Por tanto, **no hay I/O remoto de Firestore oculto identificado**, pero sería incorrecto afirmar que la duración es exclusivamente cómputo o que es imposible bloquear. Los locks y la inicialización son rutas locales de espera posibles. No hay una medida de su contención real.

## Primer uso: candidatos y exclusiones

- **Protobuf / SDK buffering:** materialización síncrona confirmada. Clases de escritura como Write, Document, Precondition y estructuras auxiliares pueden tener costes de primer uso aunque las clases de lectura ya se hayan usado. No conocemos cuáles se cargaron durante el bloque.
- **Firestore CustomClassMapper:** recorre/copía Maps y Lists y conserva tipos simples/especiales. Como la aplicación entrega mapas, cadenas, números, listas y Timestamp, no hay evidencia de que la reflexión BeanMapper sobre los POJO del dominio sea la explicación: Jackson ya los convirtió antes. No confundir los dos mappers.
- **Jackson / reflexión / serializadores:** candidatos reales. El mapper global se había utilizado antes, pero eso no implica que sus serializadores de Match, OnlineState, MatchEvent y payloads estuvieran calientes. Los caches de lectura y escritura y los tipos no son equivalentes.
- **Kotlin/JVM/static initialization:** posible en tipos/helpers aún no usados. No se atribuye un coste concreto ni se deduce que todo el mapper Kotlin se inicializara aquí.
- **stateMap:** candidato específico verificable: serializa el estado completo, lo parsea, filtra y vuelve a serializarlo. Su tiempo propio no fue capturado. El SDK recibe después `stateJson` como String: la conversión detallada del estado no ocurre en el SDK Firestore en ese punto.
- **SecureRandom / UUID:** fuera del bloque. La reserva genera UUID, el servicio baraja jugadores y el engine reparte antes de persistir. No se encontró generación de azar dentro de las llamadas de preparación revisadas. No explica directamente este stopwatch.
- **JIT:** posible contribución, sin atribución. +2.200/+610 ms son contadores agregados de compilación de toda la JVM, no tiempo bloqueado del hilo ni compilación demostrada de estos métodos.

**+59/+6 clases no identifican las clases de este bloque.** La captura abarca desde la construcción de la traza, inmediatamente antes de M2, hasta la contabilidad de M5; incluye perfiles, dominio, lecturas, persistencia y otros hilos de la JVM. También puede incluir clases de la instrumentación. No contiene nombres de clase, classloader, hilo, stack ni timestamp de carga individual.

Los artefactos S7-17 no contienen un registro de compilación por método ni un perfil JFR correlacionado. No es posible reconstruir retrospectivamente qué métodos se compilaron en los 1.304,984 ms. Que el JIT agregado supere un intervalo de pared no demuestra una pausa de esa duración.

## Por qué las asignaciones no explican el delta

| Medición (ms) | Partida 1 | Partida 2 | Primera − segunda |
|---|---:|---:|---:|
| allBufferedWrites | 1.304,983944 | 49,057731 | 1.255,926213 |
| assignmentBufferedWrites | 9,778055 | 2,833618 | 6,944437 |
| Resto del bloque, por sustracción | 1.295,205889 | 46,224113 | **1.248,981776** |
| Finalización SDK posterior al callback | 298,007700 | 276,680419 | 21,327281 |

**El 99,45 % del delta está fuera del subintervalo de asignaciones.** Estas son cuatro mapas mínimos, usan referencias ya construidas y se ejecutan después de las otras 15 escrituras. No son cuatro commits remotos y no convierten el grafo Match/OnlineState/eventos.

El residual incluye root, runtime, participantes, eventos, ronda, work, recibo, instrumentación interna y posibles esperas locales. **Root/runtime/eventos son los candidatos estructuralmente más ricos**, pero ningún grupo está medido individualmente. No se puede asignarles el residual ni excluir con certeza los grupos pequeños: un coste de primera inicialización puede caer en el primer uso de una operación pequeña.

La diferencia de finalización está fuera de este intervalo y no explica los ~1.256 ms locales. Los 10.217 ms históricos tampoco quedan explicados: aquella ejecución carecía de esta descomposición y no se reprodujo.

## S7-18B: diseño mínimo, pendiente de autorización

No se implementó ni se ejecutó. Mantener correlación hash de partida, número de intento y etiquetas constantes; no emitir IDs de jugadores, claves de mapas, JSON, payloads, tokens ni contenido serializado.

1. Conservar allBufferedWrites exterior y contadores de intentos/éxito.
2. Separar grupos **ROOT**, **STATE_MAP/runtime**, **PARTICIPANTS**, **INITIAL_EVENTS**, **ROUND**, **TURN_WORK**, **ASSIGNMENTS**, **RECEIPT**. ROOT es imprescindible aunque no estuviera en la lista inicial: es la primera escritura y puede absorber primer uso.
3. Dentro de cada grupo, medir de forma exclusiva: construcción de referencia, construcción/conversión del mapa y llamada síncrona `SDK_BUFFERING` (`tx.create`/`tx.set`). Evaluar cada referencia y cada mapa exactamente una vez y en el mismo orden actual.
4. En STATE_MAP dividir solo sus operaciones existentes: `STATE_JSON_ENCODE`, `STATE_JSON_DECODE_MAP`, `STATE_FILTER`, `STATE_JSON_REENCODE`. No reemplazar el pipeline ni precalcular fuera del intervalo.
5. En participantes/eventos guardar sum/count/max y, si se necesita identificar primer elemento, ordinal acotado (0–3 / 0–6). Etiquetas de tipo de evento solo como enum fijo; ningún contenido.
6. SDK_BUFFERING contendrá conversión CustomClassMapper, protobuf, transform/mask, Write.build y monitor. No se denominará tiempo de commit, RPC o wire serialization.
7. Comprobar suma de tiempos exclusivos + residual contra el exterior; los totales por grupo contienen SDK_BUFFERING y no se suman de nuevo. Conservar conteos esperados de 16 create/4 set y separar cualquier retry.
8. Si ya está habilitado, leer tiempo CPU del hilo actual al inicio/fin del bloque, sin activar opciones JVM. Si no está disponible: NOT_MEASURED. Diferencia pared−CPU no prueba por sí sola I/O o locks.
9. Pruebas previas: evaluación única de argumentos, excepción original, retorno null, orden, retry, ausencia de datos sensibles, suma sin doble conteo y mismas 20 escrituras/semántica en emulador.
10. Este diseño localiza grupos; **no prueba clases/JIT específicos**. JFR/eventos de carga/compilación/monitores serían una propuesta separada con revisión de alcance y datos si siguiera haciendo falta. No se habilitan ahora.

Ninguna autorización de carga se infiere de este plan. No repetir S7-15, no prewarm, no cambiar pools, schedulers, flags ni cachés. Conservar recibo transaccional, History/Replay, lifecycle S7-01R y ACK.

## Salida requerida

```makefile
SERVER-7 S7-18 LOCAL WRITE PREPARATION REVIEW
LOCAL_PREPARATION_START=MONOTONIC_BEFORE_ROOT_REFERENCE_MAP_AND_TX_CREATE
LOCAL_PREPARATION_END=MONOTONIC_AFTER_RECEIPT_TX_CREATE_RETURNS
LOCAL_PREPARATION_TIMING_BOUNDARY_VALID=YES_ELAPSED_LOCAL_PREPARATION_NOT_PURE_CPU
WRITE_COUNT=20
WRITE_GROUPS=ROOT_1_RUNTIME_1_PARTICIPANTS_4_EVENTS_7_ROUND_1_WORK_1_ASSIGNMENTS_4_RECEIPT_1
BLOCKING_IO_INSIDE_PREPARATION=NO_EXPLICIT_IO_FOUND_JVM_CLASS_LOADING_IO_POSSIBLE
HIDDEN_FIRESTORE_IO_INSIDE_PREPARATION=NO_IN_AUDITED_CREATE_SET_PATH
LOCK_WAIT_INSIDE_PREPARATION=POSSIBLE_MONITORS_CONFIRMED_CONTENTION_NOT_MEASURED
EXECUTOR_WAIT_INSIDE_PREPARATION=NO_EXPLICIT_EXECUTOR_WAIT_FOUND_OS_DESCHEDULING_NOT_EXCLUDED
SERIALIZATION_INSIDE_PREPARATION=YES_JSON_MAP_CONVERSION_AND_PROTOBUF_MATERIALIZATION
FIRESTORE_SDK_LOCAL_BUFFERING=YES_SYNCHRONOUS_16_CREATE_4_SET
CLASS_LOADING_RELEVANCE=POSSIBLE_NO_CLASS_NAMES_OR_BLOCK_SCOPED_EVENTS
JIT_RELEVANCE=POSSIBLE_NO_METHOD_LEVEL_OR_BLOCK_SCOPED_EVIDENCE
SERIALIZER_WARMUP_RELEVANCE=POSSIBLE_NOT_PROVEN
STATE_MAP_RELEVANCE=HIGH_STRUCTURAL_CANDIDATE_TIME_UNKNOWN
PARTICIPANT_RELEVANCE=POSSIBLE_FOUR_MAP_AND_SDK_CONVERSIONS
INITIAL_EVENT_RELEVANCE=HIGH_STRUCTURAL_CANDIDATE_SEVEN_POLYMORPHIC_EVENTS
ROUND_RELEVANCE=POSSIBLE_ONE_MAP_AND_SDK_CONVERSION
TURN_WORK_RELEVANCE=SMALL_LOCAL_MAP_NOT_INDIVIDUALLY_TIMED
ASSIGNMENT_RELEVANCE=6.944_MS_OF_1255.926_MS_DELTA_NOT_PRIMARY
RECEIPT_RELEVANCE=SMALL_LOCAL_MAP_NOT_INDIVIDUALLY_TIMED
PRIMARY_LOCAL_PREPARATION_CAUSE=INCONCLUSIVE_OUTSIDE_ASSIGNMENTS_SUBPHASE_NOT_IDENTIFIED
CONFIDENCE=HIGH_BOUNDARY_INVENTORY_AND_NO_COMMIT_IN_BLOCK_LOW_SPECIFIC_CAUSE
MATCH1_1305MS_EXPLAINED=NO_SUBPHASE_ATTRIBUTION_MISSING
HISTORICAL_10217MS_EXPLAINED=NO
FINER_TIMING_REQUIRED=YES
S7_18B_PLAN_READY=YES_DESIGN_ONLY_NOT_IMPLEMENTED
S7_15_REPEAT_READY=NO
NEW_LOAD_EXECUTED=NO
BACKEND_SOURCE_CHANGED=NO
CLIENT_SOURCE_CHANGED=NO
SERVER_CONFIGURATION_CHANGED=NO
COMMIT=NONE
PUSH=NONE
S7_18_SUCCESS=YES_READ_ONLY_REVIEW_AND_PLAN_COMPLETE
NEXT=S7-18 REVIEW
```
