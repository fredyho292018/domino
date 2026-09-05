# Configuración local — Fase 1

Fuente de referencia: `31679fe83af47db20c65aae9c73d4a5d3b9a6435` (working tree limpio al comenzar).

## Contrato y alcance

`Assets/_Domino/Config/double-nine-partners-v1.json` es el único preset incluido. Id `double-nine-partners`, versión 1, schema 1, ruleset 1.0. El TextAsset está referenciado por GUID desde DominoClient.unity; no se requiere acceso a archivos del dispositivo. Se utiliza el módulo nativo `com.unity.modules.jsonserialize`, sin librerías externas.

Los equipos se expresan como objetos `{"members":[0,2]}` para soportar JsonUtility sin arrays anidados no soportados. DTO, validador y snapshot no dependen de Unity. Los campos ausentes se rechazan mediante valores centinela y comprobaciones de políticas. Las cadenas desconocidas no activan políticas futuras.

La configuración validada se copia profundamente: órdenes, asignaciones y políticas no pueden modificarse a través del DTO ni de las colecciones públicas. MatchState expone la referencia a su snapshot. El controlador carga una vez al arrancar, antes de crear mesa o repartir. No cambia reglas en reinicios ni rondas de esa instancia.

Límites del cliente: cuatro asientos, dos parejas de dos miembros o el modo individual preexistente, máximo diez fichas por mano y puntos de 1 a 9 para el set. Double-6 se prueba técnicamente; no se añade otro preset. Las políticas futuras de salida, robo y apertura se rechazan.

## Regresión

Antes de modificar el motor se ejecutaron 2.981.088 comprobaciones sobre mil rondas y geometría. Se capturó un SHA-256 de cien partidas completas, incluidas manos en orden, reserva, eventos, resultados, rondas y puntuaciones:

`EFADD088F8AB7A9E11CC109A322267C5B7800F7F85BB2D64B6F8F30F95998D52`

El motor refactorizado debe producir exactamente ese digest. Se excluye el identificador numérico de ficha porque ahora usa enumeración triangular, conservando igualdad sin orientación. Los lados reales sí forman parte de la referencia.

`RunTests.ps1` usa el mismo JSON incluido en el build. Comprueba datos inválidos, omisiones, rangos, políticas, equipos/órdenes repetidos o incompletos, insuficiencia de fichas, copias inmutables y que modificaciones válidas del DTO realmente llegan al motor. Las pruebas de variantes son fixtures, no modalidades publicadas.

## Ejecución

Desde la raíz: `./Validation/Compile.ps1` y `./Validation/RunTests.ps1`.

Para Play Mode automatizado, ejecutar Unity con `-batchmode -nographics -projectPath <copia aislada del proyecto> -executeMethod Domino.Editor.Phase1Validation.Run -logFile <log>`, sin `-quit`. El ejecutor termina Unity al obtener resultado. Debe utilizarse en una copia de pruebas, nunca para cerrar una sesión de trabajo abierta. El resultado se escribe como `phase1-unity-result.txt` en la carpeta padre de esa copia.

El ejecutor valida el serializador real de Unity, referencias de escena y el smoke test existente ampliado a varias rondas/meta. Acelera el tiempo de la prueba a ×4 sin modificar las duraciones del proyecto. Una sesión sin gráficos acredita ejecución y geometría enviada al Canvas, no revisión humana de aspecto ni builds Android/iOS.

## Reglas que siguen provisionales

Cobro por tranca: sumar las otras tres manos sin bono. Salida: asiento cero en todas las rondas. Empates consecutivos: conservar ×2, sin acumulación. Se mantienen las opciones preexistentes, sin decidir reglas nuevas.

## Resultado de esta revisión

- Compilación independiente contra Unity 6000.0.41f1: SUCCESS. Tres avisos esperables por referencias privadas serializadas que asigna la escena.
- Dominio/geometría: 2.981.089 comprobaciones, mil rondas; digest de cien partidas completas idéntico al original.
- Configuración: 80 comprobaciones, incluidas copias profundas, variantes existentes y errores de validación.
- Serializador real de Unity: SUCCESS, incluido rechazo de JSON ausente/malformado, esquema desconocido y campos numéricos omitidos.
- Play Mode batch en copia aislada: SUCCESS, cinco rondas hasta meta 200, con siguiente ronda, marcador conservado y mismo snapshot.
- Console errors: 0 durante esa sesión automatizada. Evidencia local: `Generated/phase1-unity-full-match.log` y `Generated/phase1-unity-result.txt` (ignorados en Git).
- No se efectuó revisión visual humana ni build Android/iOS; no se declara validación manual. El diff de BoardView solo conecta configuración, orden/cantidad de reparto y tamaño del set del lavado.

## Trabajo futuro

SessionSetup deberá separar asientos humanos/bots y nombres. La vista permanece diseñada para cuatro jugadores. No hay backend, descarga, caché remota ni persistencia de partidas en esta fase.
