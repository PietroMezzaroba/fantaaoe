// Motore audio Web Audio API di FantaAge:
// - suonoIngresso(): fiamma/fuoco + accordo medievale di quinta aperta (Re-La-Re)
// - suonoRilancio(): tintinnio moneta Berlu
// - suonoTick(): battito ultimi 5 secondi
// - suonoAggiudicato(): martelletto + accordo finale
import { ref } from 'vue'

// Attivo di default a meno che l'utente non lo abbia spento esplicitamente
export const audioAttivo = ref(localStorage.getItem('fantaage_muto') !== '1')
export const volumeAudio = ref(Number(localStorage.getItem('fantaage_vol_v2') ?? 10))

let ctx = null

function ottieniCtx() {
  if (!ctx) {
    const AudioCtx = window.AudioContext || window.webkitAudioContext
    if (!AudioCtx) return null
    ctx = new AudioCtx()
  }
  if (ctx.state === 'suspended') ctx.resume().catch(() => {})
  return ctx
}

export async function tentaSbloccoAudio() {
  const c = ottieniCtx()
  if (!c) return false
  if (c.state === 'suspended') {
    try { await c.resume() } catch {}
  }
  return c.state === 'running'
}

function scalaGain() {
  return Math.max(0.02, Math.min(1, volumeAudio.value / 100))
}

// Effetto fuoco/fiamma + accordo medievale d'ingresso
export function suonoIngresso() {
  const c = ottieniCtx()
  if (!c) return
  const now = c.currentTime
  const vol = scalaGain()

  // 1) Fiamma (rumore filtrato con sweep passa-banda + crepitio morbido)
  const durFuoco = 1.45
  const buf = c.createBuffer(1, Math.floor(c.sampleRate * durFuoco), c.sampleRate)
  const data = buf.getChannelData(0)
  let ultimo = 0
  for (let i = 0; i < data.length; i++) {
    const bianco = Math.random() * 2 - 1
    ultimo = (ultimo + 0.08 * bianco) / 1.08 // rumore caldo (brownish)
    const scintilla = Math.random() > 0.9965 ? (Math.random() * 2 - 1) * 0.45 : 0
    data[i] = ultimo * 2.2 + scintilla
  }
  const src = c.createBufferSource()
  src.buffer = buf

  const filtro = c.createBiquadFilter()
  filtro.type = 'bandpass'
  filtro.frequency.setValueAtTime(180, now)
  filtro.frequency.exponentialRampToValueAtTime(720, now + 0.35)
  filtro.frequency.exponentialRampToValueAtTime(260, now + durFuoco)
  filtro.Q.value = 1.1

  const gFuoco = c.createGain()
  gFuoco.gain.setValueAtTime(0.001, now)
  gFuoco.gain.linearRampToValueAtTime(0.32 * vol, now + 0.18)
  gFuoco.gain.exponentialRampToValueAtTime(0.001, now + durFuoco)

  src.connect(filtro).connect(gFuoco).connect(c.destination)
  src.start(now)
  src.stop(now + durFuoco)

  // 2) Accordo medievale aperto (D3, A3, D4, F#4) con timbro caldo a triangolo
  const note = [146.83, 220.0, 293.66, 369.99]
  note.forEach((freq, idx) => {
    const t0 = now + 0.25 + idx * 0.07
    const osc = c.createOscillator()
    osc.type = idx === 0 ? 'sawtooth' : 'triangle'
    osc.frequency.setValueAtTime(freq, t0)

    const lp = c.createBiquadFilter()
    lp.type = 'lowpass'
    lp.frequency.setValueAtTime(1200, t0)
    lp.frequency.exponentialRampToValueAtTime(420, t0 + 1.5)

    const g = c.createGain()
    g.gain.setValueAtTime(0.0001, t0)
    g.gain.linearRampToValueAtTime((idx === 0 ? 0.09 : 0.11) * vol, t0 + 0.06)
    g.gain.exponentialRampToValueAtTime(0.0001, t0 + 1.6)

    osc.connect(lp).connect(g).connect(c.destination)
    osc.start(t0)
    osc.stop(t0 + 1.65)
  })
}

// Tintinnio Berlu al rilancio
export function suonoRilancio() {
  if (!audioAttivo.value) return
  const c = ottieniCtx()
  if (!c) return
  const now = c.currentTime
  const vol = scalaGain() * 0.18
  ;[880, 1318.5].forEach((freq, i) => {
    const osc = c.createOscillator()
    osc.type = 'sine'
    osc.frequency.setValueAtTime(freq, now + i * 0.055)
    const g = c.createGain()
    g.gain.setValueAtTime(0.0001, now + i * 0.055)
    g.gain.linearRampToValueAtTime(vol, now + i * 0.055 + 0.01)
    g.gain.exponentialRampToValueAtTime(0.0001, now + i * 0.055 + 0.22)
    osc.connect(g).connect(c.destination)
    osc.start(now + i * 0.055)
    osc.stop(now + i * 0.055 + 0.25)
  })
}

// Tick discreto ultimi 5 secondi
export function suonoTick() {
  if (!audioAttivo.value) return
  const c = ottieniCtx()
  if (!c) return
  const now = c.currentTime
  const osc = c.createOscillator()
  osc.type = 'triangle'
  osc.frequency.setValueAtTime(620, now)
  const g = c.createGain()
  g.gain.setValueAtTime(0.0001, now)
  g.gain.linearRampToValueAtTime(scalaGain() * 0.12, now + 0.005)
  g.gain.exponentialRampToValueAtTime(0.0001, now + 0.07)
  osc.connect(g).connect(c.destination)
  osc.start(now)
  osc.stop(now + 0.08)
}

// Colpo di martelletto + accordo finale all'aggiudicazione
export function suonoAggiudicato() {
  if (!audioAttivo.value) return
  const c = ottieniCtx()
  if (!c) return
  const now = c.currentTime
  const vol = scalaGain()

  // Martelletto (thump breve)
  const kick = c.createOscillator()
  kick.type = 'sine'
  kick.frequency.setValueAtTime(160, now)
  kick.frequency.exponentialRampToValueAtTime(48, now + 0.14)
  const gk = c.createGain()
  gk.gain.setValueAtTime(0.35 * vol, now)
  gk.gain.exponentialRampToValueAtTime(0.001, now + 0.16)
  kick.connect(gk).connect(c.destination)
  kick.start(now)
  kick.stop(now + 0.18)

  // Accordo D maggiore
  ;[293.66, 369.99, 440, 587.33].forEach((freq, idx) => {
    const t0 = now + 0.1 + idx * 0.04
    const osc = c.createOscillator()
    osc.type = 'triangle'
    osc.frequency.setValueAtTime(freq, t0)
    const g = c.createGain()
    g.gain.setValueAtTime(0.0001, t0)
    g.gain.linearRampToValueAtTime(0.11 * vol, t0 + 0.03)
    g.gain.exponentialRampToValueAtTime(0.0001, t0 + 0.9)
    osc.connect(g).connect(c.destination)
    osc.start(t0)
    osc.stop(t0 + 0.95)
  })
}
