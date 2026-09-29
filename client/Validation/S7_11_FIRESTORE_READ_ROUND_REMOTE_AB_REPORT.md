# SERVER-7 / S7-11 — Firestore read-round remote A/B

**S7_11_SUCCESS=YES. OPTIMIZATION_RESULT=CLEAR_IMPROVEMENT.**

## Checkpoint y despliegue

Checkpoint `5a74d7917fc7fb0baa04bbdf2c8fc45de5a13f14`, mensaje `perf: reduce Firestore gameplay read rounds`, publicado sin force push en origin/main. Se construyó desde un archivo Git de ese SHA; el JAR extraído de la imagen coincide byte a byte con el compilado. Validación equivalente a `infrastructure/scripts/verify-image.sh`: Linux/amd64, UID 10001, entrypoint, sin credenciales embebidas, escaneo del JAR, contenedores de comprobación sin red y sin arrancar Spring. Los recursos y variables del despliegue se compararon antes/después; Redis mantuvo ID y hora de arranque.

El checkpoint incluyó S7-10 y sus dependencias de instrumentación S7-07/S7-08, que estaban validadas pero sin commit. Sus hashes se comprobaron contra S7-08. Se añadió únicamente el contador observacional `transactionReadRoundCount` para la prueba remota; no hubo otra optimización. Pasaron 13 pruebas de timing y ocho del emulador tras esa adición. El resto de la regresión S7-10 se conserva en su informe.

Archivos incluidos en el commit:

- `client/Validation/S7_10_FIRESTORE_READ_ROUND_OPTIMIZATION_REPORT.md`
- `server/domino/src/main/kotlin/com/teamfho/domino/online/OnlineMatchService.kt`
- `server/domino/src/main/kotlin/com/teamfho/domino/online/OnlineRepository.kt`
- `server/domino/src/main/kotlin/com/teamfho/domino/realtime/AckPhaseTiming.kt`
- `server/domino/src/main/kotlin/com/teamfho/domino/realtime/ConnectionOutbound.kt`
- `server/domino/src/main/kotlin/com/teamfho/domino/realtime/FirestorePhaseTiming.kt`
- `server/domino/src/main/kotlin/com/teamfho/domino/realtime/RealtimeHandler.kt`
- `server/domino/src/test/kotlin/com/teamfho/domino/online/FirestoreEmulatorTests.kt`
- `server/domino/src/test/kotlin/com/teamfho/domino/online/GroupedCommandReadsEmulatorTests.kt`
- `server/domino/src/test/kotlin/com/teamfho/domino/realtime/AckPhaseTimingTest.kt`
- `server/domino/src/test/kotlin/com/teamfho/domino/realtime/FirestorePhaseTimingTest.kt`

El inventario clasifica fuente, pruebas, informe, evidencia local y archivos preexistentes. No se incluyeron Generated, identidades LOAD, archivos Firebase/Unity del usuario ni herramientas Swarm pendientes. Escaneo de indicadores y revisión de los archivos seleccionados: cero secretos detectados.

## Método y prueba remota

Run `c9f9cc95-ff77-4d2d-b0cf-d0d963d1f4f4`. Mismos clientes Swarm (fuente verificada contra S7-08), pool existente de 100 identidades LOAD, grupos 01/02, semillas y pausas, ruta Cloudflare, Firestore TEST, 4 CPU y 4 GiB. Sesiones de 20 y 40, con 120 segundos mínimos de permanencia más descubrimiento autoritativo; sin 60/80/100/250. Los percentiles usan nearest-rank sobre buckets de 1 ms, iguales a S7-08. El gate ACK permanece en 1000 ms y se reporta sin impedir 40 por rendimiento solamente; las condiciones críticas conservan parada.

Antes del benchmark se ejecutó una partida smoke separada con cuatro identidades LOAD del grupo 05. El primer intento falló antes de conectar por `summarySeconds=1`, corregido al mínimo permitido de 2. El smoke válido capturó un ACK y tres rondas/cuatro documentos; después sus clientes se detuvieron y se confirmó recuperación normal tras 360 segundos. Sus muestras se conservan en `smoke-proof.json` y se excluyen del A/B por tiempo. No se suman como etapa de carga.

Se capturó baseline de 60 segundos antes de los clientes. WS de carga=0 y partidas LOAD activas=0 por control de procesos y descubrimiento; el gauge del experimento corresponde a WS autenticados de Swarm, no a todos los posibles usuarios ajenos al ensayo.

En ejecución real, cada comando correlacionado registra `preReadCount=1`, `transactionReadRoundCount=2` y `transactionReadCount=3`: tres esperas de lectura y cuatro documentos incluyendo la lectura previa. Se vincula al ACK del cliente mediante hash de comando. La instrumentación cuenta llamadas/esperas de aplicación, no paquetes ni retransmisiones gRPC; el test de S7-10 demostró que `getAll` usa una petición BatchGetDocuments con ambos documentos. No se infiere el resultado solamente del SHA.

Muestras correlacionadas: **1105/1105 servidor y 1105 cliente**. La métrica de completion es la cola de finalización del SDK; no demuestra tiempo puro de commit remoto. Se conservan los residuos cliente-servidor negativos (0) y no se recortan para aparentar éxito.

## Comparación

| Usuarios | Métrica p95 | S7-08 (ms) | S7-11 (ms) | Delta ms | Delta % |
|---|---|---:|---:|---:|---:|
| 20 | clientAck | 887 | 736 | -151 | -17.02% |
| 20 | serverTotal | 815 | 662 | -153 | -18.77% |
| 20 | readWait | 344 | 240 | -104 | -30.23% |
| 20 | commitCompletion | 160 | 169 | +9 | +5.62% |
| 40 | clientAck | 1244 | 942 | -302 | -24.28% |
| 40 | serverTotal | 1079 | 837 | -242 | -22.43% |
| 40 | readWait | 465 | 286 | -179 | -38.49% |
| 40 | commitCompletion | 223 | 187 | -36 | -16.14% |


| Usuarios | Fase Firestore | p95 después (ms) |
|---|---|---:|
| 20 | total | 613 |
| 20 | transactionTotal | 528 |
| 20 | transactionRead | 240 |
| 20 | domain | 4 |
| 20 | completionTail | 169 |
| 20 | postCommitFirestore | 0 |
| 40 | total | 774 |
| 40 | transactionTotal | 628 |
| 40 | transactionRead | 286 |
| 40 | domain | 7 |
| 40 | completionTail | 187 |
| 40 | postCommitFirestore | 0 |

## Recursos

| Usuarios | Recurso | Antes | Después |
|---|---|---:|---:|
| 20 | seconds | 167.625 | 171.734 |
| 20 | cpuQuotaAvg | 17.695 | 14.237 |
| 20 | cpuQuotaPeak | 65.232 | 70.753 |
| 20 | hostCpuAvg | 21.072 | 20.175 |
| 20 | apiMemoryPeak | 910364672.000 | 908673024.000 |
| 20 | heapPeak | 387948160.000 | 392321344.000 |
| 20 | gcCount | 5.000 | 3.000 |
| 20 | gcMs | 249.000 | 218.000 |
| 20 | gcMsPerSecond | 1.485 | 1.269 |
| 20 | redisMemoryPeak | 8646656.000 | 9830400.000 |
| 20 | redisCpuAvg | 0.631 | 0.736 |
| 40 | seconds | 151.442 | 166.823 |
| 40 | cpuQuotaAvg | 24.263 | 18.168 |
| 40 | cpuQuotaPeak | 60.473 | 52.951 |
| 40 | hostCpuAvg | 33.269 | 27.037 |
| 40 | apiMemoryPeak | 928571392.000 | 931356672.000 |
| 40 | heapPeak | 386375296.000 | 389241168.000 |
| 40 | gcCount | 4.000 | 4.000 |
| 40 | gcMs | 186.000 | 32.000 |
| 40 | gcMsPerSecond | 1.228 | 0.192 |
| 40 | redisMemoryPeak | 9822208.000 | 7028736.000 |
| 40 | redisCpuAvg | 0.950 | 0.797 |

CPU se expresa como porcentaje de cuota (API=4 CPU; Redis según su límite efectivo conservado). Memoria en bytes; GC en ms y ms/s para considerar distinta duración. Comparación histórica única: las condiciones de red, backend Firestore, calentamiento y número de comandos pueden variar. La clasificación es descriptiva, no significancia estadística ni certificación de capacidad. CLEAR exige al menos 15% menos READ_WAIT en ambas etapas y descenso de ACK/server; MODEST exige descenso de READ_WAIT en ambas; mezcla de signos se clasifica INCONCLUSIVE. No se requiere que completion disminuya.

## Corrección y recuperación

Contadores cliente y auditoría autoritativa: cero regresiones/huecos de secuencia, duplicados, aceptaciones de turno inválido, entregas no autorizadas y violaciones de idempotencia. La auditoría comprueba secuencia, causalidad de comandos, turnos y concordancia de estado raíz/runtime; no vuelve a simular íntegramente cada mano con el engine y no depende solo de partidas observadas por clientes. `10` partidas del benchmark auditadas; estado final `{"COMPLETED": 0, "CANCELLED": 10, "FAILED": 0, "ACTIVE": 0, "UNKNOWN": 0}`. Tras 360 segundos se hicieron dos descubrimientos autoritativos, lectura de Redis sin mutaciones y comprobación de ausencia de Swarm huérfanos. Sin limpieza manual ni apertura de gates.

SERVER-6: antes y después, cinco History, cinco snapshots y tres manifiestos Replay disponibles, utilizando la identidad funcional solo para lectura. No se alteraron sus partidas. API y Redis saludables, gates cerrados, observador temporal detenido. No se cambió CPU, RAM, JVM, Firestore, Redis, Cloudflare, DNS, UFW o IAM; cambió únicamente la imagen autorizada y se usaron diagnósticos temporales.

## Evidencia

[Resumen A/B](D:/Fredy/development/2026/domino/client/Validation/Generated/S711/comparison.json), [análisis](D:/Fredy/development/2026/domino/client/Validation/Generated/S711/analysis.json), [inventario](D:/Fredy/development/2026/domino/client/Validation/Generated/S711/checkpoint-inventory.json), [smoke](D:/Fredy/development/2026/domino/client/Validation/Generated/S711/smoke-proof.json), [registro final](D:/Fredy/development/2026/domino/client/Validation/Generated/S711/registry-confirm.json). Se preservaron los hashes de 83 archivos de S7-08. Los resultados y herramientas operacionales S7-11 quedan locales, fuera del checkpoint de código.

```text
SERVER-7 S7-11 FIRESTORE READ-ROUND REMOTE A/B
==============================================
SOURCE_SHA_BEFORE=a4a2542363d93301bbede958b18bbce9fb3e8d1f
SOURCE_SHA_AFTER=5a74d7917fc7fb0baa04bbdf2c8fc45de5a13f14
COMMIT_SHA=5a74d7917fc7fb0baa04bbdf2c8fc45de5a13f14
COMMIT_MESSAGE=perf: reduce Firestore gameplay read rounds
FILES_COMMITTED=11
SECRETS_IN_CHECKPOINT=0
PUSH=PASS
DEPLOY_IMAGE=cuban-domino-api:5a74d7917fc7fb0baa04bbdf2c8fc45de5a13f14
API_DEPLOY=PASS
API_HEALTH=UP
REDIS_RESTARTED=NO
OBSERVED_REMOTE_READ_ROUNDS=3
TRANSACTION_ATTEMPTS=1105
TRANSACTION_RETRIES=0

20_USERS:
CLIENT_ACK_P95_BEFORE=887
CLIENT_ACK_P95_AFTER=736
CLIENT_ACK_P95_DELTA_MS=-151
CLIENT_ACK_P95_DELTA_PERCENT=-17.02
SERVER_TOTAL_P95_BEFORE=815
SERVER_TOTAL_P95_AFTER=662
SERVER_TOTAL_P95_DELTA_MS=-153
SERVER_TOTAL_P95_DELTA_PERCENT=-18.77
READ_WAIT_P95_BEFORE=344
READ_WAIT_P95_AFTER=240
READ_WAIT_P95_DELTA_MS=-104
READ_WAIT_P95_DELTA_PERCENT=-30.23
COMMIT_COMPLETION_P95_BEFORE=160
COMMIT_COMPLETION_P95_AFTER=169
ACK_GATE_1000MS=PASS

40_USERS:
CLIENT_ACK_P95_BEFORE=1244
CLIENT_ACK_P95_AFTER=942
CLIENT_ACK_P95_DELTA_MS=-302
CLIENT_ACK_P95_DELTA_PERCENT=-24.28
SERVER_TOTAL_P95_BEFORE=1079
SERVER_TOTAL_P95_AFTER=837
SERVER_TOTAL_P95_DELTA_MS=-242
SERVER_TOTAL_P95_DELTA_PERCENT=-22.43
READ_WAIT_P95_BEFORE=465
READ_WAIT_P95_AFTER=286
READ_WAIT_P95_DELTA_MS=-179
READ_WAIT_P95_DELTA_PERCENT=-38.49
COMMIT_COMPLETION_P95_BEFORE=223
COMMIT_COMPLETION_P95_AFTER=187
ACK_GATE_1000MS=PASS
CPU_COMPARISON=20: 17.695 -> 14.237; 40: 24.263 -> 18.168
MEMORY_COMPARISON=20: 910364672.000 -> 908673024.000; 40: 928571392.000 -> 931356672.000
GC_COMPARISON=20: 1.485 -> 1.269; 40: 1.228 -> 0.192
REDIS_COMPARISON=20: 0.631 -> 0.736; 40: 0.950 -> 0.797
SEQUENCE_REGRESSIONS=0
DUPLICATE_COMMAND_APPLICATIONS=0
INVALID_TURN_ACCEPTANCES=0
UNAUTHORIZED_DELIVERIES=0
MATCH_CORRUPTIONS=0
OPTIMIZATION_RESULT=CLEAR_IMPROVEMENT
PRIMARY_EFFECT=READ_WAIT_P95_DECREASED_30.23_PERCENT_AT_20_AND_38.49_PERCENT_AT_40
POST_TEST_ACTIVE_LOAD_MATCHES=0
POST_TEST_PENDING_WORK=0
UNCLASSIFIED_LOAD_MATCHES=0
ORPHAN_SWARM_PROCESSES=0
SERVER6_REGRESSION=PASS
SERVER7_100_RERUN_STARTED=NO
SERVER7_250_STARTED=NO
SERVER_CONFIGURATION_CHANGED=NO
S7_11_SUCCESS=YES
NEXT=S7-11 REVIEW
```
