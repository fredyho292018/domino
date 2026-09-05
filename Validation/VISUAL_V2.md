# Modern Social Premium

Source SHA: ac4c70f55cbed292791798e9fac39648ecb6f4a4. Working tree limpio antes del rediseño.

## Presentación

- Paleta central `DominoVisualTheme`: petróleo, esmeralda, texto cálido y oro discreto. `UiKit` comparte estos colores; `TileStyles` sigue conservando las tres opciones y las preferencias del usuario.
- Mesa con borde fino, profundidad e iluminación central mediante una malla de UI recortada al borde redondeado. Sin texturas externas, física ni postprocesado.
- Avatares circulares generados una sola vez, acento de equipo y halo de turno. Las posiciones Maria/Alex/John/Fredy no cambian.
- HUD flotante NOSOTROS/ELLOS; conserva puntuación, ronda, meta y multiplicador. Menú y Reiniciar mantienen sus zonas de interacción.
- Mano local ligeramente mayor, sombra adicional al seleccionar. Oponentes más compactos; reserva visible con sus quince reversos.
- Micro rebote de 3 % durante 0,14 s tras llegar y revelar la ficha. El evento visual `TileLanded` permite conectar audio posteriormente; no se añade sonido ni dependencia.
- Se conservan lavado, reparto de cuarenta fichas, reserva apartada después de la última entrega, revelado, organización, selección, arrastre, extremos legales, zoom, efectos de pase y victoria, reinicio y estilos TeamFHO.

## Validación

Pruebas de configuración: 80; dominio/geometría: 2.981.089; traza de cien partidas idéntica a la referencia. Sin cambios en Core, Game, Configuration, JSON ni controlador de bots.

Play Mode de reparto: SUCCESS, 78.291 comprobaciones, Console Errors = 0. Game View en 16:9, 18:9, 19.5:9, 20:9, 21:9 y tablet 4:3. Capturas móvil/tablet revisadas; manos y reserva dentro de safe area. Evidencia ignorada: `Generated/visual-v2-unity.log`, `Generated/dealing-*.png`.

Integración de partida completa: SUCCESS, tres rondas hasta la meta 200 con renderizado activo, Console Errors = 0 (`Generated/visual-v2-rendered-match-unity.log`). La captura de cadena confirmó separación, dobles transversales y contraste. Se ajustó después la salida inmediata del halo anterior para evitar dos indicadores simultáneos durante la transición.

Validación final tras ese ajuste: SUCCESS, 78.532 comprobaciones en los seis formatos y Console Errors = 0 (`Generated/visual-v2-final-unity.log`). Captura final 16:9 revisada con halo exclusivo de Fredy.

Diff de archivos existentes: `Generated/visual-v2.diff`. Los nuevos `DominoVisualTheme.cs`, su meta y este documento se revisan por separado porque `git diff` no incluye archivos sin seguimiento.

## Límites

60 FPS es el objetivo, no una medición en Android/iOS. Falta probar dispositivos físicos con notch y navegación por gestos. Avatares con iniciales, sin fotografías. Se preservan los estilos de fichas previamente seleccionados, incluido Cuba Azul; no se reemplaza la preferencia guardada por marfil.

Sin commit ni push. Revisar `git diff` y los archivos nuevos antes de aprobar la dirección visual.
