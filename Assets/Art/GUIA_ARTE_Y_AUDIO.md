# Bar del Piu — Guía de Arte y Audio (v1)

Esta guía reemplaza la sección 8 del MVP («Arte y dibujo»): el estilo pasa de vectorial a **pixel art cálido**.
La paleta del MVP se conserva como base y se amplía con tonos de sombra y luz.

---

## 1. Dirección visual

**Concepto:** una noche cálida en un bar de barrio. Madera, bronce, luz ámbar de lámparas colgantes y un letrero de neón con el nombre del local.
El jugador está **detrás de la barra**, mirando hacia los clientes.

**Reglas:**
1. **La cerveza manda.** El vaso, el líquido y la espuma son lo más brillante y saturado de la pantalla. El fondo es oscuro y de bajo contraste.
2. **Nunca solo color.** Cada cerveza se identifica por color **y** por letra (R, A, N) en la llave y en el pedido. Así funciona para personas con daltonismo.
3. **Siluetas claras.** Todo sprite lleva contorno de 1 px en tinta cálida (`#1A1410`), nunca negro puro.
4. **Feedback exagerado.** Cada acción responde en menos de 100 ms, con sonido, movimiento y partículas.

### Resolución y píxeles

| Parámetro | Valor |
|---|---|
| Resolución interna | **320 × 180** (escala ×4 exacta a 1280 × 720) |
| Pixels Per Unit | **16** (1 unidad = 16 px) |
| Cámara | `PixelPerfectCamera` de URP, ref. 320×180, Grid Snapping = Pixel Snapping, Crop = None |
| Fondo | 384 × 216 (con márgenes para pantallas de otras proporciones) |
| Filtro / compresión | Point · sin mipmaps · sin compresión (lo aplica `PixelArtImporter` automáticamente) |

### Paleta

| Rol | Hex | Uso |
|---|---|---|
| Tinta | `#1A1410` | Contornos |
| Noche | `#2A1F2D` / `#3D2B3A` | Sombras profundas, cielo |
| Madera | `#42291F` / `#5A3A2E` / `#7A5038` | Pared |
| **Verde barra** (MVP) | `#1B2B24` / **`#263B32`** / `#3A5A49` / `#5C8068` | Barra, delantales, paneles |
| **Crema** (MVP) | `#D9C9A8` / **`#F4EBD8`** | UI, globos de pedido |
| **Ámbar** (MVP) | `#A8701F` / **`#D99B32`** / `#F2C25C` | Bronce, luces, acentos |
| **Café** (MVP) | `#553B26` / **`#795638`** | Texto secundario, marcos |
| **Rojo** (MVP) | `#8E3A30` / **`#C65C4B`** | Errores, alertas |
| Neón | `#FF7A6B` / `#7FE0C8` | Letreros |

### Cervezas

| Cerveza | Letra | Líquido (claro / oscuro) | Espuma |
|---|---|---|---|
| Rubia | **R** | `#F5C84C` / `#D9A030` | `#FFF8EA` |
| Ámbar | **A** | `#D2802E` / `#9A5420` | `#FFF1DA` |
| Negra | **N** | `#4A2C1C` / `#24150E` | `#E9D3B0` |

---

## 2. Composición de la escena de juego (propuesta)

```
 y(px)
 180 ┌──────────────────────────────────────────────┐
     │ HUD: tiempo · puntaje · combo · errores      │
     │   lámparas         NEÓN «BAR DEL PIU»        │
     │  [ventana]                       [ventana]   │
  90 │        cliente   cliente   cliente  cliente  │
     │ LLAVES  🍺        🍺        🍺       🍺      │ ← vasos sobre la barra
  54 ├══════════════ barra (verde) ═════════════════┤
     │ bajo barra (oscuro): diálogos de Pipe/Nata   │
   0 └──────────────────────────────────────────────┘
```

- Torre de 3 llaves a la izquierda, sobre la barra. Hay 4 lugares para clientes.
- La lógica de juego decide si el vaso viaja a la llave o si el chorro llega al vaso. El chorro (`BeerPourStream`) va de cualquier punto de origen a cualquier vaso.

---

## 3. Personajes

- **Clientes:** 8 variantes (pelo, piel, ropa) × 4 expresiones: `neutral`, `feliz`, `impaciente`, `enojado`. La expresión cambia sola según la paciencia.
- **Pipe y Nata:** retratos de 48×48 con 3 expresiones (`neutral`, `feliz`, `sorpresa`).
  ⚠️ **Son provisorios.** Se rediseñan cuando lleguen las referencias reales (pendiente del MVP).

---

## 4. Dirección de audio

**Concepto:** *chiptune cálido de bar*. Sonidos sintetizados, suaves y redondos (triángulo y pulso filtrado, sin estridencias), sobre un ambiente de bar real.

| Bus | Contenido | Volumen inicial |
|---|---|---|
| Música | Loop de menú, loop de juego + capa «rush» (percusión) sincronizada | 0.7 |
| Ambiente | Murmullo de bar en loop (30 s) | 0.5 |
| SFX | Llaves, servido, vasos, clientes, resultados | 1.0 |
| UI | Clics, diálogos, cuenta regresiva | 0.8 |

**Reglas de mezcla:**
- El chorro (`pour_loop`) **sube de tono a medida que se llena el vaso**: el jugador escucha el nivel.
- Cada Perfecto seguido sube **un semitono** el sonido de combo (tope en x20).
- Los resultados importantes bajan un poco la música (*ducking*) durante 0,6 s.
- En los últimos 20 segundos entra la capa «rush» de la música.
- En WebGL el audio arranca recién con el primer clic o toque (regla del navegador). El menú lo resuelve solo.

---

## 5. Cómo se usa desde la lógica de juego

```csharp
using BarDelPiu.Audio;
using BarDelPiu.Visual;

// Sonido
AudioManager.Play(Sfx.TapOpen);
AudioManager.PlayMusic(MusicTrack.Game);
AudioManager.SetMusicIntensity(1f);            // activa la capa rush
AudioManager.PlayCombo(comboLevel);            // sube de tono por nivel

// Vaso
glass.SetBeer(BeerKind.Rubia);
glass.SetLevels(liquid01, foam01);             // fracciones de la capacidad
glass.SetPouring(true, tapNozzle.position);    // chorro + sonido automático

// Cliente
customer.Setup(variant: 3, order: BeerKind.Negra);
customer.SetPatience(0.4f);                    // cambia expresión y barra
customer.PlayLeave(happy: true);

// Feedback
FeedbackDirector.Instance.ServeResult(ServeGrade.Perfecto, pos, points: 200, combo: 2);
FeedbackDirector.Instance.Tip(pos, amount: 50);
```

**Menú:** `Bar del Piu ▸ Arte y Audio ▸ Configurar todo` vuelve a generar materiales, librerías, prefabs y la escena de prueba `ArtAudioTest`.

---

## 6. Regenerar los assets

El arte y el audio se generan por código. Así se pueden ajustar la paleta o el tono sin redibujar a mano:

```
python Tools/ArtGen/gen_sprites.py
python Tools/ArtGen/gen_audio.py
```

Para reemplazar un asset por uno dibujado a mano, sobrescribe el PNG o WAV con el mismo nombre. Las referencias se mantienen.
