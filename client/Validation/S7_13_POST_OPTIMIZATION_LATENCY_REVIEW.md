# SERVER-7 / S7-13 — Revisión de latencia posterior a optimización

**S7_13_SUCCESS=YES.** Revisión de evidencia local y código; cero carga nueva y cero consultas o cambios al servidor. SHA revisado: `5a74d7917fc7fb0baa04bbdf2c8fc45de5a13f14`.

La mayor parte del deterioro 40→60 está dentro del servidor y, dentro de este, repartida por la ruta Firestore. READ_WAIT sigue siendo relevante, pero no explica por sí sola la mayoría. No hay evidencia de saturación global de CPU ni de que GC/reintentos sean la causa principal. No se ha identificado si la espera restante nace en red, servicio remoto, locks sin retry, SDK o planificación local.

## Población y ventanas

Se reutilizan 2.004 ACK correlacionados con 2.004 de 2.005 muestras de servidor de R3. La muestra de servidor sin ACK durante STOP queda preservada; las estadísticas pareadas siguientes la excluyen. Las 2.005 transacciones tuvieron un intento y cero retries; el agregado no descarta esperas de locks ni retries internos de RPC no instrumentados. Los contadores conservan tres rondas lógicas, cuatro documentos.

Los gates originales son 839/920/1262 ms p95. Las ventanas completas de instrumentación de S7-12 incluyen además la espera de descubrimiento previa al siguiente grupo: 373/737/894 comandos y p95 cliente 856/929/1258 ms. No se sustituyen los gates por estos valores. Se mantiene esa delimitación para comparar fases; diferencias de población y mezcla de comandos impiden una descomposición causal exacta del delta de p95. Ningún percentil se suma a otro como si correspondieran al mismo comando. Los porcentajes aditivos usan sumas/medias de duraciones de las mismas muestras.

## Fases por etapa

| Usuarios | Métrica | n | p50 ms | p95 ms | p99 ms | max ms |
|---:|---|---:|---:|---:|---:|---:|
| 20 | CLIENT_ACK ventana completa | 373 | 397 | 856 | 1259 | 1556 |
| 20 | SERVER_TOTAL | 373 | 352 | 740 | 1097 | 1470 |
| 20 | FS_TOTAL | 373 | 340 | 680 | 1054 | 1412 |
| 20 | FS_TRANSACTION_TOTAL | 373 | 277 | 574 | 879 | 1248 |
| 20 | FS_TRANSACTION_READ | 373 | 119 | 271 | 382 | 793 |
| 20 | FS_DOMAIN_INSIDE_TRANSACTION | 373 | 0 | 2 | 18 | 34 |
| 20 | FS_COMMIT_OR_REMOTE_COMPLETION | 373 | 87 | 166 | 213 | 397 |
| 20 | FS_POST_COMMIT | 373 | 0 | 0 | 0 | 0 |
| 20 | PRIOR_RUNTIME | 373 | 60 | 122 | 175 | 332 |
| 20 | ACQUISITION_TO_CALLBACK | 373 | 57 | 139 | 282 | 342 |
| 20 | CALLBACK_LOCAL_OTHER | 373 | 1 | 18 | 38 | 51 |
| 20 | TX_OTHER calculado por comando | 373 | 59 | 165 | 290 | 380 |
| 20 | OUTSIDE_SERVER residual pareado | 373 | 39 | 123 | 172 | 396 |
| 40 | CLIENT_ACK ventana completa | 737 | 476 | 929 | 1190 | 1434 |
| 40 | SERVER_TOTAL | 737 | 412 | 800 | 1055 | 1346 |
| 40 | FS_TOTAL | 737 | 393 | 743 | 967 | 1341 |
| 40 | FS_TRANSACTION_TOTAL | 737 | 318 | 616 | 818 | 1231 |
| 40 | FS_TRANSACTION_READ | 737 | 138 | 300 | 363 | 511 |
| 40 | FS_DOMAIN_INSIDE_TRANSACTION | 737 | 0 | 4 | 15 | 38 |
| 40 | FS_COMMIT_OR_REMOTE_COMPLETION | 737 | 96 | 187 | 268 | 977 |
| 40 | FS_POST_COMMIT | 737 | 0 | 0 | 0 | 0 |
| 40 | PRIOR_RUNTIME | 737 | 67 | 151 | 197 | 291 |
| 40 | ACQUISITION_TO_CALLBACK | 737 | 66 | 150 | 206 | 251 |
| 40 | CALLBACK_LOCAL_OTHER | 737 | 1 | 21 | 42 | 193 |
| 40 | TX_OTHER calculado por comando | 737 | 70 | 167 | 224 | 273 |
| 40 | OUTSIDE_SERVER residual pareado | 737 | 52 | 148 | 192 | 236 |
| 60 | CLIENT_ACK ventana completa | 894 | 708 | 1258 | 1482 | 3271 |
| 60 | SERVER_TOTAL | 894 | 617 | 1106 | 1315 | 1969 |
| 60 | FS_TOTAL | 894 | 572 | 1012 | 1213 | 1871 |
| 60 | FS_TRANSACTION_TOTAL | 894 | 460 | 849 | 1007 | 1721 |
| 60 | FS_TRANSACTION_READ | 894 | 212 | 392 | 543 | 647 |
| 60 | FS_DOMAIN_INSIDE_TRANSACTION | 894 | 0 | 10 | 26 | 122 |
| 60 | FS_COMMIT_OR_REMOTE_COMPLETION | 894 | 129 | 260 | 350 | 1164 |
| 60 | FS_POST_COMMIT | 894 | 0 | 0 | 0 | 0 |
| 60 | PRIOR_RUNTIME | 894 | 100 | 213 | 268 | 359 |
| 60 | ACQUISITION_TO_CALLBACK | 894 | 102 | 221 | 281 | 358 |
| 60 | CALLBACK_LOCAL_OTHER | 894 | 2 | 34 | 71 | 578 |
| 60 | TX_OTHER calculado por comando | 894 | 112 | 245 | 310 | 743 |
| 60 | OUTSIDE_SERVER residual pareado | 894 | 82 | 187 | 262 | 1670 |

FS_TOTAL incluye pre-read + transacción; es distinto de la etiqueta externa `firestore` del trazador de fases. Completion mide desde el final del callback hasta la resolución del futuro: incluye commit remoto y trabajo/espera del SDK, no aísla el commit puro. POST_COMMIT Firestore=0 corresponde a la ruta de código instrumentada sin operaciones Firestore posteriores; la publicación local sí tiene coste.

## Qué crece de 40 a 60

READ_WAIT p95 aumenta 300→392 ms (+92 ms, +30,67 %); completion 187→260 ms (+73 ms, +39,04 %); adquisición 150→221 ms; pre-read 151→213 ms. Estas diferencias describen distribuciones, no se suman para reconstruir el p95 de servidor.

La media cliente crece 194.62 ms; servidor 165.98 ms y residual 28.64 ms. El 85.28 % del aumento medio está dentro del servidor. Firestore crece 148.63 ms: 89.55 % del incremento medio de servidor.

Dentro de ese incremento medio de servidor, lectura transaccional aporta 56,10 ms (33,80 %), pre-read 26,73 ms (16,10 %), adquisición 30,56 ms (18,41 %), completion 30,01 ms (18,08 %), dominio 0,92 ms y otro callback 4,31 ms. Las lecturas combinadas aportan 82,83 ms (49,90 %): tampoco justifican atribuirles por sí solas la mayoría del deterioro. Cambios de mezcla y tiempo limitan causalidad.

A 60, READ_WAIT suma 34.58 % del tiempo de servidor; FS_TOTAL 93.08 %. La razón p95 READ_WAIT/p95 servidor es 35,44 %, pero no es una participación aditiva del p95. El restante transaccional se calcula por comando como TX − READ_WAIT − completion: media 122,57 ms y p95 245 ms. Su partición exacta es adquisición + dominio + callback local + gaps entre intentos (estos últimos cero). Adquisición domina ese resto: media 111,16 ms. No se suman 221+10+34 como supuesto p95 de ese resto.

## CPU, memoria y GC

| Usuarios | CPU media % | Pico % | Máx. media 30 s % | Heap pico MiB | GC count | GC ms |
|---:|---:|---:|---:|---:|---:|---:|
| 20 | 10.247 | 37.872 | 16.818 | 391.61 | 3 | 34 |
| 40 | 16.671 | 46.265 | 24.154 | 391.54 | 4 | 163 |
| 60 | 28.573 | 50.946 | 36.444 | 390.60 | 5 | 231 |

Cuota autoritativa de cuatro CPU. A 60 quedan 71,43 puntos porcentuales de margen medio y 49,05 incluso en el pico observado. Máxima media móvil de 30 s=36,44 %; máximo mínimo de las muestras de una ventana de 30 s=17,52 %. Son dos definiciones distintas de sostenido; la resolución de muestreo no prueba ausencia de picos subintervalo. No hubo tramo medido ≥95 % durante 30 s.

GC a 60: 231 ms acumulados en unos 154 s de etapa (~0,15 % del tiempo de pared), cinco colecciones y heap pico ~391 MiB. No explica la degradación sostenida como causa primaria; no se aislaron pausas por comando. Los contadores GC agregados no equivalen necesariamente a tiempo total de stop-the-world. CPU y latencia crecen conjuntamente, pero no hay saturación global: no se puede excluir un hilo/cola local específico o espera de SDK. Un A/B 4-vs-6 CPU no es el siguiente experimento justificado por estas métricas.

## Comandos, partidas y tiempo

| Comando a 60 | n | ACK p95 ms |
|---|---:|---:|
| NEXT_ROUND | 22 | 1333 |
| PASS | 198 | 1255 |
| PLAY_TILE | 674 | 1250 |

NEXT_ROUND tiene el p95 más alto (1333 ms), pero solo 22 muestras: estimación de cola menos estable. PLAY_TILE (674) y PASS (198) también superan 1000 ms. La degradación afecta a las clases principales; no se concentra únicamente en NEXT_ROUND.

No hay matchId ni alias en las correlaciones de ACK; solo hash de commandId, tipo, tiempo y grupo. Los eventos conservados para la auditoría final no incluyen el puente commandId→match necesario para esta revisión local. No se solicita nueva lectura remota para suplirlo. Los tres grupos tienen p95 1251/1248/1258 ms (303/309/282 muestras), compatible con distribución amplia entre grupos, pero NO demuestra cuántas de las quince partidas son lentas. MATCHES_WITH_ACK_P95_GT_1000MS y skew por partida quedan NO IDENTIFICABLES.

| Ventana a 60 | n | ACK p95 ms | GC ms |
|---|---:|---:|---:|
| first30Ready | 197 | 1202 | 86 |
| middleReady | 388 | 1296 | 63 |
| last30 | 183 | 1225 | 48 |
| first30Ramp | 148 | 1326 | 34 |

La comparación principal empieza cuando los 60 sockets y quince partidas están listos: primeros 30 s=1202 ms, intervalo medio=1296 ms, últimos 30 s=1225 ms. Ya está elevado al inicio y no crece monótonamente; compatible con un escalón inmediato, no con acumulación progresiva demostrada. Los primeros 30 s de rampa (1326 ms) incluyen incorporación de usuarios y se informan aparte. Una sola realización secuencial no aísla un efecto causal puro de capacidad.

## Región y límite de tres rondas

Se reutiliza únicamente la metadata autoritativa S7-08: Firestore TEST en **us-central1**. Backend local en VM; no se deduce ubicación física de la zona horaria ni se investiga/cambia región. Es compatible con amplificación de esperas remotas seriales; el pre-read, dos lecturas transaccionales, adquisición y completion tienen coste. No son solo tres RPC totales. La instrumentación agrega las dos lecturas transaccionales, no las separa, y no distingue RTT puro, servicio, bloqueo y SDK.

Tres rondas contribuyen a un piso práctico de espera: las lecturas combinadas por comando tienen media 330,49 ms y p95 572 ms a 60. Pero no se ha probado un piso físico fijo ni que reducir una ronda garantice p95≤1000. La comparación S7-11 respaldó la mejora 4→3; no autoriza extrapolación lineal a otra reducción.

## Auditoría de las rondas restantes

En OnlineMatchService.command, la lectura previa fija before.match.lastSequence antes de repository.transact y valida pertenencia. OnlineRepository mantiene el mismo expectedSequence entre retries; primero lee recibo, luego getAll(runtime, work), comprueba revisión y solo después ejecuta/persiste. OnlineCommand no trae revisión autoritativa alternativa. Se inspeccionaron las pruebas de revisión previa, retry con otra instancia y duplicados concurrentes; no se ejecutaron nuevas pruebas.

**PRIOR_RUNTIME_ROUND_REMOVABLE=NEEDS_DESIGN.** La serialización de Firestore no sustituye la revisión congelada antes de la transacción: un comando podría evaluarse contra otra revisión cuando por fin inicia o reintenta. Congelar la primera lectura dentro del callback cambia la frontera de carrera anterior a ese callback. El recibo cubre identidad/contenido de una repetición, no la revisión esperada de un comando nuevo. El último estado en memoria/Redis o una secuencia arbitraria enviada por el cliente tampoco constituyen el mecanismo autoritativo existente requerido. Hoy no se encontró sustituto equivalente ya disponible.

**RECEIPT_ROUND_REMOVABLE=NO bajo el contrato actual.** Agrupar recibo con runtime/work podría conservar deduplicación lógica tras leerlos, pero perdería el atajo que evita esas lecturas, sus fallos y su conjunto de locks en duplicados. Sacar el recibo de la transacción o usar caché debilita la carrera entre instancias. Mantener ese atajo exige resolver primero la lectura del recibo. No se recomienda otro batch como optimización pequeña.

**FURTHER_SAFE_READ_ROUND_REDUCTION=NEEDS_DESIGN.** ACK permanece después de persistencia autoritativa. No se propone adelantarlo ni introducir autoridad de proceso.

## Recuperación y capacidad

Las 14 cancelaciones ocurrieron entre 206.65 y 241.15 segundos después de STOP; cero antes. El primer inventario mostraba 1 completada/14 activas y los dos finales 1 completada/14 canceladas/0 activas. Es compatible con grace de 180 s más programación del worker. Auditoría de quince partidas sin errores y convergencia ya comprobada en S7-12, sin limpieza ni reinicios: cancelaciones esperadas de recuperación, no fallos de capacidad por sí mismas.

Conclusión limitada: 40 usuarios concurrentes pasaron el gate definido; 60 lo incumplieron. No se declara MAX_CAPACITY=40 y no se han ensayado niveles intermedios.

## Un único siguiente paso propuesto

**Opción B: revisión de diseño de una precondición autoritativa de revisión que pudiera reemplazar PRIOR_RUNTIME.** Paso mínimo: especificar, sin implementación ni carga, de dónde vendría la revisión ligada al comando y contrastar su contrato con contraejemplos de dos instancias: avance antes del primer callback, retry tras avance, duplicado y conexión/abandono concurrentes. El resultado discriminante será equivalencia demostrada o rechazo del candidato. Una revisión emitida/verificable por el servidor sería un mecanismo nuevo que requeriría diseño, no algo ya disponible. No se propone simultáneamente prueba de 50 usuarios, cambio de CPU ni traslado de región.

Se elige diseño de transacción, no otra agrupación trivial: todavía hay coste relevante de pre-read, pero la protección de revisión impide quitarla sin contrato. Confianza alta en la atribución temporal a la ruta Firestore, moderada en la causa subyacente y no establecida para geografía o skew por partida.

## Evidencia y salida

[Análisis reproducible](Generated/S713/analysis.json), [script local](Generated/S713/analyze.py), [hashes preservados](Generated/S713/evidence-hashes.json). Fuentes: S712/phases.jsonl, group-*.log, server-resources.jsonl, stage-details.json, outcomes.txt, registros inicial/final; informes S7-08/S7-09/S7-11. Código: OnlineMatchService.kt:71, OnlineRepository.kt:129, OnlineModels.kt:16, FirestorePhaseTiming.kt:58, GroupedCommandReadsEmulatorTests.kt:149. Ninguna llamada de carga, servidor, Firestore, Redis o investigación de región fue ejecutada por S7-13.

```text
SERVER-7 S7-13 POST-OPTIMIZATION LATENCY REVIEW
==============================================
ACK_P95_20=839_MS
ACK_P95_40=920_MS
ACK_P95_60=1262_MS
SERVER_P95_20=740_MS
SERVER_P95_40=800_MS
SERVER_P95_60=1106_MS
READ_WAIT_P95_20=271_MS
READ_WAIT_P95_40=300_MS
READ_WAIT_P95_60=392_MS
COMMIT_P95_20=166_MS
COMMIT_P95_40=187_MS
COMMIT_P95_60=260_MS
READ_WAIT_GROWTH_40_TO_60=92_MS_P95_30.67_PERCENT; MEAN_PLUS_56.10_MS
READ_WAIT_SHARE_OF_SERVER_60=34.58_PERCENT_SUMMED_DURATIONS
OTHER_FIRESTORE_COMPONENT_60=ACQUISITION_PLUS_DOMAIN_PLUS_CALLBACK_OTHER; P95=245_MS; MEAN=122.57_MS
OUTSIDE_SERVER_RESIDUAL_20=123_MS_P95_PAIRED
CPU_QUOTA_AVG_20=10.247_PERCENT
CPU_QUOTA_PEAK_20=37.872_PERCENT
OUTSIDE_SERVER_RESIDUAL_40=148_MS_P95_PAIRED
CPU_QUOTA_AVG_40=16.671_PERCENT
CPU_QUOTA_PEAK_40=46.265_PERCENT
OUTSIDE_SERVER_RESIDUAL_60=187_MS_P95_PAIRED
CPU_QUOTA_AVG_60=28.573_PERCENT
CPU_QUOTA_PEAK_60=50.946_PERCENT
MAX_SUSTAINED_CPU_30S_60=36.444_PERCENT_MAX_WINDOW_AVERAGE; 17.521_PERCENT_MAX_WINDOW_MINIMUM
CPU_HEADROOM_AT_60=71.427_PERCENT_AVERAGE; 49.054_PERCENT_AT_OBSERVED_PEAK
FIRESTORE_CPU_RELATION_60=FIRESTORE_WAIT_GROWS_WITH_SUBSTANTIAL_AGGREGATE_CPU_HEADROOM
GC_LATENCY_EXPLANATION=NO_AS_PRIMARY_CAUSE; INDIVIDUAL_PAUSE_EFFECT_NOT_ISOLATED
TRANSACTION_RETRIES=0
SLOWEST_COMMAND_TYPE=NEXT_ROUND_N22
SLOWEST_COMMAND_P95=1333_MS
MATCHES_WITH_ACK_P95_GT_1000MS=NOT_IDENTIFIABLE_FROM_EXISTING_LOGS
MATCH_LATENCY_SKEW=UNKNOWN_PER_MATCH; ALL_3_GROUPS_HAVE_P95_GT_1000MS
ACK_60_FIRST_30S_P95=1202_MS_N197_AFTER_60_READY
ACK_60_LAST_30S_P95=1225_MS_N183
DEGRADATION_PATTERN=IMMEDIATE_STEP_COMPATIBLE; NO_MONOTONIC_PROGRESSIVE_GROWTH
FIRESTORE_LOCATION_RELEVANCE=VERIFIED_US_CENTRAL1; REMOTE_ROUND_AMPLIFICATION_COMPATIBLE_NOT_CAUSALLY_PROVEN
FURTHER_SAFE_READ_ROUND_REDUCTION=NEEDS_DESIGN
PRIOR_RUNTIME_ROUND_REMOVABLE=NEEDS_DESIGN
RECEIPT_ROUND_REMOVABLE=NO_UNDER_CURRENT_DUPLICATE_SHORT_CIRCUIT_CONTRACT
THREE_ROUND_LATENCY_FLOOR_EVIDENCE=SERIAL_WAIT_CONTRIBUTES; NO_PROVEN_FIXED_FLOOR_OR_PURE_NETWORK_RTT
CPU_AB_TEST_JUSTIFIED=NO
PRIMARY_POST_OPTIMIZATION_BOTTLENECK=DISTRIBUTED_FIRESTORE_CLIENT_TRANSACTION_WAIT; NOT_READ_WAIT_ALONE
CONFIDENCE=HIGH_MEASURED_PHASE_ATTRIBUTION; MODERATE_CAUSAL_INTERPRETATION
CANCELLED_MATCHES_EXPECTED_RECOVERY=YES
CURRENT_VALIDATED_STAGE=40_USERS
60_USER_STAGE_GATE=FAIL
PROPOSED_NEXT_EXPERIMENT=B: OFFLINE_PRIOR_RUNTIME_REVISION_PRECONDITION_DESIGN_REVIEW_WITH_EXPLICIT_TWO_INSTANCE_RACE_COUNTEREXAMPLES
NEW_LOAD_EXECUTED=NO
CPU_CHANGED=NO
RAM_CHANGED=NO
JVM_CHANGED=NO
FIRESTORE_CHANGED=NO
REDIS_CHANGED=NO
SERVER7_100_RERUN_STARTED=NO
SERVER7_250_STARTED=NO
SERVER_CONFIGURATION_CHANGED=NO
COMMIT=NONE
PUSH=NONE
S7_13_SUCCESS=YES
NEXT=S7-13 REVIEW
```
