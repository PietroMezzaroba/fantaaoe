// Motore audio Web Audio API di FantaAge:
// - suonoIngresso(): fiamma/fuoco + accordo medievale di quinta aperta (Re-La-Re)
// - suonoRilancio(): tintinnio moneta Berlu
// - suonoTick(): battito ultimi 5 secondi
// - suonoAggiudicato(): martelletto + accordo finale
import { ref } from 'vue'

// Impostazione del profilo (utenti_info.audio_attivo, pagina /profilo): se è spenta il controllo audio non compare
// e non parte nessun suono. Predefinito acceso; il valore vero arriva dal server dopo il login (applicaAudioUtente).
export const audioAbilitato = ref(true)
// Dal profilo: chiede al controllo audio di partire subito appena compare (l'utente ha appena cliccato)
export const avvioRichiesto = ref(false)

// Muto col pulsante in alto (resta nel browser): attivo a meno che l'utente non l'abbia spento
const mutoNelBrowser = () => localStorage.getItem('fantaage_muto') === '1'
export const audioAttivo = ref(!mutoNelBrowser())
export const volumeAudio = ref(Number(localStorage.getItem('fantaage_vol_v3') ?? 22))

// Allinea l'audio alla preferenza salvata dell'utente, senza far partire niente.
export function applicaAudioUtente(abilitato) {
  audioAbilitato.value = abilitato
  audioAttivo.value = abilitato && !mutoNelBrowser()
}

// Cambio fatto dall'utente col suo switch: se lo accende la musica parte subito.
export function impostaAudioAbilitato(acceso) {
  audioAbilitato.value = acceso
  audioAttivo.value = acceso
  if (acceso) {
    localStorage.removeItem('fantaage_muto')
    avvioRichiesto.value = true
  }
}

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

// Effetto fuoco/fiamma + accordo medievale di benvenuto (bello pieno e presente)
export function suonoIngresso() {
  const c = ottieniCtx()
  if (!c) return
  const now = c.currentTime
  // Il suono di benvenuto si sente bene (almeno ~0.75 anche se il sottofondo OST è basso al 10%)
  const vol = volumeAudio.value <= 0 ? 0 : Math.max(0.75, scalaGain())

  // 1) Fiamma (rumore caldo filtrato con sweep passa-banda + crepitii)
  const durFuoco = 1.5
  const buf = c.createBuffer(1, Math.floor(c.sampleRate * durFuoco), c.sampleRate)
  const data = buf.getChannelData(0)
  let ultimo = 0
  for (let i = 0; i < data.length; i++) {
    const bianco = Math.random() * 2 - 1
    ultimo = (ultimo + 0.09 * bianco) / 1.09
    const scintilla = Math.random() > 0.995 ? (Math.random() * 2 - 1) * 0.55 : 0
    data[i] = ultimo * 2.5 + scintilla
  }
  const src = c.createBufferSource()
  src.buffer = buf

  const filtro = c.createBiquadFilter()
  filtro.type = 'bandpass'
  filtro.frequency.setValueAtTime(200, now)
  filtro.frequency.exponentialRampToValueAtTime(850, now + 0.32)
  filtro.frequency.exponentialRampToValueAtTime(280, now + durFuoco)
  filtro.Q.value = 1.0

  const gFuoco = c.createGain()
  gFuoco.gain.setValueAtTime(0.001, now)
  gFuoco.gain.linearRampToValueAtTime(0.42 * vol, now + 0.16)
  gFuoco.gain.exponentialRampToValueAtTime(0.001, now + durFuoco)

  src.connect(filtro).connect(gFuoco).connect(c.destination)
  src.start(now)
  src.stop(now + durFuoco)

  // 2) Accordo medievale di benvenuto (D3, A3, D4, F#4) pieno e caldo
  const note = [146.83, 220.0, 293.66, 369.99]
  note.forEach((freq, idx) => {
    const t0 = now + 0.22 + idx * 0.075
    const osc = c.createOscillator()
    osc.type = idx === 0 ? 'sawtooth' : 'triangle'
    osc.frequency.setValueAtTime(freq, t0)

    const lp = c.createBiquadFilter()
    lp.type = 'lowpass'
    lp.frequency.setValueAtTime(1600, t0)
    lp.frequency.exponentialRampToValueAtTime(480, t0 + 1.6)

    const g = c.createGain()
    g.gain.setValueAtTime(0.0001, t0)
    g.gain.linearRampToValueAtTime((idx === 0 ? 0.14 : 0.18) * vol, t0 + 0.05)
    g.gain.exponentialRampToValueAtTime(0.0001, t0 + 1.65)

    osc.connect(lp).connect(g).connect(c.destination)
    osc.start(t0)
    osc.stop(t0 + 1.7)
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

// Pietra del trabucco in volo (NuvolaElmo): un fischio che scende per tutta la durata del volo
export function suonoFischio(durata = 1.1) {
  if (!audioAttivo.value) return
  const c = ottieniCtx()
  if (!c) return
  const now = c.currentTime
  const osc = c.createOscillator()
  osc.type = 'sine'
  osc.frequency.setValueAtTime(1500, now)
  osc.frequency.exponentialRampToValueAtTime(420, now + durata)
  const g = c.createGain()
  g.gain.setValueAtTime(0.0001, now)
  g.gain.linearRampToValueAtTime(0.03 * scalaGain(), now + durata * 0.6)
  g.gain.exponentialRampToValueAtTime(0.0001, now + durata)
  osc.connect(g).connect(c.destination)
  osc.start(now)
  osc.stop(now + durata + 0.05)
}

// La pietra colpisce: un tonfo basso e un soffio di detriti (rumore filtrato)
export function suonoImpatto() {
  if (!audioAttivo.value) return
  const c = ottieniCtx()
  if (!c) return
  const now = c.currentTime
  const vol = scalaGain()
  const osc = c.createOscillator()
  osc.type = 'sine'
  osc.frequency.setValueAtTime(95, now)
  osc.frequency.exponentialRampToValueAtTime(32, now + 0.45)
  const g = c.createGain()
  g.gain.setValueAtTime(0.5 * vol, now)
  g.gain.exponentialRampToValueAtTime(0.001, now + 0.5)
  osc.connect(g).connect(c.destination)
  osc.start(now)
  osc.stop(now + 0.55)
  const n = Math.floor(c.sampleRate * 0.5)
  const buf = c.createBuffer(1, n, c.sampleRate)
  const dati = buf.getChannelData(0)
  for (let i = 0; i < n; i++) dati[i] = (Math.random() * 2 - 1) * Math.pow(1 - i / n, 2)
  const rumore = c.createBufferSource()
  rumore.buffer = buf
  const filtro = c.createBiquadFilter()
  filtro.type = 'lowpass'
  filtro.frequency.setValueAtTime(1800, now)
  filtro.frequency.exponentialRampToValueAtTime(300, now + 0.45)
  const gr = c.createGain()
  gr.gain.setValueAtTime(0.18 * vol, now)
  rumore.connect(filtro).connect(gr).connect(c.destination)
  rumore.start(now)
}

// Colpo di spada sul paladino (PaladinoAoe): un clang metallico corto
export function suonoColpo() {
  if (!audioAttivo.value) return
  const c = ottieniCtx()
  if (!c) return
  const now = c.currentTime
  const vol = scalaGain()
  ;[1567, 2349, 3136].forEach((freq, i) => {
    const osc = c.createOscillator()
    osc.type = i ? 'triangle' : 'square'
    osc.frequency.setValueAtTime(freq, now)
    osc.frequency.exponentialRampToValueAtTime(freq * 0.92, now + 0.18)
    const g = c.createGain()
    g.gain.setValueAtTime(0.0001, now)
    g.gain.linearRampToValueAtTime((i ? 0.05 : 0.035) * vol, now + 0.004)
    g.gain.exponentialRampToValueAtTime(0.0001, now + 0.22 + i * 0.05)
    osc.connect(g).connect(c.destination)
    osc.start(now)
    osc.stop(now + 0.32)
  })
}

// Il paladino cade: un tonfo basso che si spegne
export function suonoCaduta() {
  if (!audioAttivo.value) return
  const c = ottieniCtx()
  if (!c) return
  const now = c.currentTime
  const vol = scalaGain()
  const osc = c.createOscillator()
  osc.type = 'sine'
  osc.frequency.setValueAtTime(120, now)
  osc.frequency.exponentialRampToValueAtTime(38, now + 0.5)
  const g = c.createGain()
  g.gain.setValueAtTime(0.4 * vol, now)
  g.gain.exponentialRampToValueAtTime(0.001, now + 0.55)
  osc.connect(g).connect(c.destination)
  osc.start(now)
  osc.stop(now + 0.6)
}

let ultimoHover = 0
// Micro-tick tattile elegante al passaggio del mouse su bottoni e link
export function suonoHover() {
  if (!audioAttivo.value) return
  const t = performance.now()
  if (t - ultimoHover < 55) return
  ultimoHover = t
  const c = ottieniCtx()
  if (!c || c.state !== 'running') return
  const now = c.currentTime
  const vol = Math.max(0.06, scalaGain() * 0.28)

  const osc = c.createOscillator()
  osc.type = 'sine'
  osc.frequency.setValueAtTime(520, now)
  osc.frequency.exponentialRampToValueAtTime(780, now + 0.032)

  const g = c.createGain()
  g.gain.setValueAtTime(0.0001, now)
  g.gain.linearRampToValueAtTime(vol * 0.22, now + 0.006)
  g.gain.exponentialRampToValueAtTime(0.0001, now + 0.04)

  osc.connect(g).connect(c.destination)
  osc.start(now)
  osc.stop(now + 0.045)
}
