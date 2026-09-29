# S7-06 — diagnóstico de latencia ACK de R2

La causa dominante sigue **INCONCLUSIVE**. La persistencia Firestore es síncrona y obligatoria antes del ACK; R2 no capturó duraciones por fase, ocupación de ejecutores ni esperas de locks. Hay asociación temporal entre CPU y latencia, pero no saturación sostenida de la cuota ni evidencia suficiente para atribuir el tiempo a CPU, Firestore, red o cliente.

No se ejecutó carga ni se contactó/modificó el servidor en este diagnóstico. Se analizaron los artefactos existentes y el código del SHA `a4a2542363d93301bbede958b18bbce9fb3e8d1f`. R2 permanece inmutable; `Generated/S706/r2-sha256.json` registra y verifica sus archivos. Los archivos de análisis son nuevos y separados.

## Corrección de interpretación y método

**1.247 ms es el p95 acumulado de R2, no el p95 aislado de 60 usuarios.** `RunR2.py` combina histogramas acumulados al evaluar el gate. Se conserva ese resultado histórico y no se reescribe el benchmark. La etapa de 60 falló el gate predefinido; esto no demuestra una capacidad máxima de 60 o 59 usuarios.

Para separar etapas se restan buckets acumulados entre snapshots de `timeline.jsonl`, sumando los procesos presentes. Ventanas: 0–164,593 s; 164,593–331,734 s; 331,734–480,578 s desde el inicio. Incluyen la rampa de incorporación al objetivo y el tiempo de revisión posterior. Son intervalos de recepción/registro del ACK, no cohortes de comandos enviados. Las publicaciones de cada proceso cada 2 s y el sondeo del coordinador añaden incertidumbre de borde; no existen timestamps individuales para resolverla.

La última línea del timeline precede al STOP: contiene 1.846 ACK; el resumen final contiene 1.901. Los 55 restantes corresponden a la cola temporal no cubierta por ese último snapshot y **no se atribuyen artificialmente a ninguna etapa**. La etapa de 20 da aquí p95=1.008 ms porque incluye tiempo posterior a la evaluación inicial del gate mientras se hacía discovery; no significa que aquel gate hubiese observado ese mismo histograma. No se restan percentiles: se restan frecuencias enteras y se recalculan rangos más cercanos, sin overflow.

## Distribuciones separadas

| Usuarios objetivo | ACK n | p50 | p90 | p95 | p99 | máximo | >1.000 ms |
|---|---:|---:|---:|---:|---:|---:|---:|
| 20 | 330 | 509 | 750 | 1008 | 1266 | 1665 | 17 (5.2%) |
| 40 | 697 | 522 | 847 | 950 | 1316 | 1801 | 21 (3.0%) |
| 60 | 819 | 705 | 1239 | 1458 | 1825 | 2247 | 218 (26.6%) |

Todas las latencias están en ms. A 60 usuarios aumenta la mediana y un 26,6% supera 1 s: hay desplazamiento de distribución y cola, no solo unos pocos extremos. El histograma completo está en `Generated/S706/analysis.json`.

| Objetivo | WS n; p50/p95/p99/máx | Matchmaking n; p50/p95/p99/máx | REST n; p50/p95/p99/máx |
|---|---|---|---|
| 20 | 1320; 0/0/0/1 | 20; 3650/4774/5292/5292 | 120; 464/10902/14698/15653 |
| 40 | 2788; 0/0/0/1 | 20; 3675/5236/5699/5699 | 120; 477/9968/14623/15340 |
| 60 | 3276; 0/0/0/3 | 20; 3726/5858/6103/6103 | 124; 558/17327/20608/21067 |

REST tampoco permanece estable: p95 sube de ~10 s a 17,3 s. Es una mezcla de endpoints de preparación/autenticación/catálogo/cola y cuatro lecturas finales de History; no equivale a una sonda homogénea de salud ni demuestra lentitud general por sí sola. Matchmaking tiene solo 20 observaciones por incorporación y aumenta moderadamente. Los tiempos de WS son enteros truncados a ms: 0 no significa trabajo nulo.

Primeros 30 s desde STAGE_READY=60: n=173, p95=1.200 ms. Últimos ~30 s del timeline: n=164, p95=1.309 ms. Ventanas fijas desde inicio muestran p95 de 1.748 durante rampa 330–360 s, después 1.411, 1.123, 1.462 y 1.570 ms. No hay empeoramiento monotónico: aparecen ráfagas desde la incorporación. Las ventanas de STAGE_READY y las fijas tienen bordes diferentes y no son intercambiables.

## Recursos y correlación

| Objetivo | API CPU cuota media/p95/pico % | cores media | Host CPU media/pico % | load1 media/pico | heap fin/pico MiB | GC count/ms | Redis cores media/pico |
|---|---|---:|---|---|---|---|---|
| 20 | 15.47/52.52/74.44 | 0.619 | 18.09/73.38 | 3.76/4.87 | 318.9/346.9 | 5/136 | 0.0456/0.2223 |
| 40 | 15.44/41.13/46.93 | 0.618 | 20.07/64.89 | 4.73/6.44 | 154.4/366.0 | 9/219 | 0.0549/0.2682 |
| 60 | 28.49/53.65/63.09 | 1.140 | 40.53/78.21 | 7.38/9.65 | 116.8/378.5 | 7/307 | 0.0906/0.3974 |

241 muestras de servidor aproximadamente cada 2 s. Correlación descriptiva de p95 ACK con CPU media en 16 ventanas no solapadas de 30 s: Pearson=0,834. Incluye rampas, tamaños muestrales desiguales, demora de snapshots y factores comunes de carga; no es causal ni una medida de tiempo de servicio. Host CPU/load también crecen. El host medido es la VM Linux, no la CPU del generador Windows ni la del hipervisor.

Máximo sostenido 30 s, definido como máximo del mínimo de muestras en una ventana completa: 14,862% de cuota. Mayor media móvil 30 s: 38,912%. Pico puntual 74,444%. Nunca se observó 95% durante 30 s; CPU_STOP_TRIGGERED=NO. Headroom agregado no descarta un hilo ocupado, esperas o throttling breve.

Heap muestra ciclos de recuperación, no crecimiento retenido monotónico; máximo ~378,5 MiB frente a Xmx=2 GiB. API cgroup pasó de 777.064.320 bytes de base a pico 913.350.656: incluye memoria no heap y otros cargos, no prueba fuga. GC=21 eventos/662 ms acumulados. Pearson por ventanas con tiempo GC=0,117: no evidencia fuerte de asociación, pero la granularidad no permite excluir pausas en comandos concretos ni sumar estos contadores como si fueran trazas de pausa individuales.

Redis: memoria pico por etapa 1.809.464 / 1.817.504 / 1.823.656 bytes; sin evictions/rejections/OOM. Snapshots de clientes en 20 y 60: 5 conexiones, 0 bloqueadas, 2 pubsub; no se midió pico continuo. No hay volumen de comandos ni histograma de latencia Redis en R2. Bajo CPU/memoria no descarta esperas de red/cliente Redis: clasificación INCONCLUSIVE, sin evidencia positiva de saturación.

## Frontera exacta del ACK

`SimulatedClient.kt`: START=`firstPendingAt=System.nanoTime()/1_000_000` inmediatamente antes de `wire.send(MATCH_COMMAND,...)`; END=`now()` dentro del manejo de `COMMAND_ACCEPTED` cuyo commandId coincide con pending. Solo aceptados aportan muestras. Un retry conservaría el inicio original; el timeout de retry es 15 s, superior al máximo observado 2,247 s. El tiempo de pensar/planificar anterior al envío no entra.

Incluye serialización y envío del cliente, esperas posteriores de scheduling, red de ida, Cloudflare/túnel, dispatch de ingreso, validación de sesión/protocolo y pertenencia, lectura/validación de estado, transacción/commit Firestore, preparación de broadcast, cola/salida ACK, red de vuelta, ensamblaje y parseo de frame y scheduling/procesamiento anterior al registro en el cliente. Firebase token verification ocurre en AUTH inicial, no por cada MATCH_COMMAND. Incluye autorización usando uid autenticado y asiento. No incluye directamente consultas Redis en el camino normal aceptado.

`CLIENT_FRAME_TO_HANDLER` solo se registra para MATCH_UPDATE: empieza después de completar y parsear JSON en el listener Java HTTP, y termina después de ejecutar `message(next)`. Incluye cola del cliente y handler, no transporte previo, no parseo inicial y no mide el handler de ACK. Sus p95/p99 bajos acotan esa porción para updates; no descartan retrasos anteriores del cliente/red.

## Camino síncrono y almacenamiento

1. `RealtimeHandler.handleTextMessage`: monitor por conexión, límites locales, parseo, secuencia/protocolo, uid existente, decode OnlineCommand. Se libera el monitor antes de llamar al dominio.
2. `OnlineMatchService.command`: validación de campos; `repository.read` hace GET runtime Firestore y espera; verifica seat autorizado.
3. `FirestoreOnlineRepository.transact`: espera transacción completa. En intento nuevo: lee receipt, runtime y turn-work secuencialmente; comprueba expectedSequence; ejecuta engine y valida writes. Esas tres lecturas más la prelectura son cuatro lecturas lógicas, no cuatro RPC garantizadas.
4. Commit: root match, runtime, work y receipt, más eventos y participantes/rounds/history que cambien. En terminal se elimina work. Retries configurados hasta 8 intentos: pueden repetir lecturas/cómputo. `transactionRead` usa Future.get y preserva causa ABORTED. Duplicado con receipt existente hace salida temprana; no corresponde al mismo coste.
5. Tras commit, `publish` invoca callback síncrono: `socialPresence.observeMatch` actualiza mapas locales; recorre conexiones, prepara vistas autorizadas y encola MATCH_UPDATE para participantes bajo sus monitores. `offerCritical` serializa JSON antes de encolar.
6. Después encola COMMAND_ACCEPTED bajo monitor del emisor. Writer virtual por conexión hace I/O después; update y ACK comparten cola crítica FIFO. Encolar ACK no equivale a emitirlo por red ni a recibirlo.

**Redis directo por comando normal aceptado: 0 operaciones, 0 round trips, sin pipeline ni lock distribuido en este recorrido.** Heartbeat/presencia, mantenimiento social, índice de turnos y matchmaking sí utilizan Redis fuera de él; pueden competir indirectamente. Caminos excepcionales de cierre/cleanup no son el caso normal.

## Serialización, workers y pools

No se encontró mutex global de gameplay ni lock Redis por comando. Firestore y la comprobación de secuencia serializan cambios al mismo estado/receipt; workers y jugador pueden entrar en conflicto sobre runtime/work. Hay monitores por conexión, locks breves de cola y un monitor global de admisión de conexiones. Broadcast puede esperar un monitor que también usa PING/presencia; no se midió esa espera. Se recorren todas las conexiones por publicación, aunque se envía solo a participantes.

OnlineTurnWorker usa scheduler dedicado de **1 hilo**, fixedDelay por defecto 5 s. Procesa matches reclamados secuencialmente: carga estado, por participante comprueba conexión (lecturas de estado y presencia), evalúa abandono/timeout y refresca discovery con transacción. Comparte Firestore, CPU y registros con foreground, pero no su executor de despacho. No existen contadores de ocupación/retries en R2 para probar contención.

Configuración explícita revisada: realtime-lifecycle 1 hilo, matchmaking 1, online-turn 1; social-presence 2/cola128; social-authorization 2/cola256; social delivery/subscription 1 cada uno; social-redis máximo2/SynchronousQueue; timers outbound 2; writer virtual por conexión. JVM processors observado=4. Más hilos que CPU no implica saturación cuando esperan I/O.

`application.yaml` no configura tamaño Tomcat, virtual threads Spring ni pools Firestore/Lettuce; Redis timeout/connect-timeout=1 s. No hay captura R2 del tamaño efectivo/ocupación de Tomcat/WebSocket, pool general Spring, Firestore/gRPC, Lettuce/Netty o carrier/ForkJoin. No se inventan valores por defecto como si fueran configuración efectiva. **Esa parte de la inspección queda NOT_MEASURED** y debe incorporarse a la próxima captura; un thread dump actual no reconstruiría el estado durante R2. No se ajustó ningún pool.

## Cliente y red

Tres procesos Swarm de 20 clientes en Windows. `runBlocking/supervisorScope/launch` sin dispatcher explícito comparte un event loop por proceso; HTTP/WS usan I/O asíncrono. Trabajo local, parseo y handlers pueden introducir scheduling. No se recopilaron CPU, GC o lag de cada proceso/host Windows: SWARM_CPU_PRESSURE, SWARM_GC_PRESSURE y presión global del event loop=NOT_MEASURED. Los updates rápidos son evidencia parcial, no exclusión del cliente. No hay histogramas ACK por tipo ni match: SLOWEST_COMMAND_TYPE y MATCHES_WITH_ACK_P95_OVER_1000MS=NOT_MEASURED. No se infiere distribución entre 15 matches a partir de agregados.

No existen marcas correlacionadas de ingreso/egreso servidor ni RTT específico que separen antes/dentro/después. Cloudflare/network=INCONCLUSIVE; no hay evidencia para culparlos.

## Correctness y recuperación

Se preservan 15 resultados autoritativos: 1 COMPLETED y 14 CANCELLED, cero ACTIVE/FAILED/UNKNOWN, auditoría de secuencias/turnos sin fallos y SERVER-6 intacto. Los 14 seguían ACTIVE en lectura inicial tras STOP y terminaron entre **189,820 y 235,259 s después del marcador stoppedUtc**, que se escribe tras detener los grupos. Es consistente con desconexión, abandono normal de 180 s y trabajo periódico; no se interpretan como corrupción. R2 conserva gate cerrado, ausencia de nueva reconciliación, cero pending/huérfanos y convergencia Redis. No se dispone aquí de timestamp por cada socket para calcular 180 s exactos por asiento.

## Propuesta, sin implementación

Primer paso: una futura comparación autorizada 40→60 con las mismas condiciones y timing por fase, sin aumentar recursos. No ejecutar todavía 100 ni 250. Antes de carga, validar el overhead de instrumentación con tráfico mínimo autorizado. Registrar solo IDs de correlación opacos y command type; nunca tokens/credenciales/payloads privados.

Marcas monotónicas: C0 antes de send; S0 ingreso; S1 monitor/auth/protocolo terminados; S2 prelectura completada; por intento A0/inicio y lecturas receipt/runtime/work; A1 engine terminado; S3 commit final; S4 publicación/encode final; S5 ACK encolado; S6 selección writer; S7 send completado; C1 frame recibido antes de parsear; C2 handler ACK. Medir duración intra-proceso; no restar relojes monotónicos de máquinas distintas. Conteos de retries y número de docs escritos, tiempos de lock/cola y worker activos permiten discriminar contención. Registrar pools efectivos, CPU/GC y lag del cliente Windows cada 2 s. Comparar histogramas incrementales por etapa, tipo y match sin sustituir los resultados R2.

Opciones ordenadas por relevancia estructural, condicionadas a timings: (1) reducir lecturas/preparación redundantes y frecuencia de trabajo de discovery conservando autoridad y secuencia; (2) reducir conflictos entre worker y foreground sobre runtime/work; (3) reducir recorrido/encode y espera de publicación si S3–S5 domina; (4) separar/ajustar executor solo con evidencia de cola; (5) repartir generadores si C1–C2/CPU Windows domina; (6) CPU A/B solo si se prueba coste CPU; (7) Redis round-trip reduction tiene baja prioridad en ACK, al ser cero directo. Desacoplar Firestore para ACK antes de commit **cambiaría la garantía de durabilidad/idempotencia**: no es una optimización segura propuesta para aplicar sin rediseño y autorización. JVM/Cloudflare no tienen fundamento de tuning en estos datos.

## Salida solicitada

```text
SERVER-7 S7-06 ACK LATENCY DIAGNOSIS
====================================
R2_STAGE_FAILED=60_USERS
ACK_GATE_LIMIT=1000_MS
R2_REPORTED_CUMULATIVE_ACK_P95=1247_MS
COMMAND_ACK_MEASUREMENT_BOUNDARY=FIRST_SEND_START_TO_MATCHING_ACCEPTED_RECEIPT_HANDLER_MONOTONIC_MS
ACK_20_SAMPLES=330
ACK_20_P50=509_MS
ACK_20_P90=750_MS
ACK_20_P95=1008_MS
ACK_20_P99=1266_MS
ACK_20_MAX=1665_MS
CPU_20_AVG=15.472_PERCENT_QUOTA
CPU_20_PEAK=74.444_PERCENT_QUOTA
JVM_HEAP_AT_20=334401600_BYTES_END
GC_COUNT_DELTA_20=5
GC_TIME_DELTA_20=136_MS
REDIS_CPU_20=0.04559_CORES_AVG_0.22226_PEAK
WS_20_P95=0_MS
MATCHMAKING_20_P95=4774_MS
REST_20_P95=10902_MS
ACK_40_SAMPLES=697
ACK_40_P50=522_MS
ACK_40_P90=847_MS
ACK_40_P95=950_MS
ACK_40_P99=1316_MS
ACK_40_MAX=1801_MS
CPU_40_AVG=15.444_PERCENT_QUOTA
CPU_40_PEAK=46.926_PERCENT_QUOTA
JVM_HEAP_AT_40=161898704_BYTES_END
GC_COUNT_DELTA_40=9
GC_TIME_DELTA_40=219_MS
REDIS_CPU_40=0.05488_CORES_AVG_0.26820_PEAK
WS_40_P95=0_MS
MATCHMAKING_40_P95=5236_MS
REST_40_P95=9968_MS
ACK_60_SAMPLES=819
ACK_60_P50=705_MS
ACK_60_P90=1239_MS
ACK_60_P95=1458_MS
ACK_60_P99=1825_MS
ACK_60_MAX=2247_MS
CPU_60_AVG=28.494_PERCENT_QUOTA
CPU_60_PEAK=63.095_PERCENT_QUOTA
JVM_HEAP_AT_60=122503952_BYTES_END
GC_COUNT_DELTA_60=7
GC_TIME_DELTA_60=307_MS
REDIS_CPU_60=0.09063_CORES_AVG_0.39737_PEAK
WS_60_P95=0_MS
MATCHMAKING_60_P95=5858_MS
REST_60_P95=17327_MS
MAX_SUSTAINED_CPU_30S=14.862_PERCENT_QUOTA_MAX_WINDOW_MINIMUM
CPU_STOP_TRIGGERED=NO
ACK_LATENCY_CORRELATES_WITH_API_CPU=YES_DESCRIPTIVE_30S_PEARSON_0.834_NOT_CAUSAL
GC_COUNT_DELTA=21
GC_TIME_DELTA=662_MS
ACK_LATENCY_CORRELATES_WITH_GC=INCONCLUSIVE
REDIS_BOTTLENECK_EVIDENCE=INCONCLUSIVE
FIRESTORE_ON_COMMAND_ACK_CRITICAL_PATH=YES
REDIS_OPS_PER_GAMEPLAY_COMMAND=0_DIRECT_NORMAL_ACCEPTED_PATH
REDIS_ROUND_TRIPS_PER_GAMEPLAY_COMMAND=0_DIRECT_NORMAL_ACCEPTED_PATH
PER_MATCH_SERIALIZATION=FIRESTORE_TRANSACTION_CONFLICT_AND_SEQUENCE_CHECK
GLOBAL_SERIALIZATION=NO_GLOBAL_GAMEPLAY_LOCK_FOUND
CROSS_MATCH_CONTENTION_RISK=SHARED_CPU_FIRESTORE_CLIENT_AND_TRANSPORT_RESOURCES
FOREGROUND_AND_BACKGROUND_SHARE_EXECUTOR=NO_AT_APPLICATION_DISPATCH_LEVEL_SHARED_CLIENT_INTERNALS
WORKER_CONTENTION_EVIDENCE=STRUCTURAL_SHARED_DATABASE_ACCESS_NOT_TIMED
CLIENT_SIDE_LATENCY_CONTRIBUTION=INCONCLUSIVE
NETWORK_OR_CLOUDFLARE_BOTTLENECK_EVIDENCE=INCONCLUSIVE
SLOWEST_COMMAND_TYPE=NOT_MEASURED
SLOWEST_COMMAND_P95=NOT_MEASURED
MATCHES_WITH_ACK_P95_OVER_1000MS=NOT_MEASURED
ACK_60_FIRST_30S=P95_1200_MS_N_173
ACK_60_LAST_30S=P95_1309_MS_N_164
R2_CANCELLED_MATCHES_EXPECTED_RECOVERY=YES
PRIMARY_BOTTLENECK_CLASSIFICATION=INCONCLUSIVE
CONFIDENCE=HIGH_IN_MEASUREMENT_AND_CODE_BOUNDARIES_LOW_IN_DOMINANT_CAUSE
PROPOSED_OPTIMIZATIONS=TIMING_FIRST_THEN_TARGET_MEASURED_STORAGE_WORKER_PUBLICATION_OR_CLIENT_COST
NEXT_DISCRIMINATING_EXPERIMENT=AUTHORIZED_40_TO_60_COMPARISON_WITH_PHASE_TIMING_AND_CLIENT_RESOURCE_SAMPLES
NEW_LOAD_EXECUTED=NO
CPU_LIMIT_CHANGED=NO
RAM_LIMIT_CHANGED=NO
JVM_CHANGED=NO
REDIS_CHANGED=NO
CLOUDFLARE_CHANGED=NO
COMMIT=NONE
PUSH=NONE
SERVER_7_100_RERUN_ALLOWED=NO
NEXT=S7-06 REVIEW
```
