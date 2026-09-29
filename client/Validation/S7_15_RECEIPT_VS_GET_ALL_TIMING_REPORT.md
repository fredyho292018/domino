# SERVER-7 / S7-15 — Firestore receipt vs getAll timing

S7-15 queda incompleto por una parada de seguridad en 20 usuarios: matchmaking p95 **11.259 ms**, límite **10.000 ms**. Se respetó el gate existente y no se iniciaron 40/60/100/250 usuarios. La instrumentación y su captura remota funcionan; no existe evidencia para clasificar el crecimiento 40→60. No se reintentó la carga ni se relajó el umbral.

Run: `fb5fcc99-be22-41e7-987f-8d10684dcd00`. Imagen: `cuban-domino-api:s715-adc5ba628731`. Base: `5a74d7917fc7fb0baa04bbdf2c8fc45de5a13f14`. Hash de instrumentación: `adc5ba6287313c24a39cffa801884c809e48c14170c6d6cd977bf0dea256cd43`. Inicio: 2026-09-27T17:32:36.173566Z; clientes desconectados: 2026-09-27T17:35:18.343163Z.

## Resultados medidos en 20 usuarios

Las métricas siguientes corresponden a 312 comandos nuevos correlacionados. Muestras cliente/servidor: 312/312; correlacionadas: 312; pérdidas: 0; residuales negativos: 1.

| Fase | Muestras | p50 ms | p95 ms | p99 ms | Máximo ms | Media ms |
|---|---:|---:|---:|---:|---:|---:|
| CLIENT_ACK_TOTAL | 312 | 441 | 968 | 1273 | 2582 | 524.032 |
| SERVER_TOTAL | 312 | 392 | 847 | 1162 | 2502 | 472.621 |
| PRIOR_RUNTIME | 312 | 61 | 144 | 175 | 220 | 78.384 |
| RECEIPT_MISS | 312 | 58 | 137 | 181 | 239 | 72.633 |
| GET_ALL | 312 | 60 | 153 | 195 | 214 | 77.570 |
| TRANSACTION_START_WAIT | 312 | 60 | 157 | 206 | 366 | 77.812 |
| DOMAIN_AFTER_READS | 312 | 0 | 10 | 21 | 108 | 2.238 |
| SDK_FINALIZATION | 312 | 91 | 190 | 301 | 1063 | 110.459 |
| FS_TOTAL | 312 | 362 | 745 | 988 | 1293 | 429.251 |
| CALLBACK_LOCAL_OTHER | 312 | 2 | 41 | 114 | 271 | 10.155 |
| BETWEEN_ATTEMPTS | 312 | 0 | 0 | 0 | 0 | 0.000 |

Recibos encontrados: 0; comandos con reintentos: 0. Sin muestras de una clase, no se infiere su rendimiento. Cada comando nuevo conservó tres rondas y cuatro documentos.

Existe un residual negativo: cliente 610 ms frente a servidor 614,611974 ms, diferencia −4,611974 ms. Se conserva y señala, sin truncarlo a cero. El servidor termina su medición después del retorno de `transport.send(frame)` (`ConnectionOutbound.kt`); esta frontera y la recepción del cliente no garantizan intervalos perfectamente anidados. Es una limitación de comparación de totales; no se atribuye el residual a red ni a desfase de reloj y no se altera ninguna frontera de medición. Las fases Firestore mantienen su partición monotónica interna válida.

El corte del gate tiene 302 ACK, p95 968 ms y p99 1273 ms. La tabla usa el intervalo completo correlacionado hasta la desconexión; una posible diferencia no es un cambio de percentiles.

CPU de cuota: media 24.09%, pico 76.33%; CPU host media 28.33%. Heap máximo 390045312 bytes; memoria API máxima 894279680 bytes. GC: 6 colecciones / 450 ms. Se conservaron 4 CPU, 4 GiB y JVM availableProcessors=4. Estos datos no establecen causalidad CPU/latencia ni contradicen por sí solos S7-13. Correlaciones descriptivas por ventanas de 30 s: `analysis.json`; no son un A/B.

## Interpretación y siguiente dirección

**PRIMARY_READ_ROUND=INCONCLUSIVE.** Las tres fases se midieron individualmente en 20 usuarios, pero no se midió el crecimiento 40→60. No se declara una fase dominante ni se extrapolan los resultados anteriores. FINALIZATION_DOMINANT_GROWTH queda no medido; no hay base para responder YES o NO.

Intentos instrumentados: 312; reintentos: 0. La lectura acotada del log de API encontró cero líneas ABORTED, de contención, RESOURCE_EXHAUSTED y UNAVAILABLE en el intervalo. No hay trazas de locks ni identificación de documentos compartidos; ausencia de estos indicadores no demuestra ausencia de espera interna. No se atribuye la latencia a contención.

Una sola dirección propuesta: **OTHER — revisar el gate de matchmaking con la evidencia capturada antes de autorizar otra ejecución S7-15**. No implementar optimización, retirar lecturas ni alterar el contrato de revisión. Tampoco se considera validada una hipótesis de arranque frío o de sobrecoste de instrumentación por este único resultado.

## Recuperación y preservación

Cinco partidas iniciadas, estado final autoritativo `{"COMPLETED": 0, "CANCELLED": 5, "FAILED": 0, "ACTIVE": 0, "UNKNOWN": 0}`. Se esperó la recuperación normal, sin reiniciar Redis ni borrar claves manualmente. Dos comprobaciones autoritativas confirmaron cero partidas LOAD activas, cero trabajo pendiente y cero partidas no clasificadas. Auditoría de corrección de las cinco partidas: PASS. Inventario Redis de solo lectura: sin claves o miembros propios del run. Cero procesos Swarm huérfanos. Observador temporal detenido; API y Redis saludables, gates cerrados, sin OOM ni reinicios de contenedor.

SERVER-6 conserva los cinco IDs originales, cinco snapshots y tres Replay disponibles, antes y después. Tres intentos del lector Java tuvieron timeout, incluido el intento precompilado; la primera salida está en `recovery.log`. La última comprobación con HTTP/1.1, misma identidad y mismos endpoints de solo lectura, completó las nueve respuestas HTTP 200 y pasó. Esto no identifica la causa de los timeouts Java. El cierre del observador y la comprobación Redis se completaron por separado, sin repetir carga. No se iniciaron partidas con esa identidad funcional.

## Cambios y validación

- `OnlineRepository.kt`: sustituye únicamente las envolturas de medición de recibo/getAll, conservando las llamadas existentes.
- `FirestorePhaseTiming.kt`: intervalos y contadores diferenciados, clasificación hit/miss y rechazo de trazas incompletas.
- `FirestoreReadRoundTimingTest.kt`: ocho pruebas nuevas. Con las pruebas existentes: 21 unitarias PASS y ocho de emulador PASS.
- Herramientas operacionales y evidencia en `client/Validation/Generated/S715`; revisión de imagen y escaneo de indicadores secretos PASS. Pruebas del analizador y aislamiento de histogramas PASS.

No commit ni push. Los archivos preexistentes de Unity/Swarm permanecen fuera de la imagen. La API instrumentada queda desplegada con las mismas variables, montajes, puertos y recursos. El colector se detuvo y su instrumentación acotada fue desactivada; no se volvió a desplegar la imagen anterior.

## MediciÃ³n y lÃ­mites

La imagen S7-15 se construyÃ³ desde HEAD `5a74d7917fc7fb0baa04bbdf2c8fc45de5a13f14` mÃ¡s exactamente dos archivos de instrumentaciÃ³n y su prueba nueva. El manifiesto `run.json` identifica los hashes de esos archivos. Es una imagen de cÃ³digo local sin commit; la etiqueta de revisiÃ³n identifica la base, no afirma que los cambios estÃ©n en Git. No contiene cambios preexistentes de Unity ni de Swarm.

Se mantienen el runtime previo, el recibo como primera lectura transaccional y el `getAll(runtime, turnWork)`: tres rondas y cuatro documentos para el comando nuevo. Un recibo encontrado conserva el retorno temprano, sin `getAll`. No cambian la comprobaciÃ³n de revisiÃ³n, el nÃºmero mÃ¡ximo de intentos, las escrituras atÃ³micas ni la persistencia anterior al ACK.

Todas las fases usan reloj monotÃ³nico del proceso. La marca de finalizaciÃ³n de pared se utiliza Ãºnicamente para asociar las muestras a etapas, no para medir la latencia.

| Fase | Frontera real |
|---|---|
| PRIOR_RUNTIME | InvocaciÃ³n de `DocumentReference.get()` hasta resoluciÃ³n de su futuro, antes de decodificar el runtime. |
| TRANSACTION_START_WAIT | Justo antes de `runTransaction` hasta la entrada al primer callback instrumentado. Incluye actividad de SDK y planificaciÃ³n; no equivale a una medida pura de adquisiciÃ³n, red o lock. |
| RECEIPT | `tx.get(receipt)` y espera del futuro, incluyendo acceso al mapa `.data`. Clasifica presencia solamente; no conserva contenido. |
| GET_ALL | `tx.getAll(runtime, turnWork)` hasta resoluciÃ³n del futuro. No incluye asociaciÃ³n de snapshots ni decodificaciÃ³n. |
| DOMAIN_AFTER_READS | Llamada existente al motor de comando. La decodificaciÃ³n, validaciÃ³n de revisiÃ³n y preparaciÃ³n de escrituras quedan en callback local restante. |
| SDK_TRANSACTION_FINALIZATION | Salida del Ãºltimo callback hasta resoluciÃ³n del futuro de `runTransaction`. No es una medida aislada del Commit RPC. |
| FS_TOTAL | Runtime previo mÃ¡s intervalo total de transacciÃ³n. Incluye dominio y trabajo local dentro del callback; no es exclusivamente espera remota. |
| SERVER_TOTAL / CLIENT_ACK | Fronteras existentes S7-07; correlaciÃ³n por hash del comando. |

Los intentos repetidos, recibos encontrados y comandos nuevos se separan. No se mezclan duplicados con el diagnÃ³stico del camino normal. Si una clase no tiene muestras, se declara no medida, no latencia cero.

El colector drena datos cada dos segundos fuera del camino del comando. La cola estÃ¡ limitada a 20.000 muestras y la instrumentaciÃ³n se desactiva por tiempo. No hay escritura diagnÃ³stica a archivo por comando en el camino crÃ­tico. Las pruebas locales midieron 7.313 ns de media por traza completa (incluida creaciÃ³n del snapshot); es una comprobaciÃ³n local de coste, no un A/B remoto ni una atribuciÃ³n causal de rendimiento.

Se reutilizan histogramas fusionados de 1 ms con rango mÃ¡s prÃ³ximo y el tratamiento existente de overflow. Los cuadros de fases usan los mismos comandos correlacionados por etapa, desde lanzamiento del grupo hasta lanzamiento del siguiente o desconexiÃ³n final; incluyen el tiempo de descubrimiento autoritativo despuÃ©s del hold. Los histogramas del gate se guardan aparte en su corte anterior al descubrimiento. Esto coincide con la distinciÃ³n de S7-13. No se suman p95 independientes para obtener un p95 total.

Cada etapa mantiene 120 s despuÃ©s de alcanzar su poblaciÃ³n. Los gates de correcciÃ³n, salud, memoria, CPU sostenida y matchmaking permanecen activos. La latencia ACK se registra como resultado diagnÃ³stico y no autoriza avanzar mÃ¡s allÃ¡ de 60. Tras desconexiÃ³n se espera la recuperaciÃ³n normal y se realizan dos lecturas autoritativas, auditorÃ­a de eventos, inventario Redis de solo lectura y preservaciÃ³n de SERVER-6.

## Salida requerida

```text
SERVER-7 S7-15 RECEIPT VS GET_ALL TIMING
========================================
FILES_CHANGED=2_MAIN_FILES_PLUS_1_NEW_TEST_AND_REPORT
INSTRUMENTATION_TESTS=21_PASS_8_EMULATOR_PASS
TIMING_OVERHEAD_ACCEPTABLE=YES_LOCAL_TRACE_MEAN_7313_NS

20_USERS:
CLIENT_ACK_P95=968_MS
SERVER_TOTAL_P95=847_MS
PRIOR_RUNTIME_P95=144_MS
RECEIPT_MISS_P95=137_MS
GET_ALL_P95=153_MS
TRANSACTION_START_WAIT_P95=157_MS
SDK_FINALIZATION_P95=190_MS
FS_TOTAL_P95=745_MS
CPU_QUOTA_AVG=24.09_PERCENT
CPU_QUOTA_PEAK=76.33_PERCENT
MATCHMAKING_P95=11259_MS
STOP=LATENCY_STAGE_GATE_matchmakingLatency

40_USERS:
CLIENT_ACK_P95=NOT_RUN_SAFETY_STOP_AT_20
SERVER_TOTAL_P95=NOT_RUN_SAFETY_STOP_AT_20
PRIOR_RUNTIME_P95=NOT_RUN_SAFETY_STOP_AT_20
RECEIPT_MISS_P95=NOT_RUN_SAFETY_STOP_AT_20
GET_ALL_P95=NOT_RUN_SAFETY_STOP_AT_20
TRANSACTION_START_WAIT_P95=NOT_RUN_SAFETY_STOP_AT_20
SDK_FINALIZATION_P95=NOT_RUN_SAFETY_STOP_AT_20
FS_TOTAL_P95=NOT_RUN_SAFETY_STOP_AT_20
CPU_QUOTA_AVG=NOT_RUN_SAFETY_STOP_AT_20
CPU_QUOTA_PEAK=NOT_RUN_SAFETY_STOP_AT_20

60_USERS:
CLIENT_ACK_P95=NOT_RUN_SAFETY_STOP_AT_20
SERVER_TOTAL_P95=NOT_RUN_SAFETY_STOP_AT_20
PRIOR_RUNTIME_P95=NOT_RUN_SAFETY_STOP_AT_20
RECEIPT_MISS_P95=NOT_RUN_SAFETY_STOP_AT_20
GET_ALL_P95=NOT_RUN_SAFETY_STOP_AT_20
TRANSACTION_START_WAIT_P95=NOT_RUN_SAFETY_STOP_AT_20
SDK_FINALIZATION_P95=NOT_RUN_SAFETY_STOP_AT_20
FS_TOTAL_P95=NOT_RUN_SAFETY_STOP_AT_20
CPU_QUOTA_AVG=NOT_RUN_SAFETY_STOP_AT_20
CPU_QUOTA_PEAK=NOT_RUN_SAFETY_STOP_AT_20

PRIOR_RUNTIME_GROWTH_40_TO_60=NOT_MEASURED
RECEIPT_GROWTH_40_TO_60=NOT_MEASURED
GET_ALL_GROWTH_40_TO_60=NOT_MEASURED
TRANSACTION_START_WAIT_GROWTH_40_TO_60=NOT_MEASURED
SDK_FINALIZATION_GROWTH_40_TO_60=NOT_MEASURED
PRIMARY_READ_ROUND=INCONCLUSIVE
FINALIZATION_DOMINANT_GROWTH=NOT_MEASURED
TRANSACTION_ATTEMPTS=312
TRANSACTION_RETRIES=0
FIRESTORE_CONTENTION_EVIDENCE=NONE_OBSERVED_NOT_PROVEN_ABSENT
GAMEPLAY_SEMANTICS_CHANGED=NO
REVISION_CONTRACT_CHANGED=NO
IDEMPOTENCY_CONTRACT_CHANGED=NO
TRANSACTION_SEMANTICS_CHANGED=NO
ACK_PERSISTENCE_SEMANTICS_CHANGED=NO
POST_TEST_ACTIVE_LOAD_MATCHES=0
POST_TEST_PENDING_WORK=0
UNCLASSIFIED_LOAD_MATCHES=0
ORPHAN_SWARM_PROCESSES=0
SERVER6_REGRESSION=PASS
PROPOSED_NEXT_DIRECTION=OTHER_MATCHMAKING_GATE_DIAGNOSTIC_REVIEW
SERVER7_100_RERUN_STARTED=NO
SERVER7_250_STARTED=NO
SERVER_CONFIGURATION_CHANGED=NO
API_IMAGE_CHANGED=YES_INSTRUMENTATION_ONLY
COMMIT=NONE
PUSH=NONE
S7_15_SUCCESS=NO
NEXT=S7-15 REVIEW
```
