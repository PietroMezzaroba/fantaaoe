<script setup>
import { ref, onUnmounted } from 'vue'
import { audioAttivo, volumeAudio, suonoIngresso } from '../suoni.js'

const tracce = [
  { id: 'jrrr7w4wz18', nome: 'Age of Kings (1999)' },
  { id: 'O9mxrurGTqo', nome: 'The Conquerors' },
]

const indice = ref(0)
const aperto = ref(false)
const ytPronto = ref(false)
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
          // 0 = ENDED -> passa alla traccia successiva
          if (e.data === 0 && audioAttivo.value) cambiaTraccia()
        },
      },
    })
  })
}

async function toggleAudio() {
  clearTimeout(avvioTimer)
  if (audioAttivo.value) {
    audioAttivo.value = false
    try { player?.pauseVideo() } catch {}
    return
  }

  audioAttivo.value = true
  suonoIngresso()

  const p = await assicuraPlayer()
  // Lascia sfumare il suono del fuoco/accordo prima della musica di AoE2
  avvioTimer = setTimeout(() => {
    if (!audioAttivo.value) return
    try {
      p.setVolume(volumeAudio.value)
      p.playVideo()
    } catch {}
  }, 1150)
}

async function cambiaTraccia() {
  indice.value = (indice.value + 1) % tracce.length
  if (!audioAttivo.value) return
  const p = await assicuraPlayer()
  try {
    p.loadVideoById(tracce[indice.value].id)
    p.setVolume(volumeAudio.value)
  } catch {}
}

function cambiaVolume(e) {
  const v = Number(e.target.value)
  volumeAudio.value = v
  localStorage.setItem('fantaage_vol', String(v))
  try { player?.setVolume(v) } catch {}
}

onUnmounted(() => {
  clearTimeout(avvioTimer)
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
