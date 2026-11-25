<script setup>
// Radice dell'app. In sviluppo (npm run dev) mostra l'app solo se il server locale risponde:
// altrimenti una pagina "server non raggiungibile" che riprova da sola. In produzione è trasparente.
import { ref, onMounted, onBeforeUnmount } from 'vue'
import App from './App.vue'
import ServerNonRaggiungibile from './componenti/ServerNonRaggiungibile.vue'

const guardia = import.meta.env.MODE === 'development'
const raggiungibile = ref(!guardia)
const controllo = ref(false)
let timer = null

async function verifica() {
  controllo.value = true
  try {
    const r = await fetch('/api/salute', { cache: 'no-store' })
    raggiungibile.value = r.ok
  } catch {
    raggiungibile.value = false
  } finally {
    controllo.value = false
  }
  if (raggiungibile.value && timer) {
    clearInterval(timer)
    timer = null
  }
}

onMounted(() => {
  if (!guardia) return
  verifica()
  timer = setInterval(verifica, 3000)
})
onBeforeUnmount(() => timer && clearInterval(timer))
</script>

<template>
  <App v-if="raggiungibile" />
  <ServerNonRaggiungibile v-else :controllo="controllo" @riprova="verifica" />
</template>
