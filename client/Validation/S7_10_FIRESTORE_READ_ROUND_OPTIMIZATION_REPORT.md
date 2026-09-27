# SERVER-7 / S7-10 — Firestore transaction read-round optimization

**S7_10_SUCCESS=YES — implementación y validación local; pendiente de revisión.**

El comando nuevo conserva las cuatro lecturas documentales requeridas y reduce las rondas secuenciales de lectura de **4 a 3**. La evidencia procede de solicitudes `BatchGetDocuments` emitidas por el SDK real hacia Firestore Emulator, además de las pruebas de persistencia y concurrencia. No se ejecutó carga remota ni se midió una mejora de latencia de producción.

## Cambio mínimo

| Ruta | Antes | Después |
|---|---|---|
| Antes de la transacción | Runtime: fija `expectedSequence` y autoriza al participante | Sin cambios |
| Primera lectura transaccional | Recibo del comando; valida fingerprint y retorna si existe | Sin cambios |
| Tras un recibo ausente | Runtime, después turn-work: dos esperas | `tx.getAll(runtimeRef, workRef)`: una espera |
| Escrituras y ACK | Transición validada, escrituras atómicas, esperar persistencia, publicar y ACK | Sin cambios |

Los dos snapshots se identifican por referencia documental, sin depender del orden de respuesta. Un resultado incompleto falla antes de ejecutar la transición. Ambas lecturas pertenecen al mismo intento y transacción; cada reintento vuelve a obtenerlas. Se conserva el máximo de **8 intentos**, comprobado también por el decorador de pruebas.

`presenceCheckAt` conserva el valor transaccional previo. Si falta el trabajo, se mantiene el fallback existente `updatedAt + 30 s`. Si falta el runtime, se mantiene `MATCH_NOT_FOUND`. Agrupar implica que también se solicita el trabajo cuando el runtime transaccional está ausente; no permite transición ni persistencia parcial y no cambia el error de dominio.

La instrumentación conserva los campos y su significado: `transactionReadCount=3` cuenta documentos (recibo + runtime + trabajo); `transactionReadNanos` mide una vez la espera conjunta, sin duplicarla por contener dos documentos. No cambia el esquema Firestore, el engine, el servicio, la política de reintentos ni el límite de persistencia del ACK.

## Archivos de S7-10

1. `server/domino/src/main/kotlin/com/teamfho/domino/online/OnlineRepository.kt`: agrupar las dos lecturas posteriores al recibo.
2. `server/domino/src/main/kotlin/com/teamfho/domino/realtime/FirestorePhaseTiming.kt`: parámetro de cantidad documental, con valor predeterminado 1.
3. `server/domino/src/test/kotlin/com/teamfho/domino/online/GroupedCommandReadsEmulatorTests.kt`: ocho pruebas nuevas contra el emulador.
4. `server/domino/src/test/kotlin/com/teamfho/domino/online/FirestoreEmulatorTests.kt`: contador compatible con `getAll` y argumentos varargs sin expandir.
5. `server/domino/src/test/kotlin/com/teamfho/domino/realtime/FirestorePhaseTimingTest.kt`: contar dos documentos y una sola duración.
6. Este informe y evidencia bajo `client/Validation/Generated/S710/`.

La comparación con el manifiesto de S7-08 detecta únicamente esos cuatro archivos existentes modificados; la nueva clase de pruebas se registra aparte. Los cambios preexistentes de Unity, Firebase, swarm y fases anteriores se conservaron. HEAD continúa en `a4a2542363d93301bbede958b18bbce9fb3e8d1f`, rama `main`.

## Prueba de rondas reales

El interceptor registra el envío de cada `BatchGetDocumentsRequest` en el canal efectivo del SDK. El test reconstruye la secuencia anterior exclusivamente mediante un decorador de pruebas que sustituye el lote por dos `tx.get` secuenciales; ambas variantes usan el SDK, transacciones y commits reales del emulador. No es una comparación contra un binario desplegado anterior.

| Caso observado | Documentos por solicitud | Rondas de lectura | Documentos solicitados |
|---|---|---:|---:|
| Secuencia anterior reconstruida | `[1,1,1,1]` | 4 | 4 |
| Implementación nueva | `[1,1,2]` | 3 | 4 |
| Duplicado aceptado | `[1,1]` | 2 | 2 |
| Mismo ID con contenido conflictivo | `[1,1]` | 2 | 2 |
| Nuevo comando, primer callback abortado y segundo exitoso | `[1,1,2,1,2]` | 5 | 7 |

Se comprueba que R1 no tiene identificador de transacción, que todas las solicitudes siguientes sí lo tienen y que la segunda solicitud corresponde al recibo. BeginTransaction, Commit y Rollback no son rondas de lectura. Estos conteos no son una estimación de facturación ni una garantía sobre reintentos de transporte en producción.

Evidencia emitida:

```text
S710_WIRE sequential=true rounds=4 documents=4
S710_WIRE sequential=false rounds=3 documents=4
```

[XML de las ocho pruebas](D:/Fredy/development/2026/domino/client/Validation/Generated/S710/emulator-focused/TEST-com.teamfho.domino.online.GroupedCommandReadsEmulatorTests.xml).

## Correctness y pruebas

| Contrato | Evidencia ejecutada |
|---|---|
| Comando nuevo, recibo primero, duplicado y conflicto | Nueva prueba de solicitudes reales; duplicado no retorna escrituras ni lee el lote |
| Duplicado concurrente y dos comandos con revisión común | Dos clientes Firestore independientes, dos contextos de servicio: una sola transición; duplicado devuelve el mismo recibo; carrera distinta rechaza con `STALE_COMMAND` |
| Revisión N obsoleta | Otro comando avanza el runtime; se rechaza antes de ejecutar el dominio y no se crea recibo |
| Reintentos | Primer callback abortado, segundo vuelve a leer ambos documentos; commit único. Otra prueba avanza la revisión desde una segunda instancia entre callbacks y confirma que la revisión inicial continúa fija |
| Turn-work | Orden de snapshots invertido, conservar `presenceCheckAt` y `dueAt`; recuperación de turno expirado tras recrear servicio y trabajadores concurrentes |
| Fallo parcial del lote | Se inyecta fallo después de leer cada miembro por separado, y lote incompleto. Cero transiciones; runtime, raíz y trabajo intactos; cero recibos/eventos nuevos |
| Documentos ausentes | Trabajo ausente mantiene fallback de 30 s; runtime ausente mantiene `MATCH_NOT_FOUND` |
| Secuencia, turno y autorización | `OnlineMatchTests`, `OnlineTurnTests`, `OnlineHttpTests`, `OnlineTransportTests`, pruebas WebSocket y seguridad |
| Finalización normal | Duelo completo persistido compara estado, eventos e History; contratos locales de finalización, reglas y puntuación |
| Abandono | `AllAbandonedTests` y cuatro `AllAbandonedEmulatorTests`: CANCELLED único, no History normal, borrar trabajo, carreras entre instancias y reconexión/grace |
| History y Replay | `MatchHistoryHttpTests`, `EntitlementHistoryTests`, `ReplayTests`, `ReplayHttpTests`, `ReplayEmulatorTests`: autorización, paginación, reconstrucción y fallos cerrados |
| ACK e instrumentación | `AckPhaseTimingTest`, `FirestorePhaseTimingTest`, `ConnectionOutboundTests`, `RealtimeHandlerTests`; servicio y handler sin cambios S7-10 |

Ejecuciones del 27 de septiembre de 2026:

- Backend `:test`: **678 descubiertas; 654 pasaron; 24 omitidas; 0 fallos**. Las omisiones son 23 pruebas opcionales con Redis y una prueba de paridad con motor local habilitable por entorno.
- Suite completa `emulatorTest`: **58 descubiertas; 55 pasaron; 2 fallaron; 1 omitida** en la primera ejecución. Los dos fallos eran del observador nuevo: `FirestoreOptions.Builder.build()` reemplaza el proveedor de canal al activar el emulador, por lo que el interceptor registraba cero solicitudes. No fueron fallos de persistencia ni de la optimización.
- Tras corregir solamente el observador y añadir la aserción de ocho intentos, repetición de las ocho pruebas nuevas: **8/8 pasaron**, sin omisiones ni fallos.
- Resultado efectivo de las clases del emulador: **57 pasaron, 1 omitida, 0 fallos pendientes**. No se presenta como una única ejecución completa verde. La omisión corresponde a invalidación distribuida opcional con Redis.
- Los runners confirmaron `EMULATOR_PORT_CLEANUP=PASS` y `EMULATOR_PROCESS_CLEANUP=PASS`.

[Resumen con nombres de pruebas y hashes](D:/Fredy/development/2026/domino/client/Validation/Generated/S710/summary.json). Se conservan los XML iniciales, incluidos sus fallos, y los XML finales para trazabilidad. No se ocultó ni sustituyó la evidencia inicial.

## Aislamiento, límites e hipótesis

Las pruebas de persistencia usan `127.0.0.1:18085`, proyecto `demo-domino-f0`, credenciales explícitas del emulador y un JAR con checksum verificado. Las pruebas ordinarias tienen `FIRESTORE_EMULATOR_HOST=127.0.0.1:1` y proyecto `demo-domino-unit`; se excluye la etiqueta `REAL_FIRESTORE`. No se usó ADC ni Firestore real. No hubo SSH ni cambios del servidor durante S7-10.

`SERVER6_REGRESSION=PASS` se refiere a sus contratos funcionales locales/emulados de partida, History, autorización y Replay. No afirma una nueva validación remota de los cinco documentos reales de SERVER-6 ni una prueba manual de Unity; esos datos no fueron leídos ni modificados por esta fase.

Los 78 archivos del manifiesto de evidencia S7-07 conservan sus hashes. Los artefactos de medición S7-08 se conservan; no se reinterpretan como resultados de esta implementación.

Hipótesis: eliminar una espera secuencial debería reducir READ_WAIT. No se promete una mejora en milisegundos o capacidad; falta medirla en una fase posterior autorizada. No se ejecutó carga de 20/40, 100 o 250 usuarios. Las carreras sintéticas de pruebas son validación funcional local, no un benchmark remoto.

```text
SERVER-7 S7-10 FIRESTORE READ-ROUND OPTIMIZATION
================================================
FILES_CHANGED=5_SOURCE_TEST_FILES + REPORT + LOCAL_EVIDENCE
BEFORE_READ_SEQUENCE=PRIOR_RUNTIME -> RECEIPT -> TX_RUNTIME -> TX_TURN_WORK
AFTER_READ_SEQUENCE=PRIOR_RUNTIME -> RECEIPT -> TX_GET_ALL(RUNTIME,TURN_WORK)
PRIOR_RUNTIME_READ_PRESERVED=YES
COMMAND_RECEIPT_CHECK_PRESERVED=YES
BEFORE_REMOTE_READ_ROUNDS=4
AFTER_REMOTE_READ_ROUNDS=3
DOCUMENT_READS_BEFORE=4
DOCUMENT_READS_AFTER=4
TRANSACTIONAL_RUNTIME_STILL_TRANSACTIONAL=YES
TURN_WORK_STILL_TRANSACTIONAL=YES
REQUIRED_DOCUMENT_READS_PRESERVED=YES
DUPLICATE_COMMAND_SHORT_CIRCUIT_PRESERVED=YES
REVISION_RACE_PROTECTION=PASS
MULTI_INSTANCE_CORRECTNESS=PASS
IDEMPOTENCY_REGRESSION=PASS
SEQUENCE_REGRESSION_TESTS=PASS
INVALID_TURN_TESTS=PASS
TURN_WORK_CONSISTENCY=PASS
NORMAL_MATCH_COMPLETION=PASS
ABANDONMENT_LIFECYCLE_REGRESSION=PASS
HISTORY_REGRESSION=PASS
REPLAY_REGRESSION=PASS
SERVER6_REGRESSION=PASS
ACK_PERSISTENCE_SEMANTICS_CHANGED=NO
ACK_PHASE_TIMING_PRESERVED=YES
FIRESTORE_PHASE_TIMING_PRESERVED=YES
FIRESTORE_SCHEMA_CHANGED=NO
TRANSACTION_RETRY_POLICY_CHANGED=NO
REAL_PRODUCTION_FIRESTORE_CALLS=0
NEW_LOAD_EXECUTED=NO
SERVER7_100_RERUN_STARTED=NO
SERVER7_250_STARTED=NO
SERVER_CONFIGURATION_CHANGED=NO
COMMIT=NONE
PUSH=NONE
DEPLOY=NONE
S7_10_SUCCESS=YES
NEXT=S7-10 REVIEW
```
