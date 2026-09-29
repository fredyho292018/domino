# SERVER-7 / S7-07 — Command ACK phase timing

**Resultado: FIRESTORE_DOMINANT dentro del recorrido medido de la transacción.** El experimento avanzó 20→40 y se detuvo por el gate ACK en 40; **60 no se ejecutó**, ni tampoco 80/100/250. No es un R3 ni determina capacidad máxima. El acumulado final fue p95=1.020 ms frente al límite 1.000 ms. No se relajó ningún gate ni se aplicó optimización.

La transacción Firestore, descontando el dominio, ocupa el 67,5% y 65,7% del tiempo total de ACK cliente agregado en 20 y 40. La lectura previa añade 12,5% y 12,9%: el conjunto relacionado con almacenamiento representa aproximadamente 80% y 79%. Esto localiza el coste en el recorrido de Firestore, **no distingue aún entre espera del SDK/canal, lecturas, preparación de writes y commit remoto**. Un canal configurado no demuestra saturación del canal. No se atribuye este tiempo a Cloudflare.

El STOP a 40 no demuestra regresión frente al STOP a 60 de R2: S7-07 comenzó con una API recién desplegada, distinto estado de calentamiento/JIT, variabilidad de red y la propia instrumentación. No hubo A/B controlado. Se preserva el resultado observado sin atribuir esa diferencia a la captura.

## Cobertura y condiciones fijadas

Base a4a2542363d93301bbede958b18bbce9fb3e8d1f, sin commit/push. Imagen de instrumentación `cuban-domino-api:s707-d86e1d5170a1`; manifiesto SHA-256 del código en `Generated/S707/source-manifest.json`. Run `6e4a24c0-b6b7-4aa8-afa9-f8daa25b0832`.

Plan guardado antes de carga: objetivos 20/40/60, 120 s tras alcanzar cada objetivo, mismos tiempos de pensar, seeds, identidad LOAD y gates R2. Se conservó el gate acumulado original para comparabilidad; las distribuciones diagnósticas se separaron por etapa. El tiempo adicional de discovery en 20 no modifica el hold fijado. CPU=4, RAM=4 GiB, flags JVM/env/montajes/red/gates sin cambios. Solo se reemplazó la imagen API. Redis conservó identidad y arranque; no se reinició.

Una aserción posterior del primer script de despliegue falló, sin iniciar carga. No se conservó la línea exacta de aquella comparación. Antes de continuar se verificó semánticamente la configuración efectiva: Compose difiere únicamente en image, variables por clave/valor iguales, montajes por conjunto iguales, API healthy y recursos iguales. Evidencia `s707-deployed.json`. No se declara que la aserción inicial hubiera pasado.

## Medición implementada

- Inicio cliente: reloj monotónico antes del primer send; fin: handler de COMMAND_ACCEPTED coincidente. Se mantuvo el cálculo previo.
- Inicio servidor: entrada al callback WebSocket; fin: retorno exitoso del envío al transporte del ACK. No representa recepción del cliente.
- Authorization agrega protocolo, validación, espera del monitor observada desde callback y autorización de asiento antes/después de la prelectura. La verificación Firebase inicial no se repite por comando.
- Lookup mide prelectura runtime. Firestore mide llamada síncrona transaccional menos dominio; registra además transacción inclusiva y número de callbacks/intentos. Incluye lecturas internas, espera, preparación de writes y commit; **FIRESTORE_COMMIT no es un span del RPC Commit exclusivamente**.
- Domain suma ejecución del engine en todos los intentos. Post-commit incluye publicación/preparación de updates. ACK emission incluye serialización, monitor, cola de salida y envío. Outbound queue se mide como subintervalo diagnóstico, no se suma otra vez al total.

Las seis fases particionan exactamente cada total en nanosegundos; la transacción inclusiva y la cola de salida son métricas superpuestas explícitas. Se correlacionaron **967/967** muestras usando SHA-256 del commandId existente, sin UID, Match ID, token, credencial ni cambios de protocolo. Cero incompletas, pérdidas o residuales negativos. El residual se calcula **por par** cliente menos servidor y luego se obtiene su percentil. No se restan percentiles.

Activación local mediante MBean, desactivada por defecto, con caducidad y buffer máximo de 20.000 registros. No endpoint HTTP ni labels Prometheus por identidad. Swarm usa buffer acotado de 2.048 correlaciones y exporta por lote en su resumen existente. No logs síncronos por fase. Captura cada 2 s mediante agente sin transformación de bytecode ni cambio de flags, ya detenido.

No hay executor de comandos de aplicación entre callback y dominio: la cola anterior de Tomcat no es observable con estos puntos. **EXECUTOR_QUEUE_WAIT=NOT_MEASURED**, no cero. No existe lock local por Match; la espera/serialización de base de datos permanece dentro del tramo Firestore y no está aislada. No se sustituyen esas métricas por la cola de salida.

## Distribuciones por etapa

Milisegundos, nearest-rank de S7-02; se conservó resolución de nanosegundos para fases. Los percentiles de columnas distintas no se suman.

| Métrica p95 | 20 usuarios (351) | 40 usuarios (616) | 60 |
|---|---:|---:|---|
| Cliente ACK | 907.000 | 1069.000 | No ejecutado |
| Servidor total | 754.776 | 909.677 | No ejecutado |
| Residual fuera del servidor medido | 160.185 | 188.809 | No ejecutado |
| Authorization | 5.620 | 4.199 | No ejecutado |
| Lookup | 121.813 | 154.358 | No ejecutado |
| Dominio | 5.471 | 5.142 | No ejecutado |
| Transacción menos dominio | 626.378 | 682.947 | No ejecutado |
| Post-commit | 67.763 | 64.385 | No ejecutado |
| Emisión ACK | 26.260 | 23.244 | No ejecutado |

`Generated/S707/analysis.json` contiene count/p50/p95/p99/max de **cada fase**, transacción inclusiva, residual, total, cola de salida y tipos. Ventanas por incorporación de grupo, incluidos ramp-up y ACK finales antes de salida; atribución por finalización del cliente. Se usan timestamps de cliente para selección de etapa; comparación con recursos de servidor es descriptiva y no una sincronización submilisegundo.

| Etapa | API CPU media/pico % cuota | Host CPU media % | GC count/ms | Heap pico MiB | Outbound queue p95 ms |
|---|---|---:|---|---:|---:|
| 20 | 15.24/76.58 | 18.65 | 5/171 | 356.98 | 20.630 |
| 40 | 18.96/62.30 | 26.37 | 4/131 | 375.98 | 18.126 |

CPU cores media: 0.610 y 0.758; JVM processors=4. CPU_STOP no se disparó. Pearson entre p95 ACK y CPU media de ventanas de 30 s: 0.630; con tiempo GC: -0.203. Muestras limitadas, ramp-up/JIT y factores comunes impiden causalidad. No evidencia de dominio pesado ni retry amplification: **todos los 967 tuvieron 1 intento/0 retries transaccionales**. Esto no mide retries de RPC individuales.

Snapshots de schedulers: cada pool tiene 1 hilo; pico activo=1. Colas máximas matchmaking=1, realtime-lifecycle=3, online-turn=1. Incluyen tareas periódicas futuras: no son prueba de saturación. Se recogieron threadCount y peakThreadCount JVM. No se instrumentó ocupación del executor interno gRPC ni cola previa de Tomcat; no se inventan esos valores.

## Tipos de comando

| Etapa | Tipo | n | ACK cliente p95 ms |
|---|---|---:|---:|
| 20 | NEXT_ROUND | 5 | 1003 |
| 20 | PASS | 49 | 828 |
| 20 | PLAY_TILE | 297 | 923 |
| 40 | NEXT_ROUND | 13 | 1209 |
| 40 | PASS | 145 | 1058 |
| 40 | PLAY_TILE | 458 | 1069 |

NEXT_ROUND tiene pocos casos (5/13); no basta para afirmar que sea el responsable único. PLAY_TILE aporta la mayor cantidad de muestras. Los otros tipos acotados no tuvieron muestras.

## Configuración Firestore auditada sin modificaciones

La instancia activa informa InstantiatingGrpcChannelProvider; min/max/initialChannelCount=1, preemptiveRefresh=false; maxRpcsPerChannel=2147483647. Service retry settings informa total/initial/max RPC 50 s, initial backoff 1 s, multiplicador2, maxbackoff32 s, maxAttempts6. **No confundir ese objeto general con retry settings efectivos de cada RPC.**

El bytecode del SDK instalado muestra que, al usar opciones generales por defecto, GrpcFirestoreRpc construye settings específicos y fija maxAttempts=5. Reconstrucción local sin cliente/red, `firestore-sdk-settings.txt`: Commit y BeginTransaction total/RPC60 s, backoff100 ms×1,3, max60 s, maxAttempts5; Commit retry codes RESOURCE_EXHAUSTED/UNAVAILABLE. BatchGet total/RPC300 s, maxAttempts5, retry codes UNAVAILABLE/DEADLINE_EXCEEDED/INTERNAL. Esto es inferencia verificada de la rama de inicialización instalada, no una inspección de un objeto RPC privado en vivo. Aplicación: 8 intentos transaccionales; prelectura Future.get15 s; future transaccional sin timeout explícito adicional. Ningún ajuste aplicado.

## Pruebas y overhead

Pasó batería completa backend/Swarm durante implementación. Tras los últimos puntos de captura: 26 pruebas backend focalizadas (incluye 6 nuevas de timing y transporte), 44 Swarm; 5 pruebas Python de percentiles/correlación. Se comprobó orden, fases ausentes, regresión de reloj, suma, retries, correlación sin ID crudo ni secretos, nearest-rank y tratamiento explícito de residual negativo. La ejecución real confirma 967 particiones completas.

Microbenchmark local: 2.000 trazas de calentamiento y 10.000 medidas, media **22.934 ns (22,934 µs)** por traza con hash y fases. No incluye I/O de captura ni es A/B de overhead remoto; no se afirma impacto cero. La captura agrega por lotes y está acotada.

## Recuperación y preservación

10 partidas, 0 completadas, **10 CANCELLED** tras STOP y ciclo de abandono. Dos lecturas autoritativas estables después de >360 s confirman cero activas y baselineReady. El descubrimiento une roots, assignments y pending work: activeLoad=0 incluye ausencia de work LOAD pendiente. Auditoría de las diez secuencias/turnos: PASS. Redis: cero claves runOwned y cero miembros del run en estructuras compartidas. API/Redis healthy, restarts0/OOMfalse; gate cerrado; Redis sin cambios; observadores detenidos. SERVER-6 antes/después: 5 historiales, 5 snapshots, 3 manifiestos Replay disponibles. R2 se verificó por hash sin modificación. No se crea History normal para estas cancelaciones ni se presenta recuperación como corrupción.

## Siguiente acción propuesta — no ejecutada

Antes de optimizar, separar dentro de la transacción los tres reads, preparación de writes, espera de commit/RPC y cola del SDK/canal. Mantener autoridad, durabilidad e idempotencia y los mismos recursos. No responder ACK antes de persistir. La evidencia no justifica aumentar CPU/RAM, alterar Redis/Cloudflare, cambiar pools o reintentar 100 usuarios. La clasificación tiene confianza alta para el tramo predominante, pero sigue sin identificar la causa interna del coste de almacenamiento. No hay datos de 60 en S7-07 para extrapolar.

## Archivos de implementación

Backend: `realtime/AckPhaseTiming.kt`, `realtime/RealtimeHandler.kt`, `realtime/ConnectionOutbound.kt`, `online/OnlineMatchService.kt`, `online/OnlineRepository.kt`, test `realtime/AckPhaseTimingTest.kt` bajo `server/domino/src`. Swarm: `Metrics.kt`, `SimulatedClient.kt`. Validación: `client/Validation/S707TimingAgent.java`, `S707TimingAnalysis.py`, `S707TimingAnalysisTests.py`. Plan, manifiestos, scripts operacionales y evidencia aislados en `client/Validation/Generated/S707/`. Archivos ajenos preservados, sin commit/push.

## Salida solicitada

```text
SERVER-7 S7-07 ACK PHASE TIMING
===============================
FILES_CHANGED=8_APPLICATION_AND_TEST_FILES_3_VALIDATION_TOOLS_REPORT_AND_GENERATED_EVIDENCE
INSTRUMENTATION_TESTS=PASS_26_TARGETED_BACKEND_44_SWARM_5_ANALYSIS_FULL_BACKEND_SUITE_PASSED
TIMING_OVERHEAD_ASSESSED=LOCAL_TRACE_MEAN_22934_NS_NOT_A_REMOTE_AB_TEST
COMMAND_ACK_BOUNDARY=CLIENT_FIRST_SEND_TO_ACCEPTED_HANDLER_SERVER_CALLBACK_TO_TRANSPORT_SEND_RETURN
PHASES_IMPLEMENTED=AUTHORIZATION_LOOKUP_DOMAIN_FIRESTORE_TRANSACTION_EXCLUDING_DOMAIN_POST_COMMIT_ACK_EMISSION
EXECUTOR_QUEUE_WAIT_MEASURED=NO_PRE_CALLBACK_NOT_OBSERVABLE
OUTBOUND_QUEUE_WAIT_MEASURED=YES_SUBSET_OF_ACK_EMISSION
MATCH_SERIALIZATION_WAIT_MEASURED=NO_LOCAL_MATCH_LOCK_DATABASE_WAIT_NOT_SEPARATED
FIRESTORE_RETRY_MEASURED=YES_TRANSACTION_CALLBACK_ATTEMPTS_RPC_RETRIES_NOT_MEASURED
TRANSACTION_ATTEMPTS=1_FOR_ALL_967_SAMPLES
CORRELATED_SAMPLES=967_OF_967
INCOMPLETE_PHASES=0
NEGATIVE_PHASE_DURATIONS=0
NEGATIVE_RESIDUALS=0

20_USERS:
SAMPLES=351
CLIENT_ACK_P95=907.000_MS
SERVER_TOTAL_P95=754.776_MS
AUTH_P95=5.620_MS
MATCH_LOOKUP_P95=121.813_MS
DOMAIN_P95=5.471_MS
FIRESTORE_COMMIT_P95=626.378_MS
POST_COMMIT_P95=67.763_MS
ACK_EMISSION_P95=26.260_MS
OUTSIDE_SERVER_RESIDUAL_P95=160.185_MS
OUTBOUND_QUEUE_P95=20.630_MS
QUEUE_WAIT_P95=NOT_MEASURED
MATCH_SERIALIZATION_WAIT_P95=NOT_SEPARATELY_MEASURED
CPU_QUOTA_AVG=15.244_PERCENT
CPU_QUOTA_PEAK=76.584_PERCENT

40_USERS:
SAMPLES=616
CLIENT_ACK_P95=1069.000_MS
SERVER_TOTAL_P95=909.677_MS
AUTH_P95=4.199_MS
MATCH_LOOKUP_P95=154.358_MS
DOMAIN_P95=5.142_MS
FIRESTORE_COMMIT_P95=682.947_MS
POST_COMMIT_P95=64.385_MS
ACK_EMISSION_P95=23.244_MS
OUTSIDE_SERVER_RESIDUAL_P95=188.809_MS
OUTBOUND_QUEUE_P95=18.126_MS
QUEUE_WAIT_P95=NOT_MEASURED
MATCH_SERIALIZATION_WAIT_P95=NOT_SEPARATELY_MEASURED
CPU_QUOTA_AVG=18.958_PERCENT
CPU_QUOTA_PEAK=62.303_PERCENT

60_USERS:
STATUS=NOT_RUN_GATE_STOP_AT_40
CLIENT_ACK_P95=NOT_MEASURED
SERVER_TOTAL_P95=NOT_MEASURED
AUTH_P95=NOT_MEASURED
MATCH_LOOKUP_P95=NOT_MEASURED
QUEUE_WAIT_P95=NOT_MEASURED
MATCH_SERIALIZATION_WAIT_P95=NOT_MEASURED
DOMAIN_P95=NOT_MEASURED
FIRESTORE_COMMIT_P95=NOT_MEASURED
POST_COMMIT_P95=NOT_MEASURED
ACK_EMISSION_P95=NOT_MEASURED
OUTSIDE_SERVER_RESIDUAL_P95=NOT_MEASURED
CPU_QUOTA_AVG=NOT_MEASURED
CPU_QUOTA_PEAK=NOT_MEASURED

PRIMARY_LATENCY_COMPONENT=FIRESTORE_TRANSACTION_PATH_EXCLUDING_DOMAIN
PRIMARY_BOTTLENECK_CLASSIFICATION=FIRESTORE_DOMINANT
CONFIDENCE=HIGH_FOR_MEASURED_BOUNDARY_NOT_FOR_INTERNAL_RPC_CAUSE
CPU_CORRELATION=DESCRIPTIVE_PEARSON_0.630_NOT_CAUSAL
GC_CORRELATION=DESCRIPTIVE_PEARSON_-0.203_INCONCLUSIVE
PROPOSED_NEXT_ACTION=SEPARATE_TRANSACTION_READS_COMMIT_RPC_AND_SDK_QUEUE_BEFORE_OPTIMIZATION
SERVER_CONFIGURATION_CHANGED=NO
API_IMAGE_CHANGED=YES_INSTRUMENTATION_ONLY
CPU_LIMIT_CHANGED=NO
RAM_LIMIT_CHANGED=NO
JVM_FLAGS_CHANGED=NO
FIRESTORE_CONFIGURATION_CHANGED=NO
REDIS_CHANGED=NO
CLOUDFLARE_CHANGED=NO
PROTOCOL_CHANGED=NO
SERVER7_60_STARTED=NO
SERVER7_100_RERUN_STARTED=NO
SERVER7_250_STARTED=NO
MATCHES_STARTED=10
MATCHES_CANCELLED=10
POST_LOAD_ACTIVE_MATCHES=0
POST_LOAD_PENDING_WORK=0
REDIS_RUN_OWNED_KEYS=0
SERVER6_REGRESSION=PASS
COMMIT=NONE
PUSH=NONE
S7_07_SUCCESS=YES
EXPERIMENT_COVERAGE=20_AND_40_ONLY_STOPPED_BY_UNCHANGED_GATE
NEXT=S7-07 REVIEW
```
