<script setup>
// L'elmo di AoE Italia come nuvola di punti 3D (dal logo della community, strumenti/genera_punti.py).
// All'apertura i punti arrivano sparsi e si ricompongono ruotando; poi l'elmo ondeggia da solo e un passaggio
// automatico gli scorre sopra. Il puntatore vero, se si muove, prende il posto di quello automatico.
// Una scansione lo attraversa ogni sette secondi.
// Si ferma fuori schermo, con la scheda nascosta, col bottone di pausa e con "riduci movimento".
// In pausa l'animazione si azzera: l'elmo resta fermo, dritto e ricomposto, senza nessun effetto.
// Motore: WebGPU dove c'è (fisica dei punti in un compute shader, punti tondi e scie quando corrono, elmoGpu.js);
// altrimenti il disegno 2D di sempre. Se la GPU si perde a metà, la tela si ricrea e si passa al 2D.
import { ref, computed, watch, onMounted, onBeforeUnmount, nextTick } from 'vue'
import { suonoRilancio } from '../suoni.js'
import { creaElmoGpu } from '../elmoGpu.js'

// centroX: dove sta l'elmo in larghezza (0..1); quota: quanta larghezza può occupare
// animata: false = l'elmo resta sempre fermo (impostazione del profilo) e il pulsante di pausa non compare;
// null = l'impostazione non è ancora arrivata, si aspetta prima di partire.
const props = defineProps({
  centroX: { type: Number, default: 0.5 },
  quota: { type: Number, default: 1 },
  animata: { default: true },
})

const tela = ref(null)
const contenitore = ref(null)
const paladinoEl = ref(null)
const inPausa = ref(false)
const pronto = ref(false)
// ferma per scelta: in pausa col pulsante oppure animazione spenta nel profilo
const congelata = computed(() => inPausa.value || props.animata === false)
let datiPronti = false

let ctx, gpu = null, dati, n, raf = 0, visibile = true, ridotto = false
let larghezza = 0, altezza = 0, dpr = 1
let ultimoFotogramma = 0
// cambia quando la GPU si perde: Vue ricrea la tela, che così può avere un contesto 2D
const chiaveTela = ref(0)
const motore = ref('')
let colori = {}
let spostX, spostY, sparsi
let ultimoMovimento = -1e9
const INGRESSO = 2.4
const mira = { x: 0, y: 0 }, vista = { x: 0, y: 0 }
const puntatore = { x: -9999, y: -9999, attivo: false }
let inizio = 0

// Carica del Paladino di AoE2 DE (profilo laterale dir 6) a velocità realistica (~135 px/s)
const carica = { attiva: false, inizio: 0, durata: 5.2, prossimo: 2.8, suonato: false }

function lanciaCarica(ora) {
  if (ridotto || congelata.value) return
  carica.attiva = true
  carica.inizio = ora
  carica.durata = Math.max(3.6, (larghezza + 220) / 135)
  carica.suonato = false
  carica.prossimo = (ora - inizio) / 1000 + carica.durata + 12
}

function leggiColori() {
  const s = getComputedStyle(document.documentElement)
  const v = (k) => s.getPropertyValue(k).trim()
  colori = {
    lo: v('--point-mid'), mid: v('--color-ink-2'), hi: v('--point-hi'),
    verde: v('--color-accent'), bianco: v('--color-ink'), rosso: v('--color-error'),
  }
  gpu?.colori(colori)
}

function dimensiona() {
  if (!contenitore.value || !tela.value) return
  const r = contenitore.value.getBoundingClientRect()
  dpr = Math.min(2, window.devicePixelRatio || 1)
  larghezza = r.width
  altezza = r.height
  if (gpu) {
    gpu.dimensiona(larghezza, altezza, dpr)
  } else if (ctx) {
    tela.value.width = Math.round(larghezza * dpr)
    tela.value.height = Math.round(altezza * dpr)
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0)
  }
}

const easeInOut = (t) => (t < 0.5 ? 4 * t * t * t : 1 - Math.pow(-2 * t + 2, 3) / 2)

function disegna(ora) {
  raf = 0
  if (!gpu && !ctx) return
  const t = (ora - inizio) / 1000
  // durata dell'ultimo fotogramma: la scia è lunga quanto lo spostamento in 1/60 di secondo, a qualunque frequenza dello schermo
  const dt = ultimoFotogramma ? Math.min(100, Math.max(4, ora - ultimoFotogramma)) : 16.7
  ultimoFotogramma = ora
  if (ctx) ctx.clearRect(0, 0, larghezza, altezza)

  // vista: ondeggia da sola (ben visibile) + puntatore, smorzata
  const fermo = ridotto || congelata.value
  const ozioX = fermo ? 0 : Math.sin((t * Math.PI * 2) / 16) * 0.5
  const ozioY = fermo ? 0 : Math.sin((t * Math.PI * 2) / 11) * 0.12
  if (congelata.value) {
    // pausa: elmo dritto, subito, senza inseguire il puntatore
    mira.x = mira.y = vista.x = vista.y = 0
  } else {
    vista.x += (mira.x + ozioX - vista.x) * 0.05
    vista.y += (mira.y + ozioY - vista.y) * 0.05
  }
  const cy = Math.cos(vista.x), sy = Math.sin(vista.x)
  const cx = Math.cos(vista.y), sx = Math.sin(vista.y)

  const scala = Math.min((larghezza * props.quota) / 1550, altezza / 1300)
  const f = 1100
  const ox = larghezza * props.centroX, oy = altezza * 0.5

  // ingresso: i punti arrivano sparsi e si ricompongono ruotando
  const arrivo = ridotto || congelata.value ? 1 : Math.min(1, t / INGRESSO)
  const raccolta = 1 - Math.pow(1 - arrivo, 4)
  const giro = (1 - raccolta) * 2.6
  const cg = Math.cos(giro), sg = Math.sin(giro)

  // passaggio automatico: se il mouse non si muove da 2,5 s, un puntatore invisibile scorre sull'elmo
  if (!fermo && arrivo >= 1 && ora - ultimoMovimento > 2500) {
    const lw = 1550 * Math.min((larghezza * props.quota) / 1550, altezza / 1300)
    const lh = 1300 * Math.min((larghezza * props.quota) / 1550, altezza / 1300)
    puntatore.x = larghezza * props.centroX + Math.sin(t * 0.55) * lw * 0.38
    puntatore.y = altezza * 0.5 + Math.sin(t * 0.9 + 1.2) * lh * 0.3
    puntatore.attivo = true
  }

  // scansione: un piano che attraversa l'elmo da sinistra a destra ogni 7 s
  const ciclo = ridotto ? 0.5 : (t % 7) / 7
  const piano = -820 + easeInOut(Math.min(1, ciclo / 0.7)) * 1640
  const scansioneAttiva = !congelata.value && (ridotto || ciclo < 0.7)

  // carica automatica del Paladino (prima volta subito dopo l'ingresso, poi ogni 14 s)
  if (!fermo && !carica.attiva && t >= carica.prossimo) {
    lanciaCarica(ora)
  }

  let cavAttivo = false, cavX = -9999, cavY = -9999
  if (carica.attiva && !fermo) {
    const pCar = (ora - carica.inizio) / 1000 / carica.durata
    if (pCar >= 1) {
      carica.attiva = false
      if (paladinoEl.value) paladinoEl.value.style.opacity = '0'
    } else {
      cavAttivo = true
      // Traiettoria coerente con l'angolo isometrico reale del Paladino (dir 6: ~22° verso sinistra-basso)
      const dxTot = larghezza + 220
      const dyTot = dxTot * 0.28
      cavX = larghezza + 95 - pCar * dxTot
      cavY = altezza * 0.42 - dyTot * 0.35 + pCar * dyTot
      const scalaCav = 0.96 + pCar * 0.16 // prospettiva naturale, nessun effetto "palloncino"
      const davanti = pCar > 0.38
      if (!carica.suonato && pCar > 0.44) {
        carica.suonato = true
        suonoRilancio()
      }
      if (paladinoEl.value) {
        paladinoEl.value.style.opacity = pCar < 0.08 ? String(pCar / 0.08) : pCar > 0.92 ? String((1 - pCar) / 0.08) : '1'
        paladinoEl.value.style.zIndex = davanti ? '3' : '0'
        paladinoEl.value.style.transform = `translate3d(${cavX}px, ${cavY}px, 0) translate(-50%, -50%) scale(${scalaCav.toFixed(3)})`
      }
    }
  } else if (paladinoEl.value) {
    paladinoEl.value.style.opacity = '0'
  }

  const raggio = 110, raggio2 = raggio * raggio
  const rCav = 165, rCav2 = rCav * rCav

  if (gpu) {
    gpu.disegna({
      larghezza, altezza, dpr, cy, sy, cx, sx, cg, sg, raccolta, scala, ox, oy, focale: f,
      scia: fermo ? 0 : Math.min(2, 16.7 / dt),
      puntatoreX: puntatore.x, puntatoreY: puntatore.y, puntatoreAttivo: puntatore.attivo && !ridotto && !congelata.value, raggio,
      cavalloX: cavX, cavalloY: cavY, cavalloAttivo: cavAttivo && !ridotto, raggioCavallo: rCav,
      piano, scansioneAttiva, azzera: congelata.value,
    })
    if (!ridotto && !congelata.value && visibile && !document.hidden) raf = requestAnimationFrame(disegna)
    else ultimoFotogramma = 0
    return
  }

  const secchi = { lo: [], mid: [], hi: [], verde: [], bianco: [], rosso: [], luce: [] }
  const p = dati

  for (let i = 0; i < n; i++) {
    const k = i * 4
    const sp = 1 - raccolta
    let x0 = p[k] + sparsi[i * 3] * sp, y = p[k + 1] + sparsi[i * 3 + 1] * sp, z0 = p[k + 2] + sparsi[i * 3 + 2] * sp
    // durante l'ingresso l'elmo gira su se stesso
    let x = x0 * cg + z0 * sg
    let z = -x0 * sg + z0 * cg
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
    if (puntatore.attivo && !ridotto && !congelata.value) {
      const dx = sxp - puntatore.x, dy = syp - puntatore.y
      const d2 = dx * dx + dy * dy
      if (d2 < raggio2 && d2 > 0.01) {
        const d = Math.sqrt(d2)
        const forza = (1 - d / raggio) * 28
        spostX[i] += (dx / d) * forza * 0.18
        spostY[i] += (dy / d) * forza * 0.18
      }
    }
    // il Paladino che sfonda l'elmo sparpaglia con forza le particelle dal petto del cavallo
    if (cavAttivo && !ridotto) {
      const dx = sxp - (cavX - 22), dy = syp - cavY
      const d2 = dx * dx + dy * dy
      if (d2 < rCav2 && d2 > 0.01) {
        const d = Math.sqrt(d2)
        const forza = (1 - d / rCav) * 72
        spostX[i] += ((dx / d) * 0.8 - 0.5) * forza * 0.22
        spostY[i] += ((dy / d) * 0.95 - 0.1) * forza * 0.22
      }
    }
    spostX[i] *= 0.93
    spostY[i] *= 0.93
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

  if (!ridotto && !congelata.value && visibile && !document.hidden) raf = requestAnimationFrame(disegna)
  else ultimoFotogramma = 0
}

function avvia() {
  if (!raf && dati && pronto.value) raf = requestAnimationFrame(disegna)
}

function muovi(e) {
  if (congelata.value) return // in pausa l'elmo non reagisce al puntatore
  const r = contenitore.value.getBoundingClientRect()
  const px = (e.clientX - r.left) / r.width - 0.5
  const py = (e.clientY - r.top) / r.height - 0.5
  mira.x = px * 0.9
  mira.y = -py * 0.45
  puntatore.x = e.clientX - r.left
  puntatore.y = e.clientY - r.top
  puntatore.attivo = true
  ultimoMovimento = performance.now()
  if (congelata.value || ridotto) avvia()
}
function esci() {
  mira.x = 0
  mira.y = 0
  puntatore.attivo = false
}

// azzera quello che era in corso: carica del Paladino, passaggio automatico, punti spostati
function azzera() {
  carica.attiva = false
  puntatore.attivo = false
  spostX?.fill(0)
  spostY?.fill(0)
  gpu?.azzera()
}

// un clic sull'elmo fa partire subito la carica del Paladino
function clic() {
  lanciaCarica(performance.now())
  avvia()
}

// alla ripresa si riparte da fermo: niente carica subito, il puntatore automatico aspetta
function riprendi() {
  carica.prossimo = (performance.now() - inizio) / 1000 + 2.8
  ultimoMovimento = performance.now()
}

function pausa() {
  inPausa.value = !inPausa.value
  if (inPausa.value) azzera()
  else riprendi()
  avvia()
}

// parte solo quando i punti sono caricati e si sa se l'animazione è accesa
function partenza() {
  if (!datiPronti || pronto.value || props.animata === null) return
  pronto.value = true
  inizio = performance.now()
  avvia()
}

watch(() => props.animata, (nuovo, vecchio) => {
  if (vecchio === null) {
    partenza()
    return
  }
  if (nuovo === false) azzera()
  else riprendi()
  avvia()
})

let osservatore, mq, ro
function cambiaVisibilita() {
  if (!document.hidden) avvia()
}

// sceglie il motore: WebGPU se c'è, altrimenti 2D; poi colori e misure
async function preparaMotore() {
  gpu = await creaElmoGpu(tela.value, { n, dati, sparsi, perso: passaAl2D })
  if (!gpu) ctx = tela.value.getContext('2d')
  motore.value = gpu ? 'webgpu' : '2d'
  leggiColori()
  dimensiona()
}

// la GPU si è persa (driver, risparmio energetico): si ricrea la tela e si continua in 2D
async function passaAl2D() {
  gpu = null
  ctx = null
  chiaveTela.value++
  await nextTick()
  ctx = tela.value.getContext('2d')
  motore.value = '2d'
  leggiColori()
  dimensiona()
  avvia()
}

// il tema chiaro/scuro cambia i colori dei punti
let osservaTema
onMounted(async () => {
  mq = window.matchMedia('(prefers-reduced-motion: reduce)')
  ridotto = mq.matches
  mq.addEventListener('change', (e) => { ridotto = e.matches; avvia() })
  ro = new ResizeObserver(() => { dimensiona(); avvia() })
  ro.observe(contenitore.value)
  osservaTema = new MutationObserver(() => { leggiColori(); avvia() })
  osservaTema.observe(document.documentElement, { attributes: true, attributeFilter: ['data-tema'] })
  try {
    const r = await fetch('/elmo-punti.json')
    const j = await r.json()
    dati = Int16Array.from(j.p)
    n = j.n
    spostX = new Float32Array(n)
    spostY = new Float32Array(n)
    // punti di partenza sparsi in una sfera larga
    sparsi = new Float32Array(n * 3)
    for (let i = 0; i < n; i++) {
      const u = Math.random() * 2 - 1, a = Math.random() * Math.PI * 2, r = 900 + Math.random() * 900
      const q = Math.sqrt(1 - u * u)
      sparsi[i * 3] = q * Math.cos(a) * r
      sparsi[i * 3 + 1] = q * Math.sin(a) * r
      sparsi[i * 3 + 2] = u * r
    }
  } catch {
    return
  }
  await preparaMotore()
  osservatore = new IntersectionObserver(([v]) => { visibile = v.isIntersecting; avvia() })
  osservatore.observe(contenitore.value)
  document.addEventListener('visibilitychange', cambiaVisibilita)
  datiPronti = true
  partenza()
})

onBeforeUnmount(() => {
  cancelAnimationFrame(raf)
  osservatore?.disconnect()
  ro?.disconnect()
  osservaTema?.disconnect()
  document.removeEventListener('visibilitychange', cambiaVisibilita)
  gpu?.distruggi()
  gpu = null
})
</script>

<template>
  <div
    ref="contenitore"
    class="nuvola"
    :class="{ 'nuvola--pronta': pronto }"
    :data-motore="motore || null"
    @pointermove="muovi"
    @pointerleave="esci"
    @click="clic"
  >
    <img
      ref="paladinoEl"
      class="nuvola__paladino"
      src="/sprite/paladino-carica.webp"
      alt=""
      aria-hidden="true"
      width="128"
      height="124"
    />
    <canvas :key="chiaveTela" ref="tela" role="img" aria-label="L'elmo di Age of Empires Italia disegnato con migliaia di punti, che si inclina seguendo il puntatore" />
    <button
      v-if="pronto && animata !== false"
      type="button"
      class="nuvola__pausa keep-motion"
      :aria-pressed="inPausa"
      :aria-label="inPausa ? 'Riprendi animazione' : 'Ferma animazione'"
      :title="inPausa ? 'Riprendi' : 'Ferma'"
      @click.stop="pausa"
    >
      <!-- quadrato rosso = stop; in pausa compare il triangolo = riprendi -->
      <span v-if="inPausa" class="nuvola__play" aria-hidden="true"></span>
      <span v-else class="nuvola__stop" aria-hidden="true"></span>
    </button>
  </div>
</template>
