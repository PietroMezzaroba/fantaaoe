<script setup>
import { ref, onMounted, onUnmounted } from 'vue'
import { audioAttivo, volumeAudio, suonoIngresso } from '../suoni.js'

const tracce = [
  { id: 'jrrr7w4wz18', nome: 'Age of Kings (1999)', inizio: 20 },
  { id: 'O9mxrurGTqo', nome: 'The Conquerors', inizio: 0 },
]

const indice = ref(0)
const aperto = ref(false)
const ytPronto = ref(false)
const partito = ref(false)
let player = null
let avvioTimer = null

function caricaApiYouTube() {
  return new Promise((resolve) => {
    if (window.YT?.Player) return resolve()
    const prev = window.onYouTubeIframeAPIReady
    window.onYouTubeIframeAPIReady = () => {
      if (prev) prev()
      resolve()
    }
    if (!document.getElementById('yt-iframe-api')) {
      const s = document.createElement('script')
      s.id = 'yt-iframe-api'
      s.src = 'https://www.youtube.com/iframe_api'
      document.head.appendChild(s)
    }
  })
}

async function assicuraPlayer() {
  if (player && ytPronto.value) return player
  await caricaApiYouTube()
  return new Promise((resolve) => {
    if (player && ytPronto.value) return resolve(player)
    player = new window.YT.Player('fanta-yt-audio', {
      height: '1',
      width: '1',
      videoId: tracce[indice.value].id,
      playerVars: {
        autoplay: 0,
        controls: 0,
        loop: 1,
        start: tracce[indice.value].inizio,
        playlist: tracce[indice.value].id,
        playsinline: 1,
      },
      events: {
        onReady: () => {
          ytPronto.value = true
          player.setVolume(volumeAudio.value)
          resolve(player)
        },
        onStateChange: (e) => {
          if (e.data === 0 && audioAttivo.value) cambiaTraccia()
        },
      },
    })
  })
}

async function avviaAlClick() {
  if (!audioAttivo.value || partito.value) return
  partito.value = true
  rimuoviListenerSblocco()

  // 1) Suono iniziale (fuoco + accordo) sempre pulito dall'inizio al volume impostato (default 10%)
  suonoIngresso()

  // 2) Subito dopo il suono di benvenuto parte piano la OST di Age of Kings da 0:20 con fade-in morbido
  const p = await assicuraPlayer()
  clearTimeout(avvioTimer)
  avvioTimer = setTimeout(() => {
    if (!audioAttivo.value) return
    try {
      const start = tracce[indice.value].inizio || 0
      p.seekTo(start, true)
      p.setVolume(0)
      p.playVideo()
      const target = volumeAudio.value
      let passo = 0
      const fade = setInterval(() => {
        passo++
        if (!audioAttivo.value || passo >= 10) {
          clearInterval(fade)
          if (audioAttivo.value) try { p.setVolume(volumeAudio.value) } catch {}
          return
        }
        try { p.setVolume(Math.round((target * passo) / 10)) } catch {}
      }, 120)
    } catch {}
  }, 1350)
}

function alPrimoGesto(e) {
  // Se l'utente clicca proprio il bottone Audio/OST, lascia gestire a toggleAudio()
  if (e?.target?.closest?.('.audio-ctl')) return
  if (!audioAttivo.value || partito.value) return
  avviaAlClick()
}

function aggiungiListenerSblocco() {
  window.addEventListener('pointerdown', alPrimoGesto, { passive: true })
  window.addEventListener('keydown', alPrimoGesto, { passive: true })
  window.addEventListener('touchstart', alPrimoGesto, { passive: true })
}

function rimuoviListenerSblocco() {
  window.removeEventListener('pointerdown', alPrimoGesto)
  window.removeEventListener('keydown', alPrimoGesto)
  window.removeEventListener('touchstart', alPrimoGesto)
}

async function toggleAudio() {
  clearTimeout(avvioTimer)
  if (audioAttivo.value && partito.value) {
    audioAttivo.value = false
    partito.value = false
    localStorage.setItem('fantaage_muto', '1')
    rimuoviListenerSblocco()
    try { player?.pauseVideo() } catch {}
    return
  }

  audioAttivo.value = true
  localStorage.removeItem('fantaage_muto')
  partito.value = false
  await avviaAlClick()
}

async function cambiaTraccia() {
  indice.value = (indice.value + 1) % tracce.length
  if (!audioAttivo.value) return
  const p = await assicuraPlayer()
  try {
    p.loadVideoById({ videoId: tracce[indice.value].id, startSeconds: tracce[indice.value].inizio || 0 })
    p.setVolume(volumeAudio.value)
  } catch {}
}

function cambiaVolume(e) {
  const v = Number(e.target.value)
  volumeAudio.value = v
  localStorage.setItem('fantaage_vol_v2', String(v))
  try { player?.setVolume(v) } catch {}
}

onMounted(() => {
  if (!audioAttivo.value) return
  // Pre-carica YouTube fermo (autoplay: 0) e aspetta il primo click per far partire tutto pulito dall'inizio
  assicuraPlayer()
  aggiungiListenerSblocco()
})

onUnmounted(() => {
  clearTimeout(avvioTimer)
  rimuoviListenerSblocco()
  try { player?.destroy() } catch {}
})
</script>

<template>
  <div class="audio-ctl">
    <button
      type="button"
      class="audio-btn"
      :class="{ 'audio-btn--on': audioAttivo }"
      :aria-pressed="audioAttivo"
      :title="audioAttivo ? `Spegni audio (${tracce[indice].nome})` : 'Accendi suono d\'ingresso e OST Age of Kings'"
      @click="toggleAudio"
    >
      <svg v-if="audioAttivo" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">
        <polygon points="11 5 6 9 2 9 2 15 6 15 11 19 11 5" />
        <path d="M15.54 8.46a5 5 0 0 1 0 7.07" />
        <path d="M19.07 4.93a10 10 0 0 1 0 14.14" />
      </svg>
      <svg v-else width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">
        <polygon points="11 5 6 9 2 9 2 15 6 15 11 19 11 5" />
        <line x1="23" y1="9" x2="17" y2="15" />
        <line x1="17" y1="9" x2="23" y2="15" />
      </svg>
      <span class="audio-btn__testo">{{ audioAttivo ? 'OST' : 'Audio' }}</span>
    </button>

    <button
      v-if="audioAttivo"
      type="button"
      class="audio-mini"
      title="Controlli musica e volume"
      :aria-expanded="aperto"
      @click="aperto = !aperto"
    >▾</button>

    <div v-if="audioAttivo && aperto" class="audio-pop angoli" role="region" aria-label="Controlli colonna sonora">
      <span class="piccolo muto">In riproduzione</span>
      <strong class="piccolo">{{ tracce[indice].nome }}</strong>
      <label class="audio-vol">
        <span class="piccolo muto">Volume {{ volumeAudio }}%</span>
        <input type="range" min="0" max="100" :value="volumeAudio" @input="cambiaVolume" />
      </label>
      <div class="azioni-inline">
        <button type="button" class="btn btn--linea btn--sm" @click="cambiaTraccia">Cambia OST</button>
        <button type="button" class="btn btn--linea btn--sm" @click="suonoIngresso()">Riprova fuoco</button>
      </div>
    </div>

    <!-- Player YouTube nascosto (solo audio in sottofondo) -->
    <div class="yt-nascosto" aria-hidden="true">
      <div id="fanta-yt-audio"></div>
    </div>
  </div>
</template>
