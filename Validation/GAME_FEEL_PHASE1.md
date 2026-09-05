# Domino — Game feel fase 1

Source SHA: `ac4c70f55cbed292791798e9fac39648ecb6f4a4`. Se preservan los cambios pendientes de Visual V2 y Device Matrix.

## Auditoría previa

Unity 6000.0.41f1, Built-in en GraphicsSettings y todos los niveles de QualitySettings. No hay DOTween Core/Pro, Feel ni Text Animator en Assets, Packages o dependencias resueltas. Shader Graph está incluido entre paquetes del editor pero no instalado en el proyecto; esta fase se difiere según la restricción de Built-in. Sin postprocesado/Volumes, AudioSource, AudioClip ni sonidos en el proyecto. El sistema previo usa coroutines, interpolación de UI y `TileLanded` como señal de impacto.

Se habilita únicamente el módulo oficial incorporado `com.unity.modules.particlesystem` 1.0.0. No hay compras, descargas externas ni cambio de pipeline. DOTween Pro, Feel y Text Animator quedan NOT_INSTALLED; All In 1 y Beautify DEFERRED.

## Implementación

`FeedbackPresenter` coordina señales de audio, pulsos de turno/marcador y tres `FeedbackParticles` persistentes. Estos últimos usan ParticleSystem nativo para simulación y una malla UGUI ligera para representarla en el Canvas Overlay existente. El ParticleSystemRenderer normal permanece deshabilitado; no se convierte la mesa a World Space ni se añade cámara de efectos.

- TileImpactParticles: cuatro partículas, alrededor de 0,24 s, después de llegar/revelar la ficha.
- RoundWinParticles: veinte partículas en calidad Medium, alrededor de 1,4 s.
- GameWinConfetti: cuarenta partículas en Medium, alrededor de 1,8 s.
- Textos: pulso nativo corto en TU TURNO y marcador; se mantienen los textos y la tarjeta de victoria existentes. No se usa Text Animator.
- Se sustituye el confeti anterior que creaba 32 objetos UI por celebración. Se conservan lavado, manos de pase, victoria, reparto, reserva, selección, movimiento, zoom y micro impacto existentes.
- Señales `TilePick`, `TilePlay`, `TileImpact`, `RoundWin`, `GameWin` para un adaptador de audio futuro. No producen sonido actualmente. Sin hápticos.

La capa nueva no toca posición/escala de las fichas: BoardView mantiene el movimiento y micro rebote. FeedbackPresenter solo controla los pulsos de texto/score y partículas, evitando dos responsables para una misma propiedad.

## Ciclo de vida y opciones

Clear, Restart y OnDisable cancelan coroutines propias, restablecen escalas y vacían los tres sistemas. Buffers de partículas y vértices reutilizados. Sin Instantiate/Destroy por impacto; los sistemas se crean al inicializar BoardView. Un Canvas hijo aísla la geometría animada del HUD estático.

FeedbackAnimationMode Full/Reduced/Off, ParticlesEnabled, Quality Low/Medium/High y Speed permiten ajustar **los nuevos efectos** desde código/Inspector. No son todavía una preferencia guardada ni un ajuste global de las animaciones previas de reparto/lavado. Low elimina impactos secundarios; Reduced reduce el número de partículas. Cambiar ParticlesEnabled u Off limpia los efectos activos.

## Validación

La compilación independiente se verificó antes y después de conectar la presentación. Las pruebas de Play Mode se ejecutan en una copia aislada; los resultados actuales quedan en `Generated/game-feel-unity.log`, `Generated/game-feel-dealing-unity.log` y sus archivos de resultado. DealingSmokeTest incluye límites de partículas, reutilización de tres sistemas, efectos Off/Reduced y Restart con partículas activas.

Partida completa: SUCCESS, cinco rondas hasta la meta 200, Console Errors=0. Valida selección/deselección, arrastre, jugadas, pases, turnos, fin de ronda/partida y reinicio. Configuración: 80 comprobaciones; dominio: 2.981.089 en 1.000 partidas; digest de regresión idéntico al original. Se verificó que Core/Game/Configuration y el controlador de bots no tienen diferencias respecto al commit base.

Validación final: SUCCESS, 89.172 comprobaciones, seis formatos (incluido 18:9), Console Errors=0. Se corrigió la ausencia inicial de CanvasRenderer en el adaptador UI; la prueba ahora exige malla visible para partículas activas. Captura revisada: `Generated/dealing-phase-FeedbackParticles.png`. Evidencia final: `Generated/game-feel-render-fixed-unity.log`. Restart limpia partículas activas y los modos Reduced/Off pasan. El diff acumulado de archivos existentes está en `Generated/game-feel-working-tree.diff`; también contiene el trabajo previo pendiente y no incluye archivos nuevos sin seguimiento.

## Límites

60 FPS es un objetivo, no una medición física. Falta perfilar overdraw y coste de reconstrucción de Canvas en Android/iOS. No hay shaders nuevos, sonido TAC ni hápticos. Los assets comerciales ausentes no se sustituyen por copias ni código propietario recreado.

Sin cambios en motor, configuración de reglas, bots ni backend. Sin commit ni push.
