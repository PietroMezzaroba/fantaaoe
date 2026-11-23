<script setup>
import { ref, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { api } from './api.js'
import { sessione, caricaSessione, linkAccesso } from './sessione.js'

const route = useRoute()
const handbook = ref(null)

onMounted(async () => {
  caricaSessione()
  try {
    handbook.value = (await api('info')).handbook
  } catch {
    /* senza info il menu resta senza handbook */
  }
})
</script>

<template>
  <RouterView v-if="route.meta.nudo" />
  <template v-else>
  <header class="barra">
    <div class="container barra__dentro">
      <RouterLink to="/" class="marchio" aria-label="FantaAge, pagina iniziale">
        <img class="marchio__elmo" src="/logo-elmo.png" alt="" width="40" height="40" />
        <span class="marchio__testo">
          <span class="marchio__nome">FantaAge</span>
          <span class="marchio__sotto">Age of Empires Italia</span>
        </span>
      </RouterLink>
      <nav class="menu" aria-label="Sezioni">
        <RouterLink to="/pannelli">Pannelli</RouterLink>
        <RouterLink to="/torneo">Torneo</RouterLink>
        <RouterLink to="/classifica">Classifica</RouterLink>
        <RouterLink to="/partite">Partite</RouterLink>
        <RouterLink to="/civilta">Civiltà</RouterLink>
        <a v-if="handbook" :href="handbook" target="_blank" rel="noopener">Handbook</a>
      </nav>

      <div class="barra__accedi">
        <div v-if="sessione.autenticato" class="utente">
          <img v-if="sessione.avatar" class="utente__foto" :src="sessione.avatar" alt="" width="32" height="32" />
          <span class="utente__nome">{{ sessione.nome }}</span>
          <a class="btn btn--linea" href="/api/auth/esci">Esci</a>
        </div>
        <a v-else-if="sessione.loginAttivo" class="btn btn--linea" :href="linkAccesso(route.fullPath)">
          Accedi<span class="accedi-lungo">con Discord</span>
        </a>
        <button
          v-else
          type="button"
          class="btn btn--linea"
          aria-disabled="true"
          title="Il login con Discord non è ancora attivo"
        >
          Accedi<span class="accedi-lungo">con Discord</span>
        </button>
      </div>
    </div>
  </header>

  <main :class="route.meta.pieno ? 'pieno' : 'container pagina'">
    <p v-if="route.query.accesso === 'annullato'" class="avviso-riga" :class="{ container: route.meta.pieno }" role="status">
      Accesso con Discord annullato. Puoi riprovare quando vuoi.
    </p>
    <RouterView />
  </main>

  <footer class="piede">
    <div class="container">
      <span>Dati della classifica interna dei TG Dito e delle ranked di Age of Empires II</span>
      <span>Community Age of Empires Italia</span>
    </div>
  </footer>
  </template>
</template>
