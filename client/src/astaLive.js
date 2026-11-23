// Connessione SSE condivisa per l'asta dal vivo (Capitano, Admin, Streamer, Overlay OBS).
import { ref, onMounted, onUnmounted } from 'vue'
import { api } from './api.js'

export function usaAstaLive() {
  const dati = ref(null)
  const connesso = ref(false)
  const errore = ref(null)
  let es = null
  let timer = null

  async function ricarica() {
    try {
      dati.value = await api('asta/stato')
      errore.value = null
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
      } catch {
        /* ignora messaggi malformati */
      }
    }
    es.onerror = () => {
      connesso.value = false
      es?.close()
      es = null
      clearTimeout(timer)
      timer = setTimeout(connetti, 3000)
    }
  }

  onMounted(() => {
    ricarica()
    connetti()
  })

  onUnmounted(() => {
    clearTimeout(timer)
    es?.close()
  })

  return { dati, connesso, errore, ricarica }
}
