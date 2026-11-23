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
  <header class="barra">
    <div class="container barra__dentro">
      <RouterLink to="/" class="marchio" aria-label="FantaAoE, torna ai pannelli">
        <img class="marchio__elmo" src="/logo-elmo.png" alt="" width="40" height="40" />
        <span class="marchio__testo">
          <span class="marchio__nome">FantaAoE</span>
          <span class="marchio__sotto">Age of Empires Italia</span>
        </span>
      </RouterLink>
      <nav class="menu" aria-label="Sezioni">
        <RouterLink to="/" exact-active-class="router-link-active" active-class="">Pannelli</RouterLink>
        <RouterLink to="/classifica">Classifica</RouterLink>
        <RouterLink to="/partite">Partite</RouterLink>
        <RouterLink to="/civilta">Civiltà</RouterLink>
        <a v-if="handbook" :href="handbook" target="_blank" rel="noopener">Handbook</a>
      </nav>
      <button
        type="button"
        class="btn btn--linea barra__accedi"
        aria-disabled="true"
        title="Il login con Discord arriva nella prossima versione"
      >
        Accedi<span class="accedi-lungo">&nbsp;con Discord</span>
      </button>
    </div>
  </header>

  <main class="container pagina">
    <RouterView />
  </main>

  <footer class="piede">
    <div class="container">
      <span>Dati della classifica interna dei TG Dito e delle ranked di Age of Empires II</span>
      <span>Community Age of Empires Italia</span>
    </div>
  </footer>
</template>
