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
import { suonoRilancio, suonoFischio, suonoImpatto, suonoCaduta } from '../suoni.js'
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
const telaEffetti = ref(null) // pietra del trabucco, scia, onda e scintille (2D, sopra i punti)
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
const carica = { attiva: false, inizio: 0, durata: 5.2, prossimo: 2.8, suonato: false, x: -9999, y: -9999, scala: 1, davanti: false,
  morto: null, mira: null, sparato: false }

// Il paladino della carica è quello vero di AoE2 DE, rosso (il nemico): sprite di PaladinoAoe (public/sprite/paladino/),
// riga 3 = direzione 6 del gioco, verso sinistra e in basso. Se una pietra del trabucco lo prende, muore e resta a terra.
const SPRITE = '/sprite/paladino/'
const RIGA_CARICA = 3
const SCALA_SPRITE = 1.25
const TELA_PAL = { l: 200, a: 150 }
const sprite = { meta: null, img: {}, pronti: false }
let cpal = null

function caricaSprite() {
  fetch(SPRITE + 'paladino-rosso.json')
    .then((r) => r.json())
    .then((meta) => {
      sprite.meta = meta
      return Promise.all(['passo', 'morte', 'cadavere'].map((nome) => new Promise((ok) => {
        const im = new Image()
        im.onload = im.onerror = ok
        im.src = SPRITE + meta.animazioni[nome].file
        sprite.img[nome] = im
      })))
    })
    .then(() => { sprite.pronti = true })
    .catch(() => { /* senza sprite la carica non parte, il resto dell'elmo funziona */ })
}

// disegna un fotogramma del paladino sulla sua tela (ancorata al punto d'appoggio, un po' sotto il centro)
function disegnaPaladino(nome, fotogramma) {
  if (!cpal || !sprite.pronti) return
  const a = sprite.meta.animazioni[nome]
  const img = sprite.img[nome]
  const [cw, ch] = a.cella
  const [ax, ay] = a.ancora
  cpal.clearRect(0, 0, TELA_PAL.l, TELA_PAL.a)
  const f = Math.min(a.fotogrammi - 1, Math.max(0, fotogramma))
  cpal.drawImage(img, f * cw, RIGA_CARICA * ch, cw, ch,
    TELA_PAL.l / 2 - ax * SCALA_SPRITE, TELA_PAL.a / 2 + 22 - ay * SCALA_SPRITE, cw * SCALA_SPRITE, ch * SCALA_SPRITE)
}

// posizione del paladino a un certo punto della carica (0..1)
function posizioneCarica(p) {
  const dxTot = larghezza + 220
  const dyTot = dxTot * 0.28
  return [larghezza + 95 - p * dxTot, altezza * 0.42 - dyTot * 0.35 + p * dyTot]
}

// Trabucco: ogni tanto (e a ogni clic) una pietra infuocata arriva da fuori, in alto a destra, e cade sull'elmo.
// All'impatto i punti saltano via (spinta nel motore) e quelli vicini diventano brace per un attimo.
const PIETRA_VOLO = 1150 // ms
const URTO = 260 // ms: quanto dura la spinta sui punti
const CALORE = 1300 // ms: quanto restano accese le braci
const trabucco = { pietre: [], braci: [], onde: [], impatto: null, prossimo: 7.5 }
let cfx = null

function lanciaPietra(ora, x, y) {
  if (ridotto || congelata.value || !larghezza) return
  // bersaglio: dove si è cliccato, oppure un punto a caso sull'elmo
  const lw = 1550 * Math.min((larghezza * props.quota) / 1550, altezza / 1300)
  const lh = 1300 * Math.min((larghezza * props.quota) / 1550, altezza / 1300)
  const tx = x ?? larghezza * props.centroX + (Math.random() - 0.5) * lw * 0.55
  const ty = y ?? altezza * 0.5 + (Math.random() - 0.4) * lh * 0.45
  const sx = larghezza + 60 + Math.random() * 80
  const sy = -80 - Math.random() * 60
  trabucco.pietre.push({ inizio: ora, sx, sy, tx, ty, cx: (sx + tx) / 2 + 40, cy: Math.min(sy, ty) - altezza * 0.35, rot: Math.random() * 6 })
  trabucco.prossimo = (ora - inizio) / 1000 + 11 + Math.random() * 6
  suonoFischio(PIETRA_VOLO / 1000)
}

function bezier(p, t) {
  const u = 1 - t
  return [u * u * p.sx + 2 * u * t * p.cx + t * t * p.tx, u * u * p.sy + 2 * u * t * p.cy + t * t * p.ty]
}

// fa avanzare pietre, braci e onde; restituisce l'impatto in corso per il motore dei punti
function aggiornaTrabucco(ora, dt) {
  for (const p of trabucco.pietre) {
    const t = Math.min(1, (ora - p.inizio) / PIETRA_VOLO)
    const [x, y] = bezier(p, t)
    p.x = x; p.y = y; p.t = t
    p.rot += dt * 0.012
    // scia di fuoco e fumo dietro la pietra
    for (let k = 0; k < 2; k++) {
      trabucco.braci.push({ x: x + (Math.random() - 0.5) * 6, y: y + (Math.random() - 0.5) * 6, vx: (Math.random() - 0.5) * 0.03, vy: -0.02 - Math.random() * 0.03,
        vita: 1, durata: 380 + Math.random() * 380, r: 3 + Math.random() * 4, fumo: Math.random() < 0.35 })
    }
    if (t >= 1) {
      p.finita = true
      trabucco.impatto = { x: p.tx, y: p.ty, inizio: ora }
      // preso! (il paladino in carica, se la pietra gli cade addosso)
      if (carica.attiva && !carica.morto && Math.abs(p.tx - carica.x) < 75 && Math.abs(p.ty - (carica.y - 20)) < 60) {
        carica.morto = ora
        suonoCaduta()
      }
      trabucco.onde.push({ x: p.tx, y: p.ty, inizio: ora })
      for (let k = 0; k < 34; k++) {
        const a = Math.random() * Math.PI * 2, v = 0.12 + Math.random() * 0.32
        trabucco.braci.push({ x: p.tx, y: p.ty, vx: Math.cos(a) * v, vy: Math.sin(a) * v - 0.12, g: 0.0006,
          vita: 1, durata: 500 + Math.random() * 700, r: 1.5 + Math.random() * 2.5, fumo: false })
      }
      suonoImpatto()
    }
  }
  trabucco.pietre = trabucco.pietre.filter((p) => !p.finita)
  for (const b of trabucco.braci) {
    b.x += b.vx * dt; b.y += b.vy * dt
    if (b.g) b.vy += b.g * dt
    b.vita -= dt / b.durata
  }
  trabucco.braci = trabucco.braci.filter((b) => b.vita > 0)
  trabucco.onde = trabucco.onde.filter((o) => ora - o.inizio < 700)
  const imp = trabucco.impatto
  if (!imp) return null
  const passato = ora - imp.inizio
  if (passato > CALORE) { trabucco.impatto = null; return null }
  return { x: imp.x, y: imp.y, forza: passato < URTO ? 190 * (1 - passato / URTO) : 0, calore: Math.pow(1 - passato / CALORE, 1.5) }
}

function disegnaTrabucco(ora) {
  if (!cfx) return
  cfx.clearRect(0, 0, larghezza, altezza)
  // onde d'urto: un anello che si allarga e sbiadisce
  for (const o of trabucco.onde) {
    const t = (ora - o.inizio) / 700
    cfx.globalAlpha = (1 - t) * 0.7
    cfx.strokeStyle = colori.fuoco
    cfx.lineWidth = 3 * (1 - t) + 0.5
    cfx.beginPath()
    cfx.ellipse(o.x, o.y, 20 + t * 150, (20 + t * 150) * 0.55, 0, 0, Math.PI * 2)
    cfx.stroke()
    // lampo al centro, solo nei primi istanti
    if (t < 0.25) {
      const g = cfx.createRadialGradient(o.x, o.y, 0, o.x, o.y, 90)
      g.addColorStop(0, colori.fuocoChiaro)
      g.addColorStop(1, 'transparent')
      cfx.globalAlpha = (1 - t / 0.25) * 0.8
      cfx.fillStyle = g
      cfx.fillRect(o.x - 90, o.y - 90, 180, 180)
    }
  }
  // braci e fumo
  for (const b of trabucco.braci) {
    cfx.globalAlpha = Math.max(0, b.vita) * (b.fumo ? 0.35 : 0.9)
    cfx.fillStyle = b.fumo ? colori.fumo : b.vita > 0.5 ? colori.fuocoChiaro : colori.fuoco
    cfx.beginPath()
    cfx.arc(b.x, b.y, b.fumo ? b.r * (1.6 - b.vita) : b.r * b.vita + 0.4, 0, Math.PI * 2)
    cfx.fill()
  }
  // la pietra: un sasso scuro con il cuore che brucia
  for (const p of trabucco.pietre) {
    if (p.x == null) continue
    const r = 9 + p.t * 5
    const g = cfx.createRadialGradient(p.x, p.y, 0, p.x, p.y, r * 2.6)
    g.addColorStop(0, colori.fuocoChiaro)
    g.addColorStop(0.35, colori.fuoco)
    g.addColorStop(1, 'transparent')
    cfx.globalAlpha = 0.85
    cfx.fillStyle = g
    cfx.fillRect(p.x - r * 2.6, p.y - r * 2.6, r * 5.2, r * 5.2)
    cfx.globalAlpha = 1
    cfx.save()
    cfx.translate(p.x, p.y)
    cfx.rotate(p.rot)
    cfx.fillStyle = colori.pietra
    cfx.beginPath()
    for (let k = 0; k < 7; k++) {
      const a = (k / 7) * Math.PI * 2, rr = r * (0.78 + ((k * 37) % 5) / 18)
      k ? cfx.lineTo(Math.cos(a) * rr, Math.sin(a) * rr) : cfx.moveTo(Math.cos(a) * rr, Math.sin(a) * rr)
    }
    cfx.closePath()
    cfx.fill()
    cfx.restore()
  }
  cfx.globalAlpha = 1
}

function lanciaCarica(ora) {
  if (ridotto || congelata.value) return
  if (!sprite.pronti) { carica.prossimo = (ora - inizio) / 1000 + 1; return }
  carica.morto = null
  carica.sparato = false
  // due cariche su tre il trabucco prova a colpirlo, in un momento a caso della prima metà del percorso
  carica.mira = Math.random() < 0.66 ? 0.12 + Math.random() * 0.3 : null
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
    fuoco: v('--color-fuoco'), fuocoChiaro: v('--color-fuoco-chiaro'), fumo: v('--color-muted'), pietra: v('--color-pietra'),
  }
  gpu?.colori(colori)
}

function dimensiona() {
  if (!contenitore.value || !tela.value) return
  const r = contenitore.value.getBoundingClientRect()
  dpr = Math.min(2, window.devicePixelRatio || 1)
  larghezza = r.width
  altezza = r.height
  if (telaEffetti.value) {
    telaEffetti.value.width = Math.round(larghezza * dpr)
    telaEffetti.value.height = Math.round(altezza * dpr)
    cfx = telaEffetti.value.getContext('2d')
    cfx.setTransform(dpr, 0, 0, dpr, 0, 0)
  }
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
  if (carica.attiva && !fermo && carica.morto) {
    // colpito dal trabucco: animazione di morte del gioco, poi il cadavere che sbiadisce, fermo dove è caduto
    const s = (ora - carica.morto) / 1000
    const morte = sprite.meta.animazioni.morte, cad = sprite.meta.animazioni.cadavere
    const tMorte = morte.fotogrammi / morte.fps
    // da morto sta davanti ai punti, così si vede anche se è caduto mentre era dietro l'elmo
    if (paladinoEl.value) paladinoEl.value.style.zIndex = '3'
    if (s < tMorte) {
      disegnaPaladino('morte', Math.floor(s * morte.fps))
    } else if (s < tMorte + 3) {
      disegnaPaladino('cadavere', Math.floor((s - tMorte) * cad.fps))
      if (paladinoEl.value) paladinoEl.value.style.opacity = String(Math.max(0, 1 - (s - tMorte) / 3))
    } else {
      carica.attiva = false
      carica.morto = null
      carica.x = carica.y = -9999
      if (paladinoEl.value) paladinoEl.value.style.opacity = '0'
    }
  } else if (carica.attiva && !fermo) {
    const pCar = (ora - carica.inizio) / 1000 / carica.durata
    if (pCar >= 1) {
      carica.attiva = false
      carica.x = carica.y = -9999
      if (paladinoEl.value) paladinoEl.value.style.opacity = '0'
    } else {
      cavAttivo = true
      // Traiettoria coerente con l'angolo isometrico reale del Paladino (dir 6: ~22° verso sinistra-basso)
      ;[cavX, cavY] = posizioneCarica(pCar)
      const scalaCav = 0.96 + pCar * 0.16 // prospettiva naturale, nessun effetto "palloncino"
      const davanti = pCar > 0.38
      carica.x = cavX; carica.y = cavY; carica.scala = scalaCav; carica.davanti = davanti
      if (!carica.suonato && pCar > 0.44) {
        carica.suonato = true
        suonoRilancio()
      }
      // il trabucco mira dove sarà il paladino quando la pietra arriva
      if (carica.mira != null && !carica.sparato && pCar >= carica.mira) {
        carica.sparato = true
        const [mx, my] = posizioneCarica(pCar + PIETRA_VOLO / 1000 / carica.durata)
        lanciaPietra(ora, mx, my - 20)
      }
      const passo = sprite.meta.animazioni.passo
      disegnaPaladino('passo', Math.floor(((ora - carica.inizio) / 1000) * passo.fps) % passo.fotogrammi)
      if (paladinoEl.value) {
        paladinoEl.value.style.opacity = pCar < 0.08 ? String(pCar / 0.08) : pCar > 0.92 ? String((1 - pCar) / 0.08) : '1'
        paladinoEl.value.style.zIndex = davanti ? '3' : '0'
        paladinoEl.value.style.transform = `translate3d(${cavX}px, ${cavY}px, 0) translate(-50%, -50%) scale(${scalaCav.toFixed(3)})`
      }
    }
  } else if (paladinoEl.value) {
    paladinoEl.value.style.opacity = '0'
  }

  // trabucco: prima pietra qualche secondo dopo l'ingresso, poi ogni 11-17 s, mai mentre passa il paladino
  if (!fermo && arrivo >= 1 && !carica.attiva && t >= trabucco.prossimo) lanciaPietra(ora)
  if (carica.attiva && carica.morto) trabucco.prossimo = Math.max(trabucco.prossimo, t + 5)
  const imp = fermo ? null : aggiornaTrabucco(ora, dt)
  disegnaTrabucco(ora)

  const raggio = 110, raggio2 = raggio * raggio
  const rCav = 165, rCav2 = rCav * rCav

  if (gpu) {
    gpu.disegna({
      larghezza, altezza, dpr, cy, sy, cx, sx, cg, sg, raccolta, scala, ox, oy, focale: f,
      scia: fermo ? 0 : Math.min(2, 16.7 / dt),
      puntatoreX: puntatore.x, puntatoreY: puntatore.y, puntatoreAttivo: puntatore.attivo && !ridotto && !congelata.value, raggio,
      cavalloX: cavX, cavalloY: cavY, cavalloAttivo: cavAttivo && !ridotto, raggioCavallo: rCav,
      piano, scansioneAttiva, azzera: congelata.value,
      impattoX: imp?.x, impattoY: imp?.y, impattoForza: imp?.forza, impattoCalore: imp?.calore,
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
    // la pietra del trabucco spinge via i punti vicini
    if (imp?.forza) {
      const dx = sxp - imp.x, dy = syp - imp.y
      const d2 = dx * dx + dy * dy
      if (d2 < 30625 && d2 > 0.01) {
        const d = Math.sqrt(d2)
        const forza = (1 - d / 175) * imp.forza * 0.22
        spostX[i] += (dx / d) * forza
        spostY[i] += (dy / d - 0.25) * forza
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
  carica.morto = null
  trabucco.pietre = []
  trabucco.braci = []
  trabucco.onde = []
  trabucco.impatto = null
  cfx?.clearRect(0, 0, larghezza, altezza)
  puntatore.attivo = false
  spostX?.fill(0)
  spostY?.fill(0)
  gpu?.azzera()
}

// un clic sull'elmo fa partire subito la carica del Paladino
function clic(e) {
  const r = contenitore.value.getBoundingClientRect()
  lanciaPietra(performance.now(), e.clientX - r.left, e.clientY - r.top)
  avvia()
}

// alla ripresa si riparte da fermo: niente carica subito, il puntatore automatico aspetta
function riprendi() {
  carica.prossimo = (performance.now() - inizio) / 1000 + 2.8
  ultimoMovimento = performance.now()
  trabucco.prossimo = (performance.now() - inizio) / 1000 + 6
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
  // tela del paladino (sempre a doppia risoluzione: resta nitido anche sugli schermi retina)
  cpal = paladinoEl.value?.getContext('2d') ?? null
  cpal?.setTransform(2, 0, 0, 2, 0, 0)
  caricaSprite()
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
    <canvas
      ref="paladinoEl"
      class="nuvola__paladino"
      aria-hidden="true"
      :width="TELA_PAL.l * 2"
      :height="TELA_PAL.a * 2"
      :style="{ width: TELA_PAL.l + 'px', height: TELA_PAL.a + 'px' }"
    />
    <canvas ref="telaEffetti" class="nuvola__effetti" aria-hidden="true" />
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
