<script setup>
// Riquadro con i giocatori ancora disponibili: ricerca per nome (anche parziale) e, sotto, l'elenco con l'ELO a fianco.
import { ref, computed, onMounted } from 'vue'
import { api } from '../api.js'
import StatoDati from './StatoDati.vue'

const giocatori = ref([])
const caricamento = ref(true)
const errore = ref('')
const ricerca = ref('')

async function carica() {
  caricamento.value = true
  errore.value = ''
  try {
    giocatori.value = await api('asta/giocatori-disponibili')
  } catch (e) {
    errore.value = e.message
  } finally {
    caricamento.value = false
  }
}
onMounted(carica)

// filtra per nome senza badare a maiuscole/minuscole e accenti
const normalizza = (t) => t.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase()
const visibili = computed(() => {
  const q = normalizza(ricerca.value.trim())
  return q ? giocatori.value.filter((g) => normalizza(g.nome).includes(q)) : giocatori.value
})
</script>

<template>
  <section class="disponibili" aria-labelledby="disponibili-titolo">
    <h2 id="disponibili-titolo" class="disponibili__titolo">Giocatori disponibili</h2>
    <input
      v-model="ricerca"
      type="search"
      class="campo disponibili__ricerca"
      placeholder="Cerca per nome"
      aria-label="Cerca un giocatore per nome"
      autocomplete="off"
    />
    <StatoDati
      :caricamento="caricamento"
      :errore="errore"
      :vuoto="!giocatori.length"
      :righe="6"
      testo-vuoto="Non ci sono giocatori disponibili."
      @riprova="carica"
    >
      <ul v-if="visibili.length" class="disponibili__elenco">
        <li v-for="g in visibili" :key="g.idDiscord">
          <span class="disponibili__nome" :title="g.nome">{{ g.nome }}</span>
          <span class="disponibili__elo">{{ g.elo }}</span>
        </li>
      </ul>
      <p v-else class="muto piccolo">Nessun giocatore corrisponde a «{{ ricerca }}».</p>
    </StatoDati>
  </section>
</template>

<style scoped>
.disponibili {
  display: flex; flex-direction: column; gap: var(--space-sm); height: 100%; min-height: 0; padding: var(--space-sm);
  background: var(--color-paper-2); border: var(--rule-hair) solid var(--color-rule-2); border-radius: var(--radius-card);
}
.disponibili__titolo { margin: 0; font-size: var(--text-lg); font-weight: 600; text-align: center; }
.disponibili__ricerca { width: 100%; }
.disponibili__elenco { list-style: none; margin: 0; padding: 0; flex: 1 1 auto; min-height: 0; overflow: auto; font-size: var(--text-sm); }
.disponibili__elenco li {
  display: flex; justify-content: space-between; gap: var(--space-sm); padding: var(--space-2xs, 4px) var(--space-xs);
  border-bottom: var(--rule-hair) dashed var(--color-rule);
}
.disponibili__nome { min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.disponibili__elo { font-variant-numeric: tabular-nums; font-weight: 600; }
</style>
