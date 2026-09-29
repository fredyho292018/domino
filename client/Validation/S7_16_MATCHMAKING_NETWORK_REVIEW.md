# SERVER-7 / S7-16 — Matchmaking y referencia de red

**Causa primaria identificada: MATCH_CREATION, con confianza alta para ese intervalo agregado.** La primera de cinco creaciones tardó **10.217 ms** desde reserva hasta `MATCH_CREATED`. Ya estaban registrados sus cuatro ingresos. Las siguientes tardaron 1.878, 3.242, 2.782 y 1.299 ms. La evidencia no separa todavía SDK/Firestore, preparación local, GC o planificación dentro de la primera creación; no se atribuyen esos 10,217 s al commit remoto.

**S7_15_REPEAT_READY=NO.** Una sola medición requerida antes de repetir: una traza acotada y correlacionada de matchmaking que identifique ingreso de los cuatro jugadores, subfases de creación y entrega. Se propone; no se implementó ni desplegó. No se cambia el gate de 10.000 ms. S7-16 completa esta revisión, con las incertidumbres señaladas, sin nueva carga ni cambios de sistema.

## 1. Qué mide el gate actual

`SimulatedClient.join()` pone `queuedAt=0`, incrementa `queueJoins`, espera `POST matchmaking/queue`, y solo tras HTTP 200 y parseo guarda `queuedAt=now()`. `enter()` registra la diferencia al procesar por primera vez MATCHED/MATCH_FOUND, **antes** de pedir el snapshot.

Incluye espera por otros jugadores, pickup, creación, finalización Redis, entrega y procesamiento del mensaje. Excluye token refresh, bootstrap, cambio de nombre, catálogo, conexión/autenticación WS, GET inicial de recuperación y duración de POST de entrada. No es latencia pura de backend. Un resultado MATCHED inmediato puede registrar casi cero tras el POST; por eso tampoco representa todo el tiempo desde que el jugador pulsa buscar.

Para conservar comparabilidad, mantener esta serie y su gate de 10.000 ms. Como producto, documentar también solicitud de búsqueda→resultado y aceptación servidor→resultado. La traza propuesta debe permitir separar espera por cuarto jugador de procesamiento una vez listo el grupo. No rebautizar el gate ni elevarlo para ocultar el fallo.

## 2. Línea temporal de S7-15

Fuente: extracción de solo lectura de logs del contenedor, limitada a nombres de evento, timestamps y hashes, más `registry-confirm.json` de S7-15. No UID ni tokens. Los tiempos de logs son de pared del mismo servidor, con precisión de milisegundos; no sustituyen futuros intervalos monotónicos por fase.

- Primer ingreso registrado: **17:32:45.703Z**; cuarto: **17:32:46.114Z**, separados por **411 ms**.
- Reserva de la primera partida: **17:32:46.542Z**: **428 ms** después del cuarto log de ingreso.
- `MATCH_CREATED`: **17:32:56.759Z**: **10.217 ms** después de reserva.
- `MATCH_FOUND` servidor: **17:32:56.968Z**: otros **209 ms**. Este log confirma finalización Redis y encolado/notificación; no confirma recepción física del cliente.
- Primer ingreso→aviso servidor: **11.265 ms**, consistente con el gate cliente p95 **11.259 ms**. No es una pareja exacta de muestras cliente/servidor.
- Cuarto ingreso→aviso servidor: **10.854 ms** aproximadamente. Incluso descontando espera por el cuarto jugador, el primer grupo ya supera diez segundos en este proxy servidor.

No se puede calcular un p95 cliente exacto posterior al cuarto jugador: los logs de JOIN no contienen correlación por jugador y el histograma de matchmaking no conserva esa asociación. El dato 10.854 ms es del primer grupo, no un p95 de veinte clientes. La primera reserva sigue a exactamente cuatro logs JOIN; la asociación es una inferencia fuerte del orden y la cola FIFO, no una traza individual instrumentada.

| Partida por orden | Reserva→creada ms | Reserva→timestamp de dominio ms | Timestamp de dominio→creada ms | Creada→aviso servidor ms |
|---|---:|---:|---:|---:|
| 1 | 10217 | 1419.493 | 8797.507 | 209 |
| 2 | 1878 | 808.978 | 1069.022 | 11 |
| 3 | 3242 | 1178.601 | 2063.399 | 6 |
| 4 | 2782 | 1191.064 | 1590.936 | 63 |
| 5 | 1299 | 631.293 | 667.707 | 8 |

El timestamp `startedAt` se toma en `createPaired` **después** de resolver perfiles y **antes** de motor/persistencia. En la primera partida deja unos 1.419 ms anteriores y 8.798 ms posteriores. Este último tramo agrupa motor, validación, serialización, transacción y observación local de presencia; no es una medición del Commit RPC.

Los veinte JOIN registrados se extienden **21.598 ms**, de 17:32:45.703Z a 17:33:07.301Z. Esto describe incorporación escalonada de toda la población, no espera del primer grupo por jugadores.

## 3. Cadencia, concurrencia y arranque

`MatchmakingWorker.tick()` tiene `fixedDelay=1000`, scheduler dedicado con un hilo. El delay comienza tras terminar el tick. Dentro de cada modo activo reserva y procesa hasta ocho grupos **en serie**; no hay temporizador de lote adicional. `online.createPaired` espera sincrónicamente perfiles y transacción. Un grupo lento bloquea pickup de los posteriores en esa instancia.

No hay una pausa fija de 5, 10 o 15 s en este camino. En reposo, pickup puede ocurrir entre casi cero y aproximadamente un segundo, más trabajo del tick y planificación. Un tick ocupado puede superar ese rango. Las reservas segunda y tercera siguen al aviso anterior por unos 4 y 26 ms: tampoco pagan un segundo por partida.

El Swarm usa WS para MATCH_FOUND; solo hace GET de recuperación antes de entrar y tras reconexión, sin poll periódico de matchmaking. El timeout AUTH de 15 s, el rate-limit Redis de 10 s, la lease de 30 s y TTL de cola de 120 s son límites/mecanismos distintos, no esperas obligatorias de búsqueda.

Arranque configurado: jitter 0–1.500 ms. El estado `AUTHENTICATING` abarca refresh Firebase **y** bootstrap, nombre y catálogo. En la muestra del coordinador hay 4 conexiones a ~14 s, 6 a ~20,6 s, 10 a ~29,3 s y 20 a ~34,6 s; son snapshots, no tiempos exactos por cliente. Existe skew de incorporación mayor que el jitter configurado. No puede atribuirse solo a Firebase ni cuantificarse su contribución exacta: las duraciones REST están fusionadas y no hay fases por identidad. El arranque anterior al POST queda fuera del cronómetro propio, pero puede prolongar la espera de otros jugadores.

## 4. Inventario Firestore y Redis

Para una **nueva partida PARTNERS de cuatro jugadores**, en el camino normal, sin contar reintentos internos:

- Ocho lecturas de perfil secuenciales: `players` y marcador TEST por cada uno de los cuatro jugadores.
- Una llamada de transacción de creación. Lee recibo de creación (1), asignaciones (4) y runtime de cada asignación anterior distinta: K entre 0 y 4. Total de creación: **13+K documentos leídos**, combinando perfiles y transacción; no es contador de facturación medido. Los recorridos de duplicado/recuperación son diferentes.
- Veinte escrituras documentales en la transacción: raíz (1), runtime (1), participantes (4), eventos iniciales (7: MatchStarted, RoundStarted, cuatro HandDealt, TurnStarted), round (1), turnWork (1), asignaciones (4), recibo (1). Para cinco éxitos serían 100 operaciones del camino estático, sin afirmar número de RPC o intentos realmente facturados.
- Entrada/GET de recuperación consulta `onlineTurnWork` por UID y puede leer runtimes activos. Catálogo se lee cuando expira su caché de 300 s; no en cada llamada obligatoriamente. No hay medición de reintentos de creación en S7-15: el cero de reintentos ACK pertenece a comandos de gameplay y no debe trasladarse a esta transacción.

Redis usa scripts Lua para presencia, FIFO, admisión, reserva atómica de cuatro jugadores, lease y finalización. No es la autoridad del estado de partida. El timer interno `domino.matchmaking.wait` registra reserva menos **promedio** de joinedAt, y excluye creación/entrega; no es el histograma del gate Swarm. No hay temporización de comandos Redis suficiente para aislar su espera. El tramo creada→aviso abarca Redis más proyecciones/encolado (6–209 ms), no solo Redis. No hay evidencia que sitúe Redis como causa primaria del primer tramo de 10.217 ms.

## 5. Recursos durante incorporación y creación

Ventana: lanzamiento del grupo hasta último aviso servidor, 17 muestras válidas de S7-15. API CPU de cuota media **51.25%**, pico **76.33%**; host media **52.36%**, pico **74.59%**. Redis CPU media **1.12%**, pico **3.66%**, normalizada a su cuota efectiva de ocho CPU. RAM disponible host mínima 10310926336 bytes; heap máximo 243134120 bytes.

Cuatro GC suman **422 ms**; el contador cgroup de throttling aumenta **1365198 µs**. Hay ráfaga de CPU/GC y throttling observables, pero no saturación sostenida al umbral de seguridad ni presión de memoria. El contador de throttling no es tiempo de pausa del comando y no se suma a GC ni a latencia. Estos datos no prueban que CPU/GC explique 8,798 s. Sin A/B ni perfiles de esa primera creación, su contribución queda sin aislar. Redis no registra OOM, evicciones ni conexiones rechazadas en las muestras.

## 6. Referencia de red actualizada y medición desde dominoserver

Referencia proporcionada por el usuario: **364,7 Mbps descarga / 245,8 Mbps subida / 3 ms Dallas**. Reemplaza como referencia actual 221,8 / 65,7 Mbps / 4 ms. Ambas son observaciones de Speedtest del navegador; la anterior pudo solaparse con actividad. No se clasifica capacidad del servidor ni del enlace doméstico desde ellas.

Interfaz obtenida con ruta efectiva: **enp0s3**, velocidad declarada **1.000 Mbps**, enlace virtual. No demuestra velocidad WAN ni rendimiento del Wi-Fi del host. Intervalo inactivo de **62.511 s**, 60 muestras; sin Swarm ni speedtest, con actividad de fondo normal del servidor. Media ponderada por tiempo: RX **0.254880 Mbps**, TX **0.029888 Mbps**; pico por muestra RX **0.819637 Mbps**, TX **0.083730 Mbps**. No son máximos instantáneos ni tráfico total de todos los dispositivos del hogar.

| Contador | Inicio | Incremento en intervalo |
|---|---:|---:|
| RX bytes | 6.857.871.890 | 1.991.598 |
| TX bytes | 1.232.059.312 | 233.544 |
| RX paquetes | 8.789.065 | 3.072 |
| TX paquetes | 4.662.449 | 1.583 |
| RX errores | 28 | 0 |
| TX errores | 0 | 0 |
| RX descartes | 75.521 | 32 |
| TX descartes | 0 | 0 |

Una consulta posterior de `ip`/`ethtool` ubica los 28 errores acumulados en longitud RX; no hay CRC, missed, no-buffer o fallos de asignación reportados. Los descartes RX continuaron aumentando. No existe atribución por flujo que los conecte con Firestore/Cloudflare; tampoco se descartan por ser bajos. No se convierten en porcentaje de pérdida TCP ni se infiere saturación.

S7-15 registró CPU/JVM/Redis, **no bytes/paquetes de interfaz ni WAN**. Por tanto **NETWORK_BANDWIDTH_BOTTLENECK=INCONCLUSIVE** para aquel run. El bajo uso inactivo actual no refuta una congestión histórica ni la de otros equipos.

## 7. DNS, TCP, TLS y caminos separados

Firestore usa Firebase Admin→FirestoreClient sin endpoint personalizado en el código; el SDK instalado resuelve su default a `firestore.googleapis.com:443`. `oauth2.googleapis.com/token` es infraestructura de credenciales relevante, verificada en el SDK; el GET de prueba no solicitó tokens. No se leyó ninguna credencial ni se hicieron RPC o escrituras Firestore.

DNS: veinte `getaddrinfo` por host, con caché normal del resolver, no veinte consultas autoritativas frías. Conexión HTTPS: procesos curl nuevos, sin credenciales, timeout conexión 5 s / total 10 s. TCP=`time_connect-time_namelookup`; TLS=`time_appconnect-time_connect`. Incluyen implementación del cliente/OS; no son RTT puro. GET a `/` en hosts Google devuelve 404 esperado: mide infraestructura HTTPS y su respuesta, **no** operación Firestore, commit ni región efectiva de un RPC.

| Medición | n | p50 ms | p95 ms | Máximo ms |
|---|---:|---:|---:|---:|
| DNS Firestore | 20 | 0.921 | 12.624 | 33.653 |
| DNS OAuth2 | 20 | 0.595 | 0.975 | 4.005 |
| TCP Firestore | 20 | 24.166 | 26.445 | 37.769 |
| TLS Firestore | 20 | 33.395 | 43.471 | 47.132 |
| HTTPS total Firestore (404) | 20 | 133.877 | 143.676 | 156.784 |
| HTTPS total OAuth2 (404) | 10 | 97.403 | 106.260 | 106.260 |
| Salud localhost servidor (200) | 10 | 8.018 | 11.594 | 11.594 |
| Salud pública desde servidor (200) | 10 | 79.422 | 98.415 | 98.415 |
| Salud pública desde Windows (200) | 10 | 85.327 | 103.814 | 103.814 |

No hubo fallos de estos probes. Los percentiles de red son nearest-rank sobre duraciones de alta resolución, reportadas a 0,001 ms; el gate original de matchmaking conserva sus buckets de 1 ms. Los cuatro caminos se mantienen separados: gateway/enlace, servidor→Google, servidor→Cloudflare→servidor (hairpin público), Windows→Cloudflare→servidor. Las consultas de salud son HTTP, no WS autenticado ni matchmaking. No se restan sus p95 para inventar una latencia del túnel.

Diez ICMP por destino: gateway 0/10 perdidos, RTT min/media/máximo 1,504/2,263/3,733 ms y mdev 0,691 ms; 1.1.1.1 0/10, 8,588/41,906/60,419 ms y mdev 22,039 ms; host Firestore 0/10, 21,565/24,114/25,415 ms y mdev 1,150 ms. Jitter aquí significa dispersión mdev de RTT ICMP, no jitter de aplicación. La variabilidad de 1.1.1.1 no se puede localizar al enlace doméstico con diez paquetes. Cero pérdidas de esos probes no significa cero pérdida universal.

Ubicación Firestore: **us-central1**, reutilizada de `S708/database-location.txt` (metadata administrativa autoritativa de TEST). El servidor está en una VM doméstica; Dallas es el destino del Speedtest proporcionado, no prueba de ubicación física del servidor. No se cambió región.

Clasificaciones causales para el incidente S7-15: HOME_LINK_LATENCY_ISSUE=INCONCLUSIVE; BACKEND_TO_GOOGLE_LATENCY_CONTRIBUTION=INCONCLUSIVE; CLIENT_CLOUDFLARE_PATH_CONTRIBUTION=INCONCLUSIVE. La referencia actual muestra tiempos de decenas/centenas de ms y ninguna reproducción del retraso de diez segundos, pero se tomó después y sin carga; no permite atribuir causalmente el incidente.

## 8. Cierre y única acción requerida

**MATCH_CREATION** es la explicación primaria al nivel demostrado. La incorporación escalonada afecta otras esperas y la creación en serie propaga retrasos; el primer grupo tenía sus cuatro ingresos en 411 ms y sufrió 10.217 ms dentro de creación. No se elige FIRESTORE puro, red ni CPU como subcausa sin evidencia independiente.

Una sola acción propuesta: **instrumentación acotada de una línea temporal correlacionada de matchmaking**, desde JOIN aceptado por jugador/grupo hasta notificación/recepción, separando perfiles, motor/serialización, inicio/callback/lecturas/finalización de creación y finalización Redis. Hashes seguros, reloj monotónico, sin cambiar concurrencia, lecturas, semántica o gate. Resolvería el tramo de 8,798 s y la separación exacta del cuarto jugador. No se implementó en esta revisión; no se repitió S7-15.

Comprobación final de solo lectura: mismos contenedores API/Redis que S7-15, saludables, 4 CPU/4 GiB, gates cerrados, cero reinicios/OOM. Sin cambios CPU, RAM, JVM, Firestore, Redis, Cloudflare o DNS. Solo se crearon archivos de diagnóstico/informe y se hicieron lecturas/probes acotados. Sin commit ni push.

Fuentes de código: `SimulatedClient.kt:90–102`, `Transport.kt:23–38`, `MatchmakingConfiguration.kt:19–22`, `MatchmakingService.kt:44–77`, `RedisMatchmakingStore.kt`, `OnlineMatchService.kt:18–33`, `OnlineConfiguration.kt:64–75`, `OnlineRepository.kt:56–88`, `OnlineEngine.kt:14–25,146–160,193–198`. Evidencia local: `Generated/S716/analysis.json`, `network.jsonl`, `match-events.jsonl`, `client-network.json`, `interface-detail.txt`, `final-server.json`; evidencia previa S715 permanece intacta.

## Salida requerida

```text
SERVER-7 S7-16 MATCHMAKING + NETWORK REVIEW
==========================================
MATCHMAKING_P95_OBSERVED=11259_MS
MATCHMAKING_GATE=10000_MS
MATCHMAKING_MEASUREMENT_BOUNDARY=CLIENT_QUEUE_POST_200_PARSED_TO_FIRST_ENTER_MATCH_BEFORE_SNAPSHOT
MATCHMAKING_WORKER_INTERVAL=1000_MS_FIXED_DELAY_AFTER_TICK_COMPLETION
MATCHMAKING_POLL_INTERVAL=NO_PERIODIC_SWARM_QUEUE_POLL_WS_PUSH_AFTER_INITIAL_GET
MATCHMAKING_BATCH_INTERVAL=NO_SEPARATE_TIMER_UP_TO_8_SERIAL_RESERVATIONS_PER_MODE_PER_TICK
MATCHMAKING_LATENCY_FLOOR_FROM_SCHEDULING=NO_FIXED_5_10_15S_FLOOR_IDLE_PICKUP_0_TO_ABOUT_1S_PLUS_TICK_WORK
FIRST_TO_LAST_QUEUE_JOIN_SPAN=21598_MS_SERVER_JOIN_LOGS
AUTH_DELAY_CONTRIBUTION=STARTUP_SKEW_OBSERVED_FIREBASE_ONLY_NOT_ISOLATED
MATCHMAKING_P95_AFTER_FOURTH_PLAYER_READY=NOT_IDENTIFIABLE_PER_CLIENT_FIRST_MATCH_SERVER_PROXY_10854_MS
MATCHMAKING_FIRESTORE_READS=8_PROFILE_PLUS_5_PLUS_K_TX_DOC_READS_PER_NEW_MATCH_K_0_TO_4_PLUS_QUEUE_QUERY_CATALOG_CACHE_MISS
MATCHMAKING_FIRESTORE_WRITES=20_DOCUMENT_WRITES_PER_NEW_PARTNERS_MATCH_STATIC_PATH
MATCHMAKING_FIRESTORE_TRANSACTIONS=1_CREATION_CALL_PER_MATCH_5_CALLS_INTERNAL_ATTEMPTS_UNMEASURED
MATCHMAKING_REDIS_ROLE=ATOMIC_FIFO_PRESENCE_RESERVATION_LEASE_COMPLETION_NOT_GAME_STATE_AUTHORITY
MATCH_CREATION_CONCURRENCY=SERIAL_ONE_WORKER_PER_API_INSTANCE
MATCHMAKING_RESOURCE_PRESSURE_EVIDENCE=CPU_BURST_GC_AND_THROTTLING_OBSERVED_NO_SUSTAINED_QUOTA_OR_MEMORY_STOP
CURRENT_HOME_DOWNLOAD_REFERENCE=364.7_Mbps
CURRENT_HOME_UPLOAD_REFERENCE=245.8_Mbps
CURRENT_DALLAS_SPEEDTEST_LATENCY_REFERENCE=3_ms
SERVER_NETWORK_INTERFACE=enp0s3
SERVER_LINK_SPEED=1000_Mbps_VIRTUAL_NOT_WAN_CAPACITY
SERVER_RX_ERRORS=28_CUMULATIVE_DELTA_0
SERVER_TX_ERRORS=0_CUMULATIVE_DELTA_0
SERVER_RX_DROPS=75521_AT_START_DELTA_32_IN_IDLE
SERVER_TX_DROPS=0_CUMULATIVE_DELTA_0
IDLE_RX_MBIT_AVG=0.254880
IDLE_TX_MBIT_AVG=0.029888
IDLE_RX_MBIT_PEAK=0.819637
IDLE_TX_MBIT_PEAK=0.083730
DNS_SAMPLE_COUNT=20_FIRESTORE_PLUS_20_OAUTH2
DNS_P50=0.921_MS
DNS_P95=12.624_MS
DNS_MAX=33.653_MS
TCP_CONNECT_P50=24.166_MS
TCP_CONNECT_P95=26.445_MS
TCP_CONNECT_MAX=37.769_MS
TLS_HANDSHAKE_P50=33.395_MS
TLS_HANDSHAKE_P95=43.471_MS
TLS_HANDSHAKE_MAX=47.132_MS
GOOGLE_NETWORK_MEASUREMENT_METHOD=20_FRESH_UNAUTHENTICATED_HTTPS_GET_ROOT_FIRESTORE_HOST_HTTP_404_TOTAL_CURL_NOT_RPC
GOOGLE_NETWORK_P50=133.877_MS
GOOGLE_NETWORK_P95=143.676_MS
GOOGLE_NETWORK_MAX=156.784_MS
PACKET_LOSS=0_OF_10_ICMP_EACH_GATEWAY_1.1.1.1_GOOGLE_ONLY
NETWORK_JITTER=ICMP_MDEV_GATEWAY_0.691_MS_1.1.1.1_22.039_MS_GOOGLE_1.150_MS
FIRESTORE_DATABASE_LOCATION=us-central1_REUSED_S7_08_AUTHORITATIVE_METADATA
NETWORK_BANDWIDTH_BOTTLENECK=INCONCLUSIVE
HOME_LINK_LATENCY_ISSUE=INCONCLUSIVE
BACKEND_TO_GOOGLE_LATENCY_CONTRIBUTION=INCONCLUSIVE_FOR_S7_15_FIRST_CREATION
CLIENT_CLOUDFLARE_PATH_CONTRIBUTION=INCONCLUSIVE_FOR_S7_15_MATCHMAKING
PRIMARY_MATCHMAKING_LATENCY_CAUSE=MATCH_CREATION
CONFIDENCE=HIGH_FOR_AGGREGATE_CREATION_PATH_SUBCOMPONENT_UNRESOLVED
MATCHMAKING_GATE_MEASUREMENT_RECOMMENDATION=PRESERVE_10000_MS_AND_EXISTING_SERIES_ADD_CORRELATED_QUEUE_READY_CREATION_DELIVERY_TIMELINE
S7_15_REPEAT_READY=NO
ONE_REQUIRED_MEASUREMENT_CHANGE=BOUNDED_CORRELATED_MATCHMAKING_PHASE_TIMELINE
NEW_LOAD_EXECUTED=NO
CPU_CHANGED=NO
RAM_CHANGED=NO
JVM_CHANGED=NO
FIRESTORE_CHANGED=NO
REDIS_CHANGED=NO
CLOUDFLARE_CHANGED=NO
DNS_CHANGED=NO
SERVER_CONFIGURATION_CHANGED=NO
COMMIT=NONE
PUSH=NONE
S7_16_SUCCESS=YES
NEXT=S7-16 REVIEW
```
