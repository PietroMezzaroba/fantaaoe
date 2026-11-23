// Connessione SSE condivisa per l'asta dal vivo (Capitano, Admin, Streamer, Overlay OBS).
import { ref, onMounted, onUnmounted } from 'vue'
import { api } from './api.js'

export function usaAstaLive() {
  const dati = ref(null)
  const connesso = ref(false)
  const errore = ref(null)
  const secondiRimasti = ref(null)
  let es = null
  let reconnectTimer = null
  let tickTimer = null

  function aggiornaCountdown() {
    const scad = dati.value?.scadenzaIl
    if (!scad || dati.value?.stato !== 'in_corso') {
      secondiRimasti.value = null
      return
    }
    const diff = Math.ceil((new Date(scad).getTime() - Date.now()) / 1000)
    secondiRimasti.value = Math.max(0, diff)
  }

  async function ricarica() {
    try {
      dati.value = await api('asta/stato')
      errore.value = null
      aggiornaCountdown()
    } catch (e) {
      errore.value = e.message
    }
  }

  function connetti() {
    if (es) es.close()
    es = new EventSource('/api/asta/stream')
    es.onopen = () => {
      connesso.value = true
      errore.value = null
    }
    es.onmessage = (ev) => {
      try {
        dati.value = JSON.parse(ev.data)
        connesso.value = true
        errore.value = null
        aggiornaCountdown()
      } catch {
        /* ignora messaggi malformati */
      }
    }
    es.onerror = () => {
      connesso.value = false
      es?.close()
      es = null
      clearTimeout(reconnectTimer)
      reconnectTimer = setTimeout(connetti, 3000)
    }
  }

  onMounted(() => {
    ricarica()
    connetti()
    tickTimer = setInterval(aggiornaCountdown, 250)
  })

  onUnmounted(() => {
    clearTimeout(reconnectTimer)
    clearInterval(tickTimer)
    es?.close()
  })

  return { dati, connesso, errore, secondiRimasti, ricarica }
}
