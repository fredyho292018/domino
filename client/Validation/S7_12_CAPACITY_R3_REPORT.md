# SERVER-7 / S7-12 — Capacity R3

Prueba escalonada del SHA autorizado, sin optimización, cambios de recursos, despliegues o reinicios. Los clientes usan exclusivamente identidades LOAD. El cálculo de los gates resta los histogramas al inicio de cada etapa; no utiliza el acumulado de etapas anteriores. Se conserva por separado la cola posterior a la decisión de parada en el análisis completo.

Antes de lanzar usuarios, un recorte de referencia dejó menos de 60 segundos y fue rechazado. No inició partidas. Se preservó ese intento y se amplió solamente la selección temporal del colector. La referencia válida está en baseline-resources.json.

COMMIT_P95 representa la cola de finalización del SDK, no una medición aislada del commit remoto. Las tres rondas se comprueban mediante contadores instrumentados de llamadas, no por paquetes gRPC. Los gauges WS corresponden al Swarm. La revisión de corrección verifica secuencia, causalidad y estado raíz/runtime; no sustituye reconstrucción completa de Replay.

La recuperación usa el ciclo normal, 360 segundos de espera, dos descubrimientos autoritativos y lectura de Redis. No se borraron registros ni se abrieron gates. SERVER-6 conserva History y Replay. Los detalles de percentiles p50/p95/p99/max, intentos, reintentos, CPU/RAM/GC y recursos Redis están en Generated/S712/stage-details.json; los logs y la evidencia original permanecen en ese directorio.

Los ACK de la salida resumida corresponden al histograma exacto utilizado para decidir el gate. El análisis completo por etapa incluye además la espera de descubrimiento antes de lanzar el siguiente grupo; por eso sus percentiles pueden diferir ligeramente. Ambos conjuntos quedan separados en stage-details.json. Los intentos/reintentos desglosados corresponden a comandos correlacionados con ACK; las muestras de servidor sin ACK se conservan y cuantifican. Un STOP no permite inferir la capacidad máxima ni certificar 100 usuarios.

```text
SERVER-7 S7-12 CAPACITY R3
==========================
DEPLOYED_SHA=5a74d7917fc7fb0baa04bbdf2c8fc45de5a13f14
RUN_ID=309c98df-b162-40b8-aa0a-0a9e4b9b8a92
TARGET_USERS=100
AUTHENTICATED_USERS=60
PEAK_ACTIVE_WS=60
OBSERVED_REMOTE_READ_ROUNDS=3

20_USERS:
ACK_P50=401
ACK_P95=839
ACK_P99=1259
SERVER_P95=740
READ_WAIT_P95=271
COMMIT_P95=166
CPU_QUOTA_AVG=10.247
CPU_QUOTA_PEAK=37.872
ACK_GATE=PASS

40_USERS:
ACK_P50=454
ACK_P95=920
ACK_P99=1160
SERVER_P95=800
READ_WAIT_P95=300
COMMIT_P95=187
CPU_QUOTA_AVG=16.671
CPU_QUOTA_PEAK=46.265
ACK_GATE=PASS

60_USERS:
ACK_P50=700
ACK_P95=1262
ACK_P99=1482
SERVER_P95=1106
READ_WAIT_P95=392
COMMIT_P95=260
CPU_QUOTA_AVG=28.573
CPU_QUOTA_PEAK=50.946
ACK_GATE=FAIL

80_USERS:
STATUS=NOT_RUN

100_USERS:
STATUS=NOT_RUN

MATCHES_STARTED=15
MATCHES_COMPLETED=1
MATCHES_CANCELLED=14
MATCHES_FAILED=0
MATCHES_ACTIVE_AFTER=0
MATCHES_UNKNOWN=0
CLIENT_OBSERVED_MATCHES=15
AUTHORITATIVE_DISCOVERED_MATCHES=15
CLIENT_MISSED_MATCHES=0
REGISTRY_UNION_MATCHES=15
UNCLASSIFIED_LOAD_MATCHES=0
TRANSACTION_ATTEMPTS=2005
TRANSACTION_RETRIES=0
CORRELATED_CLIENT_SAMPLES=2004
CLIENT_TIMING_SAMPLES=2004
SERVER_TIMING_SAMPLES=2005
UNPAIRED_SERVER_SAMPLES=1
OVERALL_ACK_SAMPLES=2004
OVERALL_ACK_P50=524
OVERALL_ACK_P95=1161
OVERALL_ACK_P99=1369
MATCHMAKING_P95=5568
HTTP_5XX=0
HTTP_429=0
AUTH_FAILURES=0
WS_CONNECT_FAILURES=0
WS_ABNORMAL_CLOSES=0
COMMAND_FAILURES=0
SEQUENCE_REGRESSIONS=0
DUPLICATE_COMMAND_APPLICATIONS=0
INVALID_TURN_ACCEPTANCES=0
UNAUTHORIZED_DELIVERIES=0
MATCH_CORRUPTIONS=0
REDIS_EVICTIONS=0
REDIS_MEMORY_REJECTIONS=0
OOM_EVENTS=0
API_RAM_PEAK=968355840
JVM_HEAP_PEAK=410636792
JVM_GC_COUNT_DELTA=12
JVM_GC_TIME_DELTA=428
HOST_RAM_MIN_AVAILABLE=10213867520
CPU_STOP_TRIGGERED=NO
MEMORY_STOP_TRIGGERED=NO
CRITICAL_STOP_TRIGGERED=NO
GLOBAL_STOP_TRIGGERED=YES
GLOBAL_STOP_STAGE=60
GLOBAL_STOP_REASON=LATENCY_STAGE_GATE_commandAckLatency
REPLAY_SAMPLE_SIZE=0
REPLAY_SAMPLE_RESULT=NOT_RUN_INSUFFICIENT_COMPLETED_MATCHES
SERVER6_REGRESSION=PASS
POST_LOAD_ACTIVE_MATCHES=0
POST_LOAD_PENDING_WORK=0
POST_LOAD_ACTIVE_RENEWAL=0
POST_LOAD_REDIS_CONVERGENCE=PASS
ORPHAN_SWARM_PROCESSES=0
CONFIRMED_RESOURCE_LEAKS=0
100_CONCURRENT_USERS_VALIDATED=NO
SERVER7_250_STARTED=NO
SOURCE_CHANGE=NO
SERVER_CONFIGURATION_CHANGED=NO
COMMIT=NONE
PUSH=NONE
S7_12_SUCCESS=NO
NEXT=S7-12 REVIEW
```
