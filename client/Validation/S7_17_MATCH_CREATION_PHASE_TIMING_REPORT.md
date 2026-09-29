# SERVER-7 S7-17 — Match creation phase timing

Se completó la instrumentación y la validación acotada de **8 identidades LOAD, 2 partidas y 8 entregas correlacionadas**. Ambas partidas se cancelaron por el ciclo normal tras desconectar a sus participantes. No se ejecutó una tercera partida ni una etapa de capacidad.

El exceso entre la primera y la segunda creación se concentra en **preparación/serialización local de escrituras dentro del callback**: 1.305 ms frente a 49 ms. Las lecturas acumuladas fueron 1.390 y 1.311 ms; el tramo posterior al callback fue 298 y 277 ms. No hay evidencia para llamar a los 1.305 ms un commit remoto.

**Los 10.217 ms de S7-15 no se reprodujeron.** Estas dos observaciones localizan un efecto de primer uso, pero no identifican qué inicialización lo causó ni explican retrospectivamente aquella duración. `COLD_START_CLASSIFICATION=INCONCLUSIVE` y `S7_15_REPEAT_READY=NO`. Se detuvo el experimento en dos partidas.

## Alcance y despliegue

- Base: `5a74d7917fc7fb0baa04bbdf2c8fc45de5a13f14`; imagen `cuban-domino-api:s717-c77f1035deab`; hash de fuentes aisladas `c77f1035deabae9e9134d2ffeaee59cbd407e20486b266445015a28eaa1b35cf`.
- Se reemplazó únicamente la API para incorporar los medidores. Redis, límites de 4 CPU/4 GiB, flags JVM, variables, montajes, puertos y gates quedaron iguales. No hubo reinicio adicional para fabricar un arranque frío.
- Uptime JVM inmediatamente antes de lanzar el primer grupo: **292.417 s**. La primera creación empezó aproximadamente 302,565 s después del arranque.
- Predeploy y preload: autoridad LOAD limpia; cero clientes huérfanos. La consulta de autoridad comprende raíces, asignaciones y trabajo pendiente, no solo logs de clientes.
- Gate original de matchmaking conservado: 10.000 ms. Histograma cliente (POST queue completado → handler MATCH_FOUND), p95 de 4 observaciones: **3.600 / 6.084 ms**. La segunda cifra incluye espera legítima por los otros jugadores y no es el indicador cuarto-listo→entrega.

## Correlación y límites de medición

SHA-256 del identificador de partida correlaciona reserva, creación, persistencia y cuatro entregas por seat. Los UID solo se usan internamente para emparejar marcadores de cola; no se emiten. No se registraron tokens, credenciales ni mensajes privados, y no se añadieron campos al protocolo.

M0/M1 se toman **al retornar la operación de ingreso a Redis en el servidor**, antes de retornar HTTP. Son una aproximación posterior a la aceptación atómica en Redis, no un timestamp dentro de Lua. M2 se toma al entrar al procesamiento de una reserva completa. Una carrera que perdiera marcadores invalidaría la traza: ambas tienen cuatro marcadores y están completas.

M6/M7 son preparación local de las asignaciones **dentro** de M4→M5; no son commits posteriores. M9 se captura antes de llamar al transporte y también se conserva su retorno. M10 se captura tras parsear el frame en el cliente, antes de despacharlo al handler.

Todos los intervalos internos usan reloj monotónico. Los relojes monotónicos de Windows y Linux no se restan entre sí. Para la entrega externa se usó reloj civil, con un offset SSH mediano servidor−cliente de -0.206 ms. Windows produjo timestamps repetidos: se presenta un margen conservador **±20 ms**, no una cota certificada de sincronización. Las cifras externas son orientativas y **no son latencia Cloudflare**.

| Fase (ms) | Partida 1 | Partida 2 |
|---|---:|---:|
| Primero → cuarto listo | 15.327 | 3,591.754 |
| Cuarto listo → reserva observada | 59.484 | 715.420 |
| Reserva → inicio creación | 1.221 | 0.631 |
| Creación → persistencia confirmada | 3,184.231 | 1,824.834 |
| Lecturas de perfiles + marcadores | 802.289 | 639.443 |
| Lecturas dentro de transacción | 587.518 | 671.195 |
| Todas las lecturas medidas | 1,389.807 | 1,310.638 |
| Transacción SDK completa | 2,302.751 | 1,169.029 |
| Preparación/serialización de escrituras | 1,304.984 | 49.058 |
| Inicio persistencia → primer callback | 76.598 | 169.994 |
| Último callback → resultado SDK | 298.008 | 276.680 |
| Preparación de cuatro asignaciones | 9.778 | 2.834 |
| Redis reserva + complete | 141.868 | 45.911 |
| Persistencia → publicación | 313.049 | 39.526 |
| Publicación → transporte, máximo | 114.663 | 8.743 |
| Exterior al servidor, mediana aproximada | 14.294 | 34.794 |
| Cuarto listo → transporte, máximo | 3,672.648 | 2,589.154 |
| Cuarto listo → cliente, máximo aproximado | 3,686.112 | 2,618.150 |

Los agregados se superponen: no sumar lectura total + transacción total + escrituras. `CREATE_FS_TOTAL` es perfiles/marcadores + transacción SDK: **3,105.041 / 1,808.471 ms**. Incluye trabajo local del cliente Firestore. El tramo de finalización incluye finalización SDK, commit y retorno; no es una medición aislada de Commit RPC.

| Punto, relativo a M1 (ms) | Partida 1 | Partida 2 |
|---|---:|---:|
| M0_JOIN_RESPONSE_OBSERVED | -15.327 | -3,591.754 |
| M1_LAST_JOIN_RESPONSE_OBSERVED | 0.000 | 0.000 |
| M2_RESERVATION_OBSERVED | 59.484 | 715.420 |
| M3_CREATION_START | 60.705 | 716.051 |
| M4_PERSISTENCE_START | 941.657 | 1,371.820 |
| FIRST_TRANSACTION_CALLBACK | 1,018.256 | 1,541.814 |
| M6_ASSIGNMENT_BUFFER_START | 2,934.523 | 2,261.280 |
| M7_ASSIGNMENT_BUFFER_COMPLETE | 2,944.698 | 2,264.135 |
| LAST_TRANSACTION_CALLBACK_END | 2,946.929 | 2,264.205 |
| M5_PERSISTENCE_COMPLETE | 3,244.936 | 2,540.885 |
| M8_PUBLICATION_START | 3,557.985 | 2,580.411 |

M9 por seat y M10 crudos, monotónicos y civiles, están en `Generated/S717/match-2-capture.json`; cada entrega y estimación están en `Generated/S717/analysis.json`.

## Inventario Firestore y asignaciones

Orden observado en ambas partidas:

1. Cuatro perfiles `players/...`, cada uno seguido de su marcador `developmentTestAccounts/...`: **8 lecturas independientes secuenciales**.
2. Construcción y validación del estado de dominio.
3. Una transacción: recibo de creación (1), asignaciones existentes (4), estados autoritativos previos distintos (2): **7 lecturas secuenciales**. Un callback por partida, sin retry observado.
4. Preparación de 20 escrituras: raíz (1), runtime (1), participantes (4), eventos iniciales (7), ronda (1), trabajo programado (1), asignaciones (4), recibo (1).
5. Un único commit atómico de esa transacción y espera de su resultado SDK. No hay cuatro commits de asignación, consulta adicional o batch independiente en este camino nuevo.
6. Presencia local, finalización de la reserva Redis y publicación a las conexiones.

`ASSIGNMENT_EXECUTION_MODE=BATCHED_COMMIT_WITH_SERIAL_LOCAL_BUFFERING`: cuatro `tx.set` se preparan serialmente y se confirman juntas. Los **9,778 / 2,834 ms** son esa preparación; no puede separarse un tiempo remoto exclusivo para las asignaciones dentro del commit compartido.

Perfiles y marcadores se leen por participante. Modo y reglas llegan congelados en la reserva. El catálogo tiene caché de 300 s y `resolve()` sincronizado antes de reservar; su eventual refresco no está individualizado en esta traza. Está dentro del contexto previo/pickup, no oculto dentro del commit. No se modificó ninguna caché ni se hizo prewarm.

## Primer uso y causalidad

| Candidato | Clasificación | Evidencia y límite |
|---|---|---|
| Inicialización del cliente Firestore | NO como primera inicialización durante esta partida | Singleton ya utilizado; 764 operaciones instrumentadas anteriores. |
| Canal gRPC específico establecido/caliente | NOT_MEASURABLE | Había Firestore exitoso antes, pero no se observa identidad/estado de cada subcanal físico. |
| Establecimiento TLS | NOT_MEASURABLE | No hay eventos TLS del canal Firestore. |
| Refresh de credencial | NOT_MEASURABLE para todos los intentos; 0 cambios observados | Listener de cambios registra solo contador; ausencia de callback no prueba ausencia de un intento o actividad interna no notificada. |
| DNS del canal Firestore | NOT_MEASURABLE | No se infiere de curls ni consultas DNS externas. |
| Carga/inicialización de clases | POSSIBLE contribución | +59 clases en la primera frente a +6 en la segunda; contador global, sin atribución por clase/hilo. |
| Lazy initialization Spring | NOT_MEASURABLE | No hay eventos por bean; no se puede asignar la diferencia a Spring. |
| Inicialización de caché/serializadores | POSSIBLE | 1.305→49 ms en map/stateMap/protobuf/buffer; no se separó cada serializador. |
| Creación de hilos/executor | POSSIBLE | Se muestrean hilos globales; no hay enqueue timestamp de cada tarea SDK. |
| JIT | POSSIBLE | Contador JVM +2200 / +610 ms de compilación; tiempo agregado, no pausa ni atribución causal. |
| Primer query/índice | NOT_MEASURABLE | No hay span remoto de calentamiento de índice. |
| Primera inicialización transaccional | POSSIBLE | Una transacción por creación; finalización parecida; trabajo local de escritura distinto. |
| Reconexión tras inactividad | NOT_MEASURABLE | Actividad previa observada a 258.709 / 152.931 ms, sin estado del canal para probar o descartar reconnect. |

`FIRST_MATCH_IS_FIRST_FIRESTORE_ACTIVITY=NO`. Los contadores son de los caminos instrumentados, no un inventario completo de todo RPC. Actividad anterior prueba uso previo del cliente; no permite declarar cada canal caliente.

## CPU, GC, executor, locks y Redis

En la ventana de creación, con márgenes de una muestra:

- Partida 1: CPU API pico 65.57% de 4 CPU, host 70.70%; GC +1 / 3 ms; throttling +0 µs.
- Partida 2: CPU API pico 37.53%, host 61.68%; GC +0 / 0 ms; throttling +0 µs.
- No evidencia de agotamiento de cuota o una pausa GC que explique segundos. CPU objetivo 500 ms, intervalo máximo observado cerca de 983 ms por coste del observador; GC/fases objetivo 250 ms. No se excluyen bloqueos más cortos ni competencia local no capturada. El observador también consume recursos.

El scheduler de matchmaking tiene un hilo y fixedDelay de 1 s al terminar tick; procesa reservas serialmente. El pickup fue 59,484 / 715,420 ms. No hay otro executor de creación separado en ese camino; el inicio SDK→primer callback (76,598 / 169,994 ms) incluye planificación/adquisición y no equivale a tiempo puro de cola del executor.

Puntos de serialización: Lua Redis atómico + lease de reserva; scheduler de un hilo; `GameCatalogService.resolve()` sincronizado; lecturas/buffering seriales del callback y control transaccional Firestore; sincronización por conexión al notificar y writer FIFO. No se encontró un lock global adicional de creación. Los bloqueos de la traza son locales y acotados; no se cuantificó por separado su perturbación.

Redis medido: llamada `reserve` con serialización/deserialización (**41,321 / 12,163 ms**) y `complete` (**100,547 / 33,748 ms**). Son tiempos de operación de aplicación, no solo servidor Redis. `join`, `cleanup`, `recover` y listado de colas ocurren fuera del cuerpo de creación; no se inventa una duración propia para ellos. `pairedStateObserved` modifica presencia local, no hace un viaje Redis síncrono.

## Red

Ventana antes del primer lanzamiento → captura del segundo, 42.825 s; interfaz del servidor enp0s3, contadores de todo su tráfico:

| Contador | Antes | Después | Delta |
|---|---:|---:|---:|
| rx_bytes | 7161232178 | 7162948050 | 1715872 |
| tx_bytes | 1244981215 | 1246139145 | 1157930 |
| rx_packets | 9083417 | 9087727 | 4310 |
| tx_packets | 4752007 | 4755011 | 3004 |
| rx_dropped | 77055 | 77077 | 22 |
| tx_dropped | 0 | 0 | 0 |
| rx_errors | 28 | 28 | 0 |
| tx_errors | 0 | 0 | 0 |

Los **32 drops de S7-16 fueron nuevos durante aquella observación**: 75.521→75.553, no un valor histórico acumulado. S7-17 registra otros 22 en su ventana. No se atribuyen a Firestore o Cloudflare: no hay captura por flujo ni evidencia causal de retransmisión.

## Diferencias primera menos segunda

| Componente | Delta ms |
|---|---:|
| Pickup | −655,936 |
| Firestore agregado, incluyendo buffering local | +1.296,569 |
| Preparación local de escrituras | +1.255,926 |
| Preparación local de asignaciones (subconjunto) | +6,944 |
| Publicación → transporte máximo | +105,920 |
| Exterior al servidor, mediana aproximada | −20,500 |
| Cuarto listo → cliente máximo aproximado | +1.067,962 |
| Inicio SDK → primer callback (no cola pura de executor) | −93,396 |

El tiempo puro de cola de executor y su delta no están aislados. El agregado Firestore no debe interpretarse como red: la mayor parte de su diferencia está dentro de `allBufferedWrites`.

## Recuperación y recomendación

Dos lecturas autoritativas finales y auditoría de eventos confirman 2 CANCELLED, 0 ACTIVE, 0 UNKNOWN, 0 trabajo pendiente del run y 0 fallos de consistencia. Redis se verifica por lectura del run, sin borrar claves manualmente. SERVER-6 mantiene 5 History, 5 snapshots y 3 manifiestos Replay. No quedan procesos de clientes del experimento; el observador se detiene.

El primer usuario después de que la API se declara saludable forma parte del SLO normal del producto. La arquitectura actual no tiene una barrera de warm-up explícita que justifique excluirlo. Puede reportarse primer uso por separado como dimensión diagnóstica, **sin retirarlo del SLO ni cambiar 10.000 ms**. No se propone prewarm ni optimización en esta tarea.

## Archivos y pruebas

S7-17 modifica `MatchmakingService.kt`, `OnlineMatchService.kt`, `OnlineRepository.kt`, `OnlineConfiguration.kt`, `ConnectionOutbound.kt`, el cliente `Transport.kt`; añade `MatchCreationTiming.kt` y `MatchCreationTimingTest.kt`; amplía `ConnectionOutboundTests.kt` y `GroupedCommandReadsEmulatorTests.kt`. Los cambios previos S7-15 y los archivos de usuario se conservaron.

37 pruebas unitarias de la copia aislada pasaron: 8 de la nueva traza, 21 de transporte y 8 de la instrumentación previa. 9 pruebas de emulador pasaron, incluida creación con cuatro asignaciones y reutilización del recibo. Build, verificación del JAR exacto y secret scan pasaron. Los helpers, capturas, manifests y resultados están en `Generated/S717/`.

## Salida requerida

```makefile
SERVER-7 S7-17 MATCH CREATION PHASE TIMING
==========================================
FILES_CHANGED=10_PRODUCT_AND_TEST_FILES_PLUS_DIAGNOSTIC_ARTIFACTS
INSTRUMENTATION_TESTS=37_UNIT_PASS_9_EMULATOR_PASS
API_UPTIME_BEFORE_TEST=292.417_SECONDS
MATCHMAKING_CORRELATION_READY=YES_2_MATCHES_8_DELIVERIES
MATCH_CREATION_FIRESTORE_PATH=8_PROFILE_MARKER_READS_7_TRANSACTION_READS_20_BUFFERED_WRITES_1_ATOMIC_COMMIT_PER_MATCH
MATCH_CREATION_REFERENCE_READS=4_PROFILES_4_TEST_MARKERS_FROZEN_RULES_CACHED_CATALOG
ASSIGNMENT_COUNT=4_PER_MATCH
ASSIGNMENT_EXECUTION_MODE=BATCHED_COMMIT_SERIAL_LOCAL_BUFFERING
FIRESTORE_CHANNEL_WARM_BEFORE_FIRST_MATCH=PRIOR_CLIENT_ACTIVITY_YES_PHYSICAL_CHANNEL_UNVERIFIED
GOOGLE_CREDENTIAL_REFRESH_DURING_FIRST_MATCH=NOT_MEASURABLE_ALL_ATTEMPTS_0_CHANGE_EVENTS
FIRST_MATCH_IS_FIRST_FIRESTORE_ACTIVITY=NO
FIRESTORE_IDLE_CONNECTION_REESTABLISHMENT=NOT_MEASURABLE
MATCH_CREATION_SERIALIZATION_POINTS=SCHEDULER_REDIS_LUA_LEASE_CATALOG_MONITOR_SDK_TRANSACTION_CONNECTION_WRITER

MATCH_1:
FIRST_TO_FOURTH_PLAYER_READY=15.327_MS
MATCHMAKER_PICKUP_WAIT=59.484_MS
MATCH_CREATION_PREPARE_TIME=1.221_MS
CREATE_FS_READ_TIME=1389.807_MS
CREATE_FS_TRANSACTION_TIME=2302.751_MS
CREATE_FS_WRITE_TIME=1304.984_MS
CREATE_FS_FINALIZATION_TIME=298.008_MS
CREATE_FS_TOTAL=3105.041_MS
ASSIGNMENT_TOTAL_TIME=9.778_MS
MATCH_CREATION_REDIS_TIME=141.868_MS
MATCH_FOUND_SERVER_DELIVERY_TIME=114.663_MS
MATCH_FOUND_OUTSIDE_SERVER_TIME=14.294_MS_APPROX
READY_TO_MATCH_FOUND=3686.112_MS_APPROX

MATCH_2:
FIRST_TO_FOURTH_PLAYER_READY=3591.754_MS
MATCHMAKER_PICKUP_WAIT=715.420_MS
MATCH_CREATION_PREPARE_TIME=0.631_MS
CREATE_FS_READ_TIME=1310.638_MS
CREATE_FS_TRANSACTION_TIME=1169.029_MS
CREATE_FS_WRITE_TIME=49.058_MS
CREATE_FS_FINALIZATION_TIME=276.680_MS
CREATE_FS_TOTAL=1808.471_MS
ASSIGNMENT_TOTAL_TIME=2.834_MS
MATCH_CREATION_REDIS_TIME=45.911_MS
MATCH_FOUND_SERVER_DELIVERY_TIME=8.743_MS
MATCH_FOUND_OUTSIDE_SERVER_TIME=34.794_MS_APPROX
READY_TO_MATCH_FOUND=2618.150_MS_APPROX

FIRST_VS_SECOND_DELTA=1067.962_MS_APPROX_FIRST_MINUS_SECOND
FIRST_MATCH_CPU_PRESSURE=NO_QUOTA_SATURATION_OBSERVED
FIRST_MATCH_GC_PRESSURE=NO_3_MS_OBSERVED
MATCH_CREATION_EXECUTOR_QUEUE_WAIT=NOT_ISOLATED_SDK_CALLBACK_START_76.598_AND_169.994_MS
RX_DROPS_BASELINE=77055
RX_DROPS_AFTER=77077
RX_DROPS_DELTA=22
PRIMARY_MATCH_CREATION_LATENCY_COMPONENT=READS_PLUS_LOCAL_WRITE_PREPARATION_FIRST_SECOND_EXCESS_IS_WRITE_PREPARATION
COLD_START_CLASSIFICATION=INCONCLUSIVE
CONFIDENCE=HIGH_PHASE_LOCALIZATION_LOW_SPECIFIC_COLD_START_CAUSE
MATCHMAKING_SLO_SCOPE_RECOMMENDATION=FIRST_USE_INCLUDED_WHEN_API_READY_NO_THRESHOLD_CHANGE
S7_15_REPEAT_READY=NO
POST_TEST_ACTIVE_LOAD_MATCHES=0
POST_TEST_PENDING_WORK=0
UNCLASSIFIED_LOAD_MATCHES=0
ORPHAN_SWARM_PROCESSES=0
SERVER6_REGRESSION=PASS
20_USER_CAPACITY_STAGE_STARTED=NO
40_USER_STAGE_STARTED=NO
60_USER_STAGE_STARTED=NO
100_USER_STAGE_STARTED=NO
250_USER_STAGE_STARTED=NO
SERVER_CONFIGURATION_CHANGED=NO_OPERATIONAL_SETTINGS_API_IMAGE_CHANGED_FOR_INSTRUMENTATION
COMMIT=NONE
PUSH=NONE
S7_17_SUCCESS=YES_INSTRUMENTATION_AND_TWO_MATCH_VALIDATION
NEXT=S7-17 REVIEW
```
