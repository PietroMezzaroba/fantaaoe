<script setup>
// Il paladino di Age of Empires II DE, con gli sprite del gioco (colore giocatore rosso, 8 direzioni), che si può abbattere.
//  - si gira verso il puntatore;
//  - clic sul paladino: un colpo (40 punti vita su 160, barra verde e rossa come nel gioco) e lui risponde con un attacco;
//    al quarto colpo muore con l'animazione del gioco, resta a terra e dopo un po' ne arriva un altro;
//  - clic sul terreno: ci va al passo;
//  - da tastiera: Invio o spazio sul riquadro colpiscono il paladino.
// Gli sprite (public/sprite/paladino/) vengono dai file .sld del gioco: estratti con gli strumenti del vault
// (tools/aoe2-sld), non con codice di questo repo. Si caricano solo quando il riquadro si avvicina allo schermo.
// Con "riduci movimento" niente animazioni: le pose cambiano di colpo, il resto funziona uguale.
import { ref, computed, onMounted, onBeforeUnmount } from 'vue'
import { suonoColpo, suonoCaduta } from '../suoni.js'

const BASE = '/sprite/paladino/'
const VITA = 160
const DANNO = 40
const SCALA = 1.4
const VELOCITA = 70 // px al secondo, al passo

const arena = ref(null)
const tela = ref(null)
const pronto = ref(false)
const vita = ref(VITA)
const abbattuti = ref(leggiAbbattuti())
const stato = ref('fermo') // fermo | passo | attacco | morte | cadavere | assente

let ctx, meta, immagini = {}, raf = 0, visibile = false, ridotto = false
let larghezza = 0, altezza = 0, dpr = 1
let ultimo = 0
let anim = 'fermo', fotogramma = 0, tempoAnim = 0
let riga = 2 // direzione (0 destra, 2 verso chi guarda, 4 sinistra, 6 di spalle)
let x = 0, y = 0, meta_x = null, meta_y = null
let lampo = 0, attesa = 0
let colori = { verde: '#3c3', rosso: '#c33' }
let puntatore = null
let osservatore, caricamentoAvviato = false

function leggiAbbattuti() {
  try { return Number(localStorage.getItem('fantaage_paladini_abbattuti')) || 0 } catch { return 0 }
}
function salvaAbbattuti(n) {
  try { localStorage.setItem('fantaage_paladini_abbattuti', String(n)) } catch { /* senza storage il conto vale solo qui */ }
}

const etichetta = computed(() => {
  if (stato.value === 'morte' || stato.value === 'cadavere') return 'Paladino abbattuto'
  if (stato.value === 'assente') return 'Sta arrivando un altro paladino'
  return `Paladino nemico, vita ${vita.value} su ${VITA}. Premi per colpirlo`
})
const didascalia = computed(() => {
  if (stato.value === 'morte' || stato.value === 'cadavere') return 'Abbattuto. Ne arriva un altro.'
  if (stato.value === 'assente') return 'Ne arriva un altro…'
  return 'Clicca sul paladino per colpirlo, sul terreno per farlo muovere.'
})

// direzione dalla posizione (lo schermo isometrico schiaccia la verticale: la si raddoppia prima di misurare l'angolo)
function direzioneVerso(px, py) {
  const a = Math.atan2((py - y) * 1.8, px - x)
  return ((Math.round(a / (Math.PI / 4)) % 8) + 8) % 8
}

function imposta(nome, ripartendo = true) {
  if (anim === nome && !ripartendo) return
  anim = nome
  fotogramma = 0
  tempoAnim = 0
}

function leggiColori() {
  const s = getComputedStyle(document.documentElement)
  colori = { verde: s.getPropertyValue('--color-accent').trim() || colori.verde, rosso: s.getPropertyValue('--color-error').trim() || colori.rosso }
}

function dimensiona() {
  const r = arena.value.getBoundingClientRect()
  dpr = Math.min(2, window.devicePixelRatio || 1)
  larghezza = r.width
  altezza = r.height
  tela.value.width = Math.round(larghezza * dpr)
  tela.value.height = Math.round(altezza * dpr)
  ctx.setTransform(dpr, 0, 0, dpr, 0, 0)
  ctx.imageSmoothingQuality = 'high'
  if (!x) { x = larghezza / 2; y = altezza * 0.66 }
  x = Math.min(Math.max(x, 60), larghezza - 60)
  y = Math.min(Math.max(y, 100), altezza - 16)
  disegna()
}

function nuovoPaladino() {
  vita.value = VITA
  lampo = 0
  const daSinistra = Math.random() < 0.5
  x = daSinistra ? -60 : larghezza + 60
  y = altezza * (0.6 + Math.random() * 0.15)
  meta_x = larghezza * (0.35 + Math.random() * 0.3)
  meta_y = y
  if (ridotto) { x = meta_x; meta_x = meta_y = null; riga = 2; stato.value = 'fermo'; imposta('fermo'); return }
  riga = daSinistra ? 0 : 4
  stato.value = 'passo'
  imposta('passo')
}

function colpisci() {
  if (!pronto.value || !['fermo', 'passo', 'attacco'].includes(stato.value)) return
  vita.value = Math.max(0, vita.value - DANNO)
  lampo = 1
  suonoColpo()
  meta_x = meta_y = null
  if (vita.value <= 0) {
    stato.value = 'morte'
    imposta('morte')
    suonoCaduta()
    abbattuti.value++
    salvaAbbattuti(abbattuti.value)
    if (ridotto) { fotogramma = meta.animazioni.morte.fotogrammi - 1; attesa = 2.5 }
  } else {
    // risponde al colpo: si gira verso chi l'ha colpito e attacca
    if (puntatore) riga = direzioneVerso(puntatore.x, puntatore.y)
    stato.value = 'attacco'
    imposta('attacco')
    attesa = ridotto ? 0.6 : 0
  }
  avvia()
}

function sulPaladino(px, py) {
  // ingombro del cavaliere: un ovale sopra il punto d'appoggio
  const dx = (px - x) / (42 * SCALA), dy = (py - (y - 30 * SCALA)) / (40 * SCALA)
  return dx * dx + dy * dy <= 1
}

function clic(e) {
  if (!pronto.value) return
  const r = arena.value.getBoundingClientRect()
  const px = e.clientX - r.left, py = e.clientY - r.top
  if (sulPaladino(px, py)) return colpisci()
  if (!['fermo', 'passo', 'attacco'].includes(stato.value)) return
  // sul terreno: ci va al passo (restando dentro il riquadro)
  meta_x = Math.min(Math.max(px, 60), larghezza - 60)
  meta_y = Math.min(Math.max(py, 100), altezza - 16)
  riga = direzioneVerso(meta_x, meta_y)
  if (ridotto) { x = meta_x; y = meta_y; meta_x = meta_y = null; disegna(); return }
  stato.value = 'passo'
  imposta('passo', false)
  avvia()
}

function muovi(e) {
  const r = arena.value.getBoundingClientRect()
  puntatore = { x: e.clientX - r.left, y: e.clientY - r.top }
  arena.value.style.cursor = pronto.value && sulPaladino(puntatore.x, puntatore.y) && vita.value > 0 ? 'crosshair' : ''
  if (stato.value === 'fermo') {
    const nuova = direzioneVerso(puntatore.x, puntatore.y)
    if (nuova !== riga) { riga = nuova; if (ridotto) disegna() }
  }
  avvia()
}
function esci() { puntatore = null }

function aggiorna(dt) {
  const a = meta.animazioni[anim]
  // con "riduci movimento" le pose restano ferme: si cambia solo stato
  tempoAnim += ridotto ? 0 : dt
  const passo = 1 / a.fps
  while (tempoAnim >= passo) {
    tempoAnim -= passo
    if (fotogramma < a.fotogrammi - 1) fotogramma++
    else if (a.ripeti) fotogramma = 0
    else fotogramma = a.fotogrammi - 1
  }
  if (lampo > 0) lampo = Math.max(0, lampo - dt * 6)

  if (stato.value === 'passo' && meta_x != null) {
    const dx = meta_x - x, dy = meta_y - y
    const d = Math.hypot(dx, dy)
    if (d < 2) { x = meta_x; y = meta_y; meta_x = meta_y = null; stato.value = 'fermo'; imposta('fermo') }
    else { const k = Math.min(1, (VELOCITA * dt) / d); x += dx * k; y += dy * k }
  } else if (stato.value === 'attacco') {
    if (fotogramma >= a.fotogrammi - 1 || (ridotto && (attesa -= dt) <= 0)) { stato.value = 'fermo'; imposta('fermo') }
  } else if (stato.value === 'morte') {
    if (ridotto) { if ((attesa -= dt) <= 0) { stato.value = 'cadavere'; imposta('cadavere'); fotogramma = meta.animazioni.cadavere.fotogrammi - 1; attesa = 1.5 } }
    else if (fotogramma >= a.fotogrammi - 1) { stato.value = 'cadavere'; imposta('cadavere') }
  } else if (stato.value === 'cadavere') {
    if (ridotto ? (attesa -= dt) <= 0 : fotogramma >= a.fotogrammi - 1) { stato.value = 'assente'; attesa = 1.2 }
  } else if (stato.value === 'assente') {
    if ((attesa -= dt) <= 0) nuovoPaladino()
  }
}

function disegna() {
  if (!ctx || !meta) return
  ctx.clearRect(0, 0, larghezza, altezza)
  if (stato.value === 'assente') return
  const a = meta.animazioni[anim]
  const img = immagini[anim]
  if (!img?.complete) return
  const [cw, ch] = a.cella
  const [ax, ay] = a.ancora
  const dx = Math.round(x - ax * SCALA), dy = Math.round(y - ay * SCALA)
  ctx.globalAlpha = stato.value === 'cadavere' && !ridotto ? Math.max(0.15, 1 - fotogramma / a.fotogrammi * 0.7) : 1
  ctx.drawImage(img, fotogramma * cw, riga * ch, cw, ch, dx, dy, cw * SCALA, ch * SCALA)
  // lampo bianco del colpo, solo sui pixel del paladino
  if (lampo > 0) {
    ctx.globalCompositeOperation = 'lighter'
    ctx.globalAlpha = lampo * 0.55
    ctx.drawImage(img, fotogramma * cw, riga * ch, cw, ch, dx, dy, cw * SCALA, ch * SCALA)
    ctx.globalCompositeOperation = 'source-over'
  }
  ctx.globalAlpha = 1
  // barra della vita sopra la testa, come nel gioco
  if (['fermo', 'passo', 'attacco'].includes(stato.value) && vita.value < VITA) {
    const bw = 40, bh = 4, bx = Math.round(x - bw / 2), by = Math.round(y - 78 * SCALA)
    ctx.fillStyle = colori.rosso
    ctx.fillRect(bx, by, bw, bh)
    ctx.fillStyle = colori.verde
    ctx.fillRect(bx, by, Math.round(bw * vita.value / VITA), bh)
  }
}

function ciclo(ora) {
  raf = 0
  const dt = ultimo ? Math.min(0.1, (ora - ultimo) / 1000) : 0
  ultimo = ora
  aggiorna(ridotto && ['fermo'].includes(stato.value) ? 0 : dt)
  disegna()
  const fermo = ridotto && stato.value === 'fermo'
  if (visibile && !document.hidden && !fermo) raf = requestAnimationFrame(ciclo)
  else ultimo = 0
}
function avvia() {
  if (!raf && pronto.value && visibile) raf = requestAnimationFrame(ciclo)
}

function carica() {
  if (caricamentoAvviato) return
  caricamentoAvviato = true
  fetch(BASE + 'paladino-rosso.json')
    .then((r) => r.json())
    .then((j) => {
      meta = j
      const promesse = Object.entries(j.animazioni).map(([nome, a]) => new Promise((ok) => {
        const im = new Image()
        im.onload = im.onerror = ok
        im.src = BASE + a.file
        immagini[nome] = im
      }))
      return Promise.all(promesse)
    })
    .then(() => {
      pronto.value = true
      dimensiona()
      avvia()
    })
    .catch(() => { /* senza sprite il riquadro resta vuoto con la didascalia */ })
}

function suTasto(e) {
  if (e.key === 'Enter' || e.key === ' ') {
    e.preventDefault()
    colpisci()
  }
}

let ro, mq
function cambiaVisibilita() { if (!document.hidden) avvia() }
onMounted(() => {
  ctx = tela.value.getContext('2d')
  mq = window.matchMedia('(prefers-reduced-motion: reduce)')
  ridotto = mq.matches
  mq.addEventListener('change', (e) => { ridotto = e.matches; avvia() })
  leggiColori()
  ro = new ResizeObserver(() => { if (pronto.value) dimensiona() })
  ro.observe(arena.value)
  osservatore = new IntersectionObserver(([v]) => {
    visibile = v.isIntersecting
    if (visibile) { carica(); avvia() }
  }, { rootMargin: '300px 0px' })
  osservatore.observe(arena.value)
  document.addEventListener('visibilitychange', cambiaVisibilita)
})
onBeforeUnmount(() => {
  cancelAnimationFrame(raf)
  osservatore?.disconnect()
  ro?.disconnect()
  document.removeEventListener('visibilitychange', cambiaVisibilita)
})
</script>

<template>
  <figure class="paladino">
    <div
      ref="arena"
      class="paladino__arena"
      :class="{ 'paladino__arena--pronta': pronto }"
      role="button"
      tabindex="0"
      :aria-label="etichetta"
      @click="clic"
      @pointermove="muovi"
      @pointerleave="esci"
      @keydown="suTasto"
    >
      <canvas ref="tela" aria-hidden="true" />
    </div>
    <figcaption class="paladino__didascalia">
      <span>{{ didascalia }}</span>
      <span v-if="abbattuti" class="paladino__conto">Abbattuti: <strong class="num">{{ abbattuti }}</strong></span>
    </figcaption>
  </figure>
</template>

<style scoped>
.paladino { margin: var(--space-md) 0 0; display: grid; gap: var(--space-xs); }
.paladino__arena {
  position: relative; height: 230px; border-radius: var(--radius-card); overflow: hidden; touch-action: manipulation;
  background: radial-gradient(ellipse at 50% 64%, color-mix(in oklch, var(--color-paper-3) 90%, transparent), var(--color-paper-2) 72%);
  user-select: none; -webkit-tap-highlight-color: transparent;
}
.paladino__arena canvas { width: 100%; height: 100%; display: block; opacity: 0; transition: opacity var(--dur-long) var(--ease-out); }
.paladino__arena--pronta canvas { opacity: 1; }
.paladino__arena:focus-visible { outline: 2px solid var(--color-focus); outline-offset: 2px; }
/* il clic si sente anche sul riquadro: 1 px giù, come i bottoni del sito */
.paladino__arena:active canvas { transform: translateY(1px); }
.paladino__didascalia { display: flex; flex-wrap: wrap; justify-content: space-between; gap: var(--space-xs); color: var(--color-muted); font-size: var(--text-sm); }
.paladino__conto strong { color: var(--color-ink); }
</style>
