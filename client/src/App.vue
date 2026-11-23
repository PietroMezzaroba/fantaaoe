<script setup>
import { ref, onMounted } from 'vue'
import { api } from './api.js'

const handbook = ref(null)
onMounted(async () => {
  try {
    handbook.value = (await api('info')).handbook
  } catch {
    /* senza info il menu resta senza handbook */
  }
})
</script>

<template>
  <header class="testata">
    <RouterLink to="/" class="marchio">
      <img src="/icona.svg" alt="" width="28" height="28" />
      <span>FantaAoE</span>
    </RouterLink>
    <nav class="menu">
      <RouterLink to="/">Classifica</RouterLink>
      <RouterLink to="/partite">Partite</RouterLink>
      <RouterLink to="/civilta">Civiltà</RouterLink>
      <a v-if="handbook" :href="handbook" target="_blank" rel="noopener">Handbook ↗</a>
    </nav>
  </header>
  <main class="contenuto">
    <RouterView />
  </main>
  <footer class="piede">Age of Empires Italia · dati della classifica interna e delle ranked</footer>
</template>
