<script setup>
// Tutte le civiltà dell'anagrafica (tabella fanta_torneo.civilta) in ordine alfabetico, ognuna con il suo stemma.
// Il nome in inglese è già scritto nello stemma: sotto si vede solo la descrizione.
// Lo stemma è il file client/public/civilta/<avatar>.png, dove <avatar> è il valore della colonna "avatar".
import { ref, reactive, onMounted } from 'vue'
import { api } from '../api.js'
import StatoDati from '../componenti/StatoDati.vue'

const civilta = ref([])
const caricamento = ref(true)
const errore = ref('')
const senzaStemma = reactive(new Set()) // avatar per cui il file non si trova

async function carica() {
  caricamento.value = true
  errore.value = ''
  try {
    const righe = await api('civilta')
    civilta.value = righe.sort((a, b) => a.nome.localeCompare(b.nome, 'it', { sensitivity: 'base' }))
  } catch (e) {
    errore.value = e.message
  } finally {
    caricamento.value = false
  }
}
onMounted(carica)
</script>

<template>
  <div class="testa-pagina">
    <h1>Civiltà</h1>
    <p>Tutte le civiltà di Age of Empires II, in ordine alfabetico.</p>
  </div>

  <StatoDati
    :caricamento="caricamento"
    :errore="errore"
    :vuoto="!civilta.length"
    :righe="8"
    testo-vuoto="Non c'è ancora nessuna civiltà."
    @riprova="carica"
  >
    <ul class="civ">
      <li v-for="c in civilta" :key="c.id" class="civ__scheda">
        <span class="civ__stemma">
          <span v-if="senzaStemma.has(c.avatar)" class="civ__iniziale" aria-hidden="true">{{ c.nome.charAt(0) }}</span>
          <img
            v-else
            :src="`/civilta/${encodeURIComponent(c.avatar)}.png`"
            :alt="`Stemma: ${c.nome}`"
            width="96"
            height="96"
            loading="lazy"
            @error="senzaStemma.add(c.avatar)"
          />
        </span>
        <span class="civ__descrizione">{{ c.descrizione || c.nome }}</span>
      </li>
    </ul>
  </StatoDati>
</template>

<style scoped>
.civ {
  display: grid; grid-template-columns: repeat(auto-fill, minmax(9.5rem, 1fr)); gap: var(--space-md);
  list-style: none; margin: 0; padding: 0;
}
.civ__scheda {
  display: grid; justify-items: center; align-content: start; gap: var(--space-xs); text-align: center;
  padding: var(--space-md) var(--space-sm); background: var(--color-paper-2);
  border: var(--rule-hair) solid var(--color-rule-2); border-radius: var(--radius-card);
}
.civ__stemma { display: grid; place-items: center; width: 96px; height: 96px; }
.civ__stemma img { width: 96px; height: 96px; object-fit: contain; }
.civ__iniziale {
  display: grid; place-items: center; width: 96px; height: 96px; border-radius: 50%;
  background: var(--color-paper-3); color: var(--color-muted); font-size: var(--text-3xl);
}
.civ__descrizione { color: var(--color-ink-2); overflow-wrap: anywhere; }
</style>
