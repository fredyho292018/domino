# SERVER-7 S7-19 — 200 USER EXPLORATORY STRESS

La ejecución exploratoria terminó y el sistema recuperó su estado, pero **no se alcanzaron 200 conexiones simultáneas ni 50 partidas**. Se lanzaron 200 clientes; el pico observado fue de 163 WebSocket autenticados. Hubo 40 partidas: tres completadas y 37 canceladas después de detener los clientes. No hubo parada de emergencia ni repetición del run.

## Alcance y límites de la evidencia

- Se crearon exactamente 100 identidades nuevas, total 200 distintas, fuera de Git y sin usar las cuatro identidades funcionales. Una muestra de cinco nuevas pasó Auth/REST/WSS sin matchmaking.
- Baseline autoritativo limpio y al menos 60 s de recursos; etapas solicitadas de 20 a 200 cada ≥60 s, último tramo ≥300 s. **Esos 300 s corresponden a 200 clientes lanzados; no son un hold de 200 conexiones reales.**
- No se cambió ni desplegó backend, Unity, recursos, DNS o Cloudflare. Imagen S717 sobre el SHA indicado. No S7-18B, commit, push, nueva carga ni 250 usuarios.
- Antes de cargar se preparó un harness aislado en Generated/S719: límites 200/50, provisionador TEST existente ampliado a grupos 06–10 y recuperación de la misma partida al reconectar con requeue=false. Siete pruebas de registro pasaron. El producto permanece intacto por hash.
- El generador conserva limitaciones: los fallos genéricos ocultan su operación/causa exacta; un cliente fatal detiene su grupo de 20; las cancelaciones de coroutine pueden terminar un cliente. Esto impide atribuir el déficit de admisión exclusivamente al servidor.
- Los ACK de la tabla se calculan por timestamp de finalización, no por el momento de lectura del dashboard. Pequeñas diferencias frente a los anuncios en vivo son de frontera de ventana. Percentiles nearest-rank; 6.602 ACK correlacionados, cero pérdidas del búfer de timing.
- Los cruces son la primera observación del percentil acumulado **dentro de cada etapa**, muestreado aproximadamente cada 2 s; no un instante continuo conocido al milisegundo. El matchmaking excluye el tiempo de Auth/bootstrap anterior a la entrada en cola y desborda el histograma por encima de 60 s.
- WS es una muestra de contadores por proceso, no una instantánea atómica del servidor. ACTIVE_MATCHES se reconstruye de createdAt/finishedAt autoritativos. El inventario de autoridad también añade tráfico de observación.

## Interpretación

A. El ACK p95 supera 1 s en 40 usuarios solicitados, y el p99 supera 3 s en 120. Matchmaking p95 supera 10 s en 100. Solo 20 pasó estos umbrales en esta ejecución corta; eso no certifica capacidad.
B–C. No se encontró un techo sostenido de CPU, heap, memoria de API o Redis. La admisión se degrada antes: a partir de 120 solicitados ya no se completa la entrada del grupo dentro de la etapa. No hay fundamento para declarar 163 como capacidad máxima del backend.
D. Firestore-related path representa 93.5% de la duración media del servidor. Incluye lecturas y finalización del SDK; **SDK_FINALIZATION no equivale a commit remoto puro**. El tiempo adicional del cliente también aumenta en los tramos altos.
E. Pico RX 3.577 Mbps; TX 9.489 Mbps, 3.86% de la referencia de subida de 245,8 Mbps. La referencia de descarga de 364,7 Mbps tampoco es capacidad garantizada. Hubo 428 drops RX, cero TX y cero errores: no prueban causalidad de red.
F. Windows alcanzó 53.76% de CPU, 31.28 GiB usados y solo 549.0 MiB físicos libres; el margen de commit mínimo fue 882.3 MiB. Presión de memoria del generador es un factor posible; no se midieron fallos de página ni su contribución causal. RSS agregado de Swarm: 1.79 GiB pico.
G. Cero violaciones de integridad en los contadores del cliente y en la auditoría autoritativa de las 40 partidas. Tres Replay pasaron secuencia, score, resultado y seek mediante el reductor existente. SERVER-6 permanece accesible.
H. Hubo pérdida de clientes/admisión incompleta, pero no crash-loop, OOM o colapso confirmado del backend. La etiqueta adecuada es degradación de admisión y pérdida de clientes, con causa todavía parcialmente confundida por el harness.
En el tramo de 200 solicitados el ACK p95 baja de 4457 a 2608 ms entre primer y último minuto. **No demuestra estabilización a 200**: el pico de conexiones fue 163 y el final 149. CPU media 49.19% → 46.80%; RAM pico API 936.6 → 977.0 MiB.

## Tabla por etapa

USERS = solicitados. Latencias en ms; RAM/heap en MiB; CPU en % de cuota; REDIS_CPU y red muestran pico de etapa. Los grupos pueden compartir partidas; no se suman contadores de partidas duplicados entre procesos. Véase también [CSV](Generated/S719/stages.csv).

| USERS | ACTIVE_WS | ACTIVE_MATCHES | ACK_P50 | ACK_P95 | ACK_P99 | MATCHMAKING_P95 | SERVER_P95 | READ_WAIT_P95 | SDK_FINALIZATION_P95 | CPU_AVG | CPU_PEAK | API_RAM | JVM_HEAP | REDIS_CPU | REDIS_RAM | HOST_RX_MBPS | HOST_TX_MBPS | RX_DROPS_DELTA | HTTP_5XX | RECONNECTS |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 20 | 20 | 5 | 409 | 584 | 610 | 3879 | 496.1 | 277.9 | 125.1 | 10.88 | 36.43 | 858.1 | 354.0 | 1.78 | 1.93 | 1.296 | 1.563 | 31 | 0 | 0 |
| 40 | 40 | 10 | 618 | 1268 | 1696 | 6554 | 1144.8 | 539.6 | 265.6 | 31.79 | 85.56 | 872.5 | 357.0 | 4.69 | 1.93 | 1.828 | 2.592 | 30 | 1 | 0 |
| 60 | 60 | 15 | 812 | 1416 | 1855 | 7152 | 1255.4 | 632.5 | 272.6 | 39.7 | 66.2 | 885.7 | 364.5 | 4.46 | 1.94 | 2.198 | 3.538 | 30 | 0 | 0 |
| 80 | 80 | 20 | 941 | 1536 | 2471 | 7124 | 1334.1 | 713.7 | 337.3 | 41.33 | 62.95 | 890.1 | 359.6 | 4.94 | 1.95 | 2.85 | 5.554 | 31 | 0 | 0 |
| 100 | 100 | 25 | 1314 | 2234 | 2654 | 10097 | 2015.8 | 1094.7 | 522.8 | 48.51 | 59.29 | 904.1 | 367.4 | 5.89 | 1.97 | 2.748 | 5.868 | 29 | 0 | 0 |
| 120 | 118 | 30 | 1717 | 2847 | 3270 | 11514 | 2534.8 | 1274.7 | 666.7 | 52.39 | 61.26 | 904.3 | 371.8 | 5.33 | 1.98 | 2.29 | 4.012 | 31 | 0 | 0 |
| 140 | 132 | 33 | 1968 | 2685 | 3163 | 20068 | 2453.9 | 1188.1 | 661.8 | 51.62 | 63.46 | 907.1 | 375.8 | 5.88 | 2.0 | 2.389 | 5.717 | 30 | 0 | 0 |
| 160 | 149 | 37 | 2092 | 3355 | 4620 | 16493 | 2980.0 | 1275.1 | 770.6 | 53.21 | 67.13 | 917.9 | 379.7 | 5.36 | 2.03 | 3.577 | 9.489 | 32 | 0 | 0 |
| 180 | 159 | 39 | 2229 | 3712 | 4235 | 36347 | 3492.2 | 1259.5 | 1058.8 | 50.22 | 60.9 | 922.8 | 385.6 | 4.78 | 2.02 | 3.249 | 7.485 | 31 | 0 | 0 |
| 200 | 149 | 37 | 2125 | 4860 | 6224 | OVER_60000_MS | 3211.4 | 1241.4 | 856.6 | 48.84 | 67.14 | 977.0 | 399.9 | 6.38 | 2.03 | 3.253 | 6.884 | 153 | 0 | 1 |

## Throughput y muestras

| USERS | ACK samples | ACK/s | Commands/s | Created/min | Completed/min |
| --- | --- | --- | --- | --- | --- |
| 20 | 116 | 1.868 | 1.755 | 4.830 | 0.000 |
| 40 | 220 | 3.561 | 3.302 | 4.856 | 0.000 |
| 60 | 321 | 5.234 | 5.186 | 4.892 | 0.000 |
| 80 | 416 | 6.904 | 6.672 | 4.979 | 0.000 |
| 100 | 452 | 7.411 | 7.247 | 4.918 | 0.000 |
| 120 | 466 | 7.687 | 7.770 | 4.949 | 0.000 |
| 140 | 525 | 8.653 | 8.323 | 2.967 | 0.000 |
| 160 | 581 | 9.134 | 9.370 | 3.773 | 0.000 |
| 180 | 609 | 9.714 | 9.555 | 1.914 | 0.000 |
| 200 | 2891 | 9.423 | 9.439 | 0.196 | 0.587 |

## Recuperación y cobertura

Orden de cierre de clientes: 2026-09-27T19:44:35.331538+00:00. Salida confirmada: 2026-09-27T19:45:05.364011Z. Se esperaron ≥360 s desde esa confirmación antes de las dos lecturas finales: cero LOAD activos, cero trabajo pendiente y cero sin clasificar. Redis quedó en 31 claves globales, sin claves ni miembros de este run. API/Redis saludables, cero reinicios/OOM, gates cerrados y observadores detenidos.
GC durante carga: 40 colecciones / 3652 ms acumulados. Non-heap pico 217.9 MiB. Redis: 5 conexiones pico, 527 claves pico; INFO RTT p95 34.42 ms (incluye scheduling/lectura, no latencia de comandos de juego). CPU host Linux pico 89.15%.
La cobertura de fallos de conexión no permite devolver un total exacto de WS_CONNECT_FAILURES: hay 323 CLIENT_FAILURE sin causa distinguible, un HTTP 502 explícito, un rechazo WS y un cierre 1008. HTTP 429 = cero observados explícitamente por la API, no una afirmación de cobertura completa de Firebase/red. Una reconexión registrada. No se realizó un experimento adicional para explicar estos fallos.

## Resultado solicitado

```text
SERVER-7 S7-19 — 200 USER EXPLORATORY STRESS
================================================

RUN_ID=9e93b841-cea0-4105-86c8-1e45a95fa3eb
DEPLOYED_SHA=5a74d7917fc7fb0baa04bbdf2c8fc45de5a13f14
DEPLOYED_IMAGE=cuban-domino-api:s717-c77f1035deab
TARGET_USERS=200
LOAD_IDENTITIES_TOTAL=200
ADDITIONAL_LOAD_IDENTITIES=100
PEAK_AUTHENTICATED_USERS=163
PEAK_ACTIVE_WS=163
PEAK_ACTIVE_MATCHES=40
TARGET_MATCHES=50
MATCHES_STARTED=40
MATCHES_COMPLETED=3
MATCHES_CANCELLED_DURING_LOAD=0
MATCHES_CANCELLED_DURING_RECOVERY=37
MATCHES_FAILED=0
MATCHES_UNKNOWN=0
STAGE_20=OBSERVED_62.1s_ACTIVE_WS_AT_END_20
STAGE_40=OBSERVED_61.8s_ACTIVE_WS_AT_END_40
STAGE_60=OBSERVED_61.3s_ACTIVE_WS_AT_END_60
STAGE_80=OBSERVED_60.2s_ACTIVE_WS_AT_END_80
STAGE_100=OBSERVED_61.0s_ACTIVE_WS_AT_END_100
STAGE_120=OBSERVED_60.6s_ACTIVE_WS_AT_END_118
STAGE_140=OBSERVED_60.7s_ACTIVE_WS_AT_END_132
STAGE_160=OBSERVED_63.6s_ACTIVE_WS_AT_END_149
STAGE_180=OBSERVED_62.7s_ACTIVE_WS_AT_END_159
STAGE_200=OBSERVED_306.8s_ACTIVE_WS_AT_END_149
ACK_P95_1S_FIRST_CROSSED_AT=2026-09-27T19:31:22.851062+00:00 USERS=40
ACK_P99_3S_FIRST_CROSSED_AT=2026-09-27T19:35:32.323033+00:00 USERS=120
MATCHMAKING_P95_10S_FIRST_CROSSED_AT=2026-09-27T19:35:02.128000+00:00 USERS=100
CPU_95_FIRST_CROSSED_AT=NOT_CROSSED
API_RAM_90_FIRST_CROSSED_AT=NOT_CROSSED
PEAK_CPU_QUOTA_PERCENT=85.561
MAX_SUSTAINED_CPU_30S=55.631
PEAK_API_RAM=977.00_MiB
PEAK_JVM_HEAP=399.85_MiB
MIN_HOST_AVAILABLE_RAM=9.421_GiB
PEAK_REDIS_CPU=6.383
PEAK_REDIS_RAM=2.035_MiB
REDIS_EVICTIONS=0
REDIS_REJECTIONS=0
PEAK_HOST_RX_MBIT=3.577
PEAK_HOST_TX_MBIT=9.489
UPLOAD_REFERENCE_UTILIZATION=3.860_PERCENT
RX_DROPS_DELTA=428
TX_DROPS_DELTA=0
LOAD_GENERATOR_CPU_PEAK=53.765
LOAD_GENERATOR_RAM_PEAK=31.282_GiB
TRANSACTION_ATTEMPTS=6608
TRANSACTION_RETRIES=4
HTTP_5XX=1
HTTP_429=0_EXPLICIT_API_OBSERVATIONS_NOT_FULL_FIREBASE_COVERAGE
WS_CONNECT_FAILURES=NOT_SEPARATELY_MEASURED_323_GENERIC_CLIENT_FAILURES
WS_ABNORMAL_CLOSES=AT_LEAST_1_CODE_1008
RECONNECTS=1
SEQUENCE_REGRESSIONS=0
DUPLICATE_COMMAND_APPLICATIONS=0
INVALID_TURN_ACCEPTANCES=0
UNAUTHORIZED_DELIVERIES=0
MATCH_CORRUPTIONS=0
ACK_P95_200_FIRST_MINUTE=4457
ACK_P95_200_LAST_MINUTE=2608
CPU_200_FIRST_MINUTE=49.193
CPU_200_LAST_MINUTE=46.797
MEMORY_200_FIRST_MINUTE=936.63_MiB_PEAK
MEMORY_200_LAST_MINUTE=977.00_MiB_PEAK
SATURATION_SHAPE=OTHER_ADMISSION_DEGRADATION_AND_CLIENT_LOSS
SLO_VALIDATED_BAND=20_OBSERVED_ONLY_NOT_CERTIFIED
DEGRADED_BUT_USABLE_BAND=40_TO_80_CANDIDATE_NO_MANUAL_UX_VALIDATION
SEVERELY_DEGRADED_BAND=100_TO_200_REQUESTED_ACTUAL_PEAK_163
TECHNICAL_SATURATION_POINT=NOT_IDENTIFIED_CPU_RAM_REDIS_BANDWIDTH_NOT_EXHAUSTED
UNSTABLE_POINT=ADMISSION_DEGRADES_FROM_120_REQUESTED_CLIENT_LOSS_180_TO_200_NOT_PROVEN_BACKEND_COLLAPSE
EMERGENCY_STOP_TRIGGERED=NO
EMERGENCY_STOP_REASON=NONE
POST_RECOVERY_ACTIVE_LOAD_MATCHES=0
POST_RECOVERY_PENDING_WORK=0
UNCLASSIFIED_LOAD_MATCHES=0
ORPHAN_SWARM_PROCESSES=0
REPLAY_SAMPLE_SIZE=3
REPLAY_SAMPLE_RESULT=PASS_1813_EVENTS_EXISTING_REDUCER
SERVER6_REGRESSION=PASS
SOURCE_CHANGE=NO_PRODUCT_SOURCE_OR_DURING_RUN_CHANGES
SERVER_CONFIGURATION_CHANGED=NO
COMMIT=NONE
PUSH=NONE
EXPLORATORY_EXECUTION_FINISHED=YES
ACTUAL_200_USER_HOLD_300S=NOT_ACHIEVED
S7_19_COMPLETE=NO_200_CONCURRENT_USERS_AND_50_MATCH_TARGET_NOT_REACHED
NEXT=S7-19 INTERPRETATION
```

Evidencia detallada: [analysis.json](Generated/S719/analysis.json), [registro autoritativo](Generated/S719/registry-confirm.json), [auditoría](Generated/S719/outcomes.txt), [Replay](Generated/S719/replay-result.json), [estado final](Generated/S719/server-final.json).
