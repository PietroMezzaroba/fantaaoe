<script setup>
import { computed, watchEffect } from 'vue'
import { trovaRuolo } from '../ruoli.js'

const props = defineProps({ ruolo: { type: String, required: true } })
const r = computed(() => trovaRuolo(props.ruolo))

watchEffect(() => {
  if (r.value) document.title = `${r.value.nome}, FantaAoE`
})
</script>

<template>
  <div v-if="!r" class="avviso">
    <strong>Questo pannello non esiste.</strong>
    <RouterLink to="/" class="btn btn--linea">Torna ai pannelli</RouterLink>
  </div>
  <template v-else>
    <nav class="briciole" aria-label="Percorso">
      <RouterLink to="/">Pannelli</RouterLink>
      <span aria-hidden="true">/</span>
      <span aria-current="page">{{ r.nome }}</span>
    </nav>

    <div class="ruolo">
      <section aria-labelledby="titolo-ruolo">
        <div class="testa-pagina">
          <h1 id="titolo-ruolo">{{ r.nome }}</h1>
          <p>{{ r.intro }}</p>
        </div>
        <h2>Cosa troverai qui</h2>
        <ul class="funzioni">
          <li v-for="[titolo, testo] in r.funzioni" :key="titolo">
            <strong>{{ titolo }}</strong>
            <span>{{ testo }}</span>
          </li>
        </ul>
      </section>

      <aside class="accesso angoli" aria-labelledby="titolo-accesso">
        <h2 id="titolo-accesso">Si entra con Discord</h2>
        <p>
          Il sito riconosce il tuo ruolo dal server Discord di AoE Italia, senza password nuove.
          Il login è la prossima cosa che colleghiamo.
        </p>
        <button type="button" class="btn" aria-disabled="true">Accedi con Discord</button>
        <p>Intanto classifica, partite e civiltà sono aperte a tutti.</p>
        <RouterLink to="/classifica" class="btn btn--linea">Apri la classifica</RouterLink>
      </aside>
    </div>
  </template>
</template>
