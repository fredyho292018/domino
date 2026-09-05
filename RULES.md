# Reglas y características — Dominó doble nueve

## Reglas de mesa

| Regla | Definición |
|---|---|
| Set | Doble nueve, 55 fichas únicas, valores de 0 a 9 |
| Reparto | Diez fichas para cada uno de cuatro jugadores |
| Reserva | Quince fichas apartadas, sin robo (supuesto inicial de la client) |
| Turnos | Fredy → John → Maria → Alex → Fredy |
| Parejas | Fredy–Maria (A) contra Alex–John (B) |
| Jugada | Coincidencia de números con cualquiera de los extremos abiertos |
| Dobles | Se presentan atravesados respecto a la dirección de la cadena |
| Pase | Solo si el jugador no tiene ninguna ficha válida; automático en la client |
| Tranca | Cuatro pases consecutivos, sin una jugada intermedia |
| Apertura | Fredy comienza cada ronda por ahora; política de salida pendiente de definir |

## Final de ronda y puntos

### Salida: un jugador coloca su última ficha

Gana ese jugador o su equipo. Se añade un bono de diez puntos. La configuración inicial cuenta los puntos de las manos de los otros tres jugadores, incluido el compañero, y después añade el bono. Esta es la interpretación de la confirmación recibida para una salida sin tranca.

Ejemplo configurado: Fredy sale y quedan Alex=20, Maria=30, John=40. El equipo A recibe 20+30+40+10=100 puntos en una ronda normal.

### Tranca

Se giran las fichas restantes y se muestra la suma individual de los dos lados de cada ficha. Gana el jugador con menos puntos individuales. En parejas gana su equipo: **no se suman las dos manos del equipo para decidir quién gana**. Esto fue confirmado por el usuario.

El bono de salida no se aplica a la tranca. La cantidad a acreditar al ganador por tranca todavía requiere confirmación. El cálculo provisional usa la misma fuente de puntos de las otras manos, sin los diez puntos adicionales.

Si dos jugadores del mismo equipo empatan en el mínimo, el equipo gana en la implementación actual. Si el mínimo queda empatado entre bandos contrarios, la ronda no otorga puntos y la siguiente vale doble. La interpretación de empate interno también puede revisarse como regla de casa.

### Empate y multiplicador

Configuración actual: después de una tranca empatada, la siguiente ronda vale ×2. Ejemplo confirmado: 30 puntos se convierten en 60. Si se repite el empate se mantiene ×2 por ahora; la variante acumulativa ×4/×8 existe, pero no está activada ni confirmada.

El multiplicador afecta al total de la ronda, incluido el bono de salida. Al resolverse una ronda con ganador vuelve a ×1. No se trasladan puntos de las manos de la ronda empatada: solo se conserva el multiplicador.

### Partida

Meta inicial: 200 puntos. Gana el primer jugador/equipo que alcance o supere la meta al cobrar una ronda. Siguiente ronda conserva puntuaciones y multiplicador. Reiniciar/Nueva partida pone a cero todos los puntos. No hay premios parciales, persistencia ni desempate de clasificación.

## Configuración disponible

En el componente DominoClientController de la escena:

- Teams: parejas o individual.
- Target Score: meta, inicialmente 200.
- Finish Bonus: bono de salida, inicialmente 10.
- Blocked Winner: menor mano individual (regla confirmada); existe variante por suma de pareja.
- Points Source: todos los demás jugadores o solamente rivales.
- Compound Ties: duplicación acumulativa o valor máximo ×2.

Configurar antes de entrar en Play. Los valores se copian a GameRules al iniciar la client y no cambian durante la partida.

## Características implementadas

- Reparto y manos ocultas, selección, arrastre, animación de colocación y rechazo de jugadas inválidas.
- Orientación de fichas para conservar las coincidencias y cadena con curvas.
- Turnos de jugadores simulados, pases y cierre de ronda.
- Giro de manos y puntos individuales al final; marcador acumulado por parejas o individual.
- Resumen del cobro: puntos de manos + bono, multiplicador y puntos otorgados.
- Siguiente ronda y nueva partida.
- UI horizontal con Screen.safeArea; validación visual y táctil de esta revisión pendiente.

## Arquitectura

GameRules define variantes; RoundScoring calcula resultados sin Unity; MatchState conserva puntos, ronda y multiplicador; ClientGame valida y emite eventos. GAME_FINISHED se emite únicamente al alcanzar la meta, mientras ROUND_FINISHED se emite al acabar cada mano. La presentación consume el resultado y no decide quién gana.

No hay servicios externos, juego online ni almacenamiento persistente. Las reglas no especificadas permanecen identificadas como pendientes en este documento; no se consideran confirmadas por el usuario.



## Efectos de presentación

Cada PLAYER_PASSED muestra una mano estilizada dando exactamente dos toques, con ondas visuales, antes de presentar el siguiente turno. Una ronda con ganador muestra su nombre/equipo y confeti dorado; al ganar la partida la celebración dura más. Los empates no celebran ganador. Reiniciar cancela y elimina los efectos. Esta versión no incluye sonidos. Compilación independiente verificada; apariencia y ritmo en Play Mode pendientes.


### Darle agua al dominó

Al finalizar cada ronda, después de mostrar el resultado y celebrar si hay ganador, se voltean y reúnen las 55 fichas, incluidas las 15 reservadas. También ocurre tras una tranca empatada. Dos jugadores situados en lados opuestos remueven las fichas simultáneamente con ambas manos durante unos segundos. Todas permanecen boca abajo al acabar. Cuando termina el efecto, Siguiente ronda (o Nueva partida al alcanzar la meta) permite volver a repartir desde el modelo; el movimiento visual no cambia puntos ni resultados. Reiniciar cancela el efecto y elimina las fichas de reserva temporales. Se ejecuta antes de cada nuevo reparto, sin depender de alcanzar los 200 puntos. Compilación verificada; revisión visual en Unity pendiente.


Vista previa sin terminar la partida: en Play, durante el turno de Fredy, abrir Domino > Ver efecto - Darle agua (Play Mode). Usa copias de las 55 fichas y restaura la mano y la cadena originales al terminar, sin cambiar puntos ni turnos.



Al iniciar una partida nueva también se ejecuta darle agua con las 55 fichas boca abajo, antes del primer reparto. En rondas posteriores se conserva el efecto de cierre anterior para no repetirlo dos veces seguidas.


## Estilos de fichas

El menú de tres líneas permite elegir TEAMFHO · Marfil (predeterminado, puntos negros, marca dorada y bandera cubana en el reverso) o Cuba · Azul (puntos blancos y bandera). Se actualizan las fichas existentes, las de futuros repartos y las usadas en los efectos. La preferencia se conserva localmente con PlayerPrefs. Cambiar el estilo no modifica la mano ni las reglas. Compilación independiente correcta; aspecto y selección en Unity pendientes de comprobación visual.


Tercer estilo disponible: TeamFHO · Blanco. Cara blanca con puntos negros y reverso con el logotipo proporcionado por el usuario, conservando sus colores cian/azul/violeta. Es una opción adicional del selector; los estilos marfil y azul permanecen disponibles. Preferencia guardada también para esta opción.

