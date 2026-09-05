# DominoGame

Proyecto local Unity 6 LTS **6000.0.41f1**. Abrir la carpeta `DominoGame` y la escena `Assets/_Domino/Scenes/DominoClient.unity`, luego pulsar Play. Tras cambios de código durante una partida, detener Play y volver a iniciarlo.

## Reglas actuales

- Set doble nueve: 55 fichas únicas, de 0–0 a 9–9.
- Se mezclan y reparten 10 fichas a cada jugador. Las 15 restantes se apartan; no se roba.
- Fredy comienza. Orden: Fredy → John → Maria → Alex → Fredy.
- La primera ficha es libre. Las siguientes deben coincidir con uno de los dos extremos abiertos.
- Las fichas se orientan automáticamente para unir números iguales. Los dobles quedan atravesados respecto a la cadena.
- Solo se pasa cuando no existe ninguna jugada válida. Los pases son automáticos, también para Fredy.
- La ronda termina cuando alguien vacía su mano o después de cuatro pases consecutivos. Las manos se revelan y se calcula el resultado; el marcador acumula puntos hasta 200 (configurable). Véase RULES.md para reglas confirmadas y detalles de cobro pendientes.

## Controles

- Tocar una ficha para seleccionar; tocar de nuevo para deseleccionar.
- JUGAR o tocar la mesa coloca la seleccionada en un extremo válido; si ambos sirven, se prioriza el final de la cadena.
- Arrastrar y soltar dentro de la mesa: se prioriza el extremo más cercano. Si solo sirve el otro, se coloca allí automáticamente.
- La mesa se ilumina verde con una ficha válida y rojiza con una inválida. Las jugadas inválidas vuelven a la mano y muestran los números necesarios.
- Soltar fuera de la mesa cancela el arrastre.
- Reiniciar cancela animaciones y comienza con otro reparto.
- Los oponentes muestran reversos y revelan su ficha al llegar a la mesa.

## Estructura

`Core`: datos y eventos independientes de Unity. `Game/ClientGame`: reglas, orientación lógica de la cadena, manos, reserva, turnos y final de ronda. `Client/DominoClientController`: intenciones del usuario y simulación de oponentes mediante una cola de eventos. `UI`: geometría, prefabs, arrastre, animación y área segura.

Los eventos locales incluyen inicio, ficha jugada con índice de inserción y orientación, cambio de turno, pase y final. No hay backend, red, autenticación ni servicios externos.

Los prefabs `DominoTile` y `Player` generan sus gráficos durante Play. El set incluye puntos correctos de 0 a 9. La composición se ajusta a Screen.safeArea sin deformarse. La cadena admite hasta las 40 fichas repartidas.

## Validación

Desde la raíz, PowerShell:

```powershell
./Validation/Compile.ps1
./Validation/RunTests.ps1
```

La compilación independiente usa las bibliotecas reales de Unity, sin abrir el editor. Las pruebas ejecutan 1.000 partidas, comprueban combinaciones, 10 fichas por mano, reserva de 15, extremos, orientación, intentos inválidos sin mutación, pases, turnos, finalización y geometría. No sustituyen la comprobación visual.

En Play, `Domino > Run interactive smoke test (Play Mode)` verifica reparto, gráficos, arrastre, reinicios y una partida con reglas. Puede tardar hasta tres minutos. Después comprobar manualmente mouse/touch y las proporciones 16:9, 18:9, 19.5:9 y 20:9, especialmente las diez fichas y los extremos en curvas. `Domino > Capture Game View (Play Mode)` guarda capturas locales.

Unity abrió correctamente tras resolverse el problema inicial de licencia. La versión actual de doble nueve aún necesita revisión visual dentro del editor. No se afirma Console Errors = 0 sin una sesión comprobada.

## Móvil y Git

Landscape, Android ARM64/IL2CPP preparado y soporte iOS. Android SDK/NDK/OpenJDK están instalados; no hay builds ni firma verificados. iOS final requiere macOS/Xcode.

Assets con sus metas, Packages y ProjectSettings son versionables; Library, temporales, builds y resultados generados están ignorados. No se ha hecho push.

## Partidas y reglas de casa

La configuración está en DominoClientController (modo, meta, bono, tranca, fuente de puntos y multiplicador). Siguiente conserva puntuaciones; Reiniciar comienza una partida nueva. RULES.md es la especificación de reglas y características, incluidos los puntos todavía por confirmar.


