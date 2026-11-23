<script setup>
// L'elmo di AoE Italia come nuvola di punti 3D (dal logo della community, strumenti/genera_punti.py).
// Il puntatore inclina la vista e scosta i punti vicini; una scansione lo attraversa ogni sette secondi.
// Si ferma fuori schermo, con la scheda nascosta, col bottone di pausa e con "riduci movimento".
import { ref, onMounted, onBeforeUnmount } from 'vue'

// centroX: dove sta l'elmo in larghezza (0..1); quota: quanta larghezza può occupare
const props = defineProps({ centroX: { type: Number, default: 0.5 }, quota: { type: Number, default: 1 } })

const tela = ref(null)
const contenitore = ref(null)
const inPausa = ref(false)
const pronto = ref(false)

let ctx, dati, n, raf = 0, visibile = true, ridotto = false
let larghezza = 0, altezza = 0, dpr = 1
let colori = {}
let spostX, spostY
const mira = { x: 0, y: 0 }, vista = { x: 0, y: 0 }
const puntatore = { x: -9999, y: -9999, attivo: false }
let inizio = 0

function leggiColori() {
  const s = getComputedStyle(document.documentElement)
  const v = (k) => s.getPropertyValue(k).trim()
  colori = {
    lo: v('--point-mid'), mid: v('--color-ink-2'), hi: v('--point-hi'),
    verde: v('--color-accent'), bianco: v('--color-ink'), rosso: v('--color-error'),
  }
}

function dimensiona() {
  const r = contenitore.value.getBoundingClientRect()
  dpr = Math.min(2, window.devicePixelRatio || 1)
  larghezza = r.width
  altezza = r.height
  tela.value.width = Math.round(larghezza * dpr)
  tela.value.height = Math.round(altezza * dpr)
  ctx.setTransform(dpr, 0, 0, dpr, 0, 0)
}

const easeInOut = (t) => (t < 0.5 ? 4 * t * t * t : 1 - Math.pow(-2 * t + 2, 3) / 2)

function disegna(ora) {
  raf = 0
  const t = (ora - inizio) / 1000
  ctx.clearRect(0, 0, larghezza, altezza)

  // vista: oscillazione lenta + puntatore, smorzata
  const ozio = ridotto || inPausa.value ? 0 : Math.sin(t / 9) * 0.22
  vista.x += (mira.x + ozio - vista.x) * 0.06
  vista.y += (mira.y - vista.y) * 0.06
  const cy = Math.cos(vista.x), sy = Math.sin(vista.x)
  const cx = Math.cos(vista.y), sx = Math.sin(vista.y)

  const scala = Math.min((larghezza * props.quota) / 1550, altezza / 1300)
  const f = 1100
  const ox = larghezza * props.centroX, oy = altezza * 0.5

  // ingresso: i punti si raccolgono in 0,8 s
  const arrivo = ridotto ? 1 : Math.min(1, t / 0.8)
  const raccolta = 1 - Math.pow(1 - arrivo, 3)

  // scansione: un piano che attraversa l'elmo da sinistra a destra ogni 7 s
  const ciclo = ridotto ? 0.5 : (t % 7) / 7
  const piano = -820 + easeInOut(Math.min(1, ciclo / 0.7)) * 1640
  const scansioneAttiva = ridotto || ciclo < 0.7

  const secchi = { lo: [], mid: [], hi: [], verde: [], bianco: [], rosso: [], luce: [] }
  const p = dati
  const raggio = 110, raggio2 = raggio * raggio

  for (let i = 0; i < n; i++) {
    const k = i * 4
    let x = p[k] * (2 - raccolta), y = p[k + 1] * (2 - raccolta), z = p[k + 2] + (1 - raccolta) * 400
    const c = p[k + 3]
    // rotazione: imbardata (x) poi beccheggio (y)
    const x1 = x * cy + z * sy
    const z1 = -x * sy + z * cy
    const y1 = y * cx - z1 * sx
    const z2 = y * sx + z1 * cx
    const prosp = f / (f + z2 * 0.6)
    let sxp = ox + x1 * scala * prosp
    let syp = oy + y1 * scala * prosp

    // il puntatore scosta i punti vicini, che poi tornano a posto
    if (puntatore.attivo && !ridotto) {
      const dx = sxp - puntatore.x, dy = syp - puntatore.y
      const d2 = dx * dx + dy * dy
      if (d2 < raggio2 && d2 > 0.01) {
        const d = Math.sqrt(d2)
        const forza = (1 - d / raggio) * 28
        spostX[i] += (dx / d) * forza * 0.18
        spostY[i] += (dy / d) * forza * 0.18
      }
    }
    spostX[i] *= 0.9
    spostY[i] *= 0.9
    sxp += spostX[i]
    syp += spostY[i]

    const lato = Math.max(1.5, 2.3 * prosp * Math.min(1.6, scala * 3.4)) * (c ? 1.25 : 1)
    const illuminato = scansioneAttiva && Math.abs(x - piano) < 38
    let secchio
    if (c === 1) secchio = 'verde'
    else if (c === 2) secchio = 'bianco'
    else if (c === 3) secchio = 'rosso'
    else if (illuminato) secchio = 'luce'
    else secchio = z > 35 ? 'hi' : z > -20 ? 'mid' : 'lo'
    secchi[secchio].push(sxp, syp, lato)
  }

  for (const [nome, arr] of Object.entries(secchi)) {
    if (!arr.length) continue
    ctx.fillStyle = nome === 'luce' ? colori.hi : colori[nome]
    ctx.globalAlpha = nome === 'lo' ? 0.75 : nome === 'mid' ? 0.9 : 1
    for (let j = 0; j < arr.length; j += 3) ctx.fillRect(arr[j], arr[j + 1], arr[j + 2], arr[j + 2])
  }
  ctx.globalAlpha = 1

  if (!ridotto && !inPausa.value && visibile && !document.hidden) raf = requestAnimationFrame(disegna)
}

function avvia() {
  if (!raf && dati) raf = requestAnimationFrame(disegna)
}

function muovi(e) {
  const r = contenitore.value.getBoundingClientRect()
  const px = (e.clientX - r.left) / r.width - 0.5
  const py = (e.clientY - r.top) / r.height - 0.5
  mira.x = px * 0.9
  mira.y = -py * 0.45
  puntatore.x = e.clientX - r.left
  puntatore.y = e.clientY - r.top
  puntatore.attivo = true
  if (inPausa.value || ridotto) avvia()
}
function esci() {
  mira.x = 0
  mira.y = 0
  puntatore.attivo = false
}

function pausa() {
  inPausa.value = !inPausa.value
  if (!inPausa.value) avvia()
}

let osservatore, mq, ro
function cambiaVisibilita() {
  if (!document.hidden) avvia()
}

onMounted(async () => {
  ctx = tela.value.getContext('2d')
  mq = window.matchMedia('(prefers-reduced-motion: reduce)')
  ridotto = mq.matches
  mq.addEventListener('change', (e) => { ridotto = e.matches; avvia() })
  leggiColori()
  dimensiona()
  ro = new ResizeObserver(() => { dimensiona(); avvia() })
  ro.observe(contenitore.value)
  try {
    const r = await fetch('/elmo-punti.json')
    const j = await r.json()
    dati = Int16Array.from(j.p)
    n = j.n
    spostX = new Float32Array(n)
    spostY = new Float32Array(n)
  } catch {
    return
  }
  pronto.value = true
  inizio = performance.now()
  osservatore = new IntersectionObserver(([v]) => { visibile = v.isIntersecting; avvia() })
  osservatore.observe(contenitore.value)
  document.addEventListener('visibilitychange', cambiaVisibilita)
  avvia()
})

onBeforeUnmount(() => {
  cancelAnimationFrame(raf)
  osservatore?.disconnect()
  ro?.disconnect()
  document.removeEventListener('visibilitychange', cambiaVisibilita)
})
</script>

<template>
  <div
    ref="contenitore"
    class="nuvola"
    :class="{ 'nuvola--pronta': pronto }"
    @pointermove="muovi"
    @pointerleave="esci"
  >
    <canvas ref="tela" role="img" aria-label="L'elmo di Age of Empires Italia disegnato con migliaia di punti, che si inclina seguendo il puntatore" />
    <button
      v-if="pronto"
      type="button"
      class="nuvola__pausa keep-motion"
      :aria-pressed="inPausa"
      @click="pausa"
    >
      {{ inPausa ? 'Riprendi' : 'Ferma' }}
    </button>
  </div>
</template>
