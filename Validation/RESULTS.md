# Validación actual — Doble Nueve

## Fase 1 de configuración (2026-09-05)

Compilación: SUCCESS. Configuración: 80 comprobaciones. Dominio/geometría: 2.981.089 comprobaciones y mil rondas. Cien partidas completas coinciden exactamente con la referencia previa al refactor. Unity 6000.0.41f1 cargó el TextAsset con JsonUtility y completó cinco rondas hasta 200 en Play Mode automatizado de una copia aislada, con Console Errors = 0 durante esa sesión. No incluye revisión visual humana ni build móvil. Detalles y reproducción en `PHASE1.md`.

## Historial anterior

- Compilación C# independiente contra Unity 6000.0.41f1: SUCCESS; 0 errores y 2 avisos de campos serializados del controlador.
- Modelo: SUCCESS; 1.000 partidas, 1.983.660 comprobaciones.
- Cobertura observada: 16.105 jugadas por el inicio, 15.577 por el final, 13.580 inversiones, 8.885 pases, 418 victorias por mano vacía y 582 partidas trancadas.
- Geometría: hasta 40 fichas, sin solapamientos para las combinaciones de dobles y normales, con cadena centrada.
- Apertura de Unity y escena: comprobada en la fase anterior, después de resolverse la licencia.
- Play Mode, gestos reales, Console y revisión visual de esta versión doble nueve: PENDIENTES. Las pruebas automáticas no acreditan ausencia de errores en Unity Console.
- El smoke test del editor está actualizado para las reglas nuevas; aún no ejecutado en esta revisión.

## Reglas de partida y puntuación

Compilación independiente: SUCCESS. Pruebas actualizadas: 1.985.239 comprobaciones, 1.000 rondas. Incluyen mínimo individual por parejas, empate entre equipos, 30→60, bono de salida, marcador hasta 500, siguiente ronda, reinicio y protección contra cobro duplicado. Play Mode y revisión visual de esta revisión pendientes. La cantidad a cobrar por tranca sin empate aún requiere confirmación (ver RULES.md).
