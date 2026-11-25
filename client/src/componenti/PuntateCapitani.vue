<script setup>
// Riquadro delle puntate: un capitano per riga, con il nome e fino a 5 spazi per le sue puntate (le ultime 5).
import { ref, computed, onMounted } from 'vue'
import { api } from '../api.js'
import StatoDati from './StatoDati.vue'

const SPAZI = 5

const capitani = ref([])
const caricamento = ref(true)
const errore = ref('')

// PROVVISORIO: finché non c'è la tabella delle puntate, valori a caso da 1 a 150, sempre crescenti
function puntateCasuali() {
  const quante = Math.floor(Math.random() * (SPAZI + 1))
  const valori = new Set()
  while (valori.size < quante) valori.add(1 + Math.floor(Math.random() * 150))
  return [...valori].sort((a, b) => a - b)
}

async function carica() {
  caricamento.value = true
  errore.value = ''
  try {
    const squadre = await api('asta/squadre')
    capitani.value = squadre
      .filter((s) => s.capitano)
      .map((s) => ({ id: s.id, nome: s.capitano, puntate: puntateCasuali() }))
  } catch (e) {
    errore.value = e.message
  } finally {
    caricamento.value = false
  }
}
onMounted(carica)

// giocatore su cui si sta facendo l'asta: per ora non c'è ancora una fonte, resta vuoto
const giocatoreInAsta = ref('')
// puntata massima attuale: la più alta tra quelle fatte dai capitani
const puntataMassima = computed(() => Math.max(0, ...capitani.value.flatMap((c) => c.puntate)))

// offerta a rialzo: per ora i tasti non inviano nulla (manca la tabella delle puntate)
const offerta = ref('')
const offertaValida = computed(() => Number.isInteger(offerta.value) && offerta.value > 0)

// gli spazi della riga: le ultime SPAZI puntate, completate con spazi vuoti
const spazi = (c) => Array.from({ length: SPAZI }, (_, i) => c.puntate.slice(-SPAZI)[i] ?? null)
</script>

<template>
  <section class="puntate" aria-labelledby="puntate-titolo">
    <h2 id="puntate-titolo" class="puntate__titolo">Puntate</h2>
    <StatoDati
      :caricamento="caricamento"
      :errore="errore"
      :vuoto="!capitani.length"
      :righe="6"
      testo-vuoto="Non c'è ancora nessun capitano."
      @riprova="carica"
    >
      <ul class="puntate__elenco">
        <li v-for="c in capitani" :key="c.id">
          <span class="puntate__nome" :title="c.nome">{{ c.nome }}</span>
          <span v-for="(v, i) in spazi(c)" :key="i" class="puntate__spazio" :class="{ vuoto: v === null }">{{ v }}</span>
        </li>
      </ul>
      <dl class="puntate__stato">
        <div><dt>Giocatore:</dt><dd>{{ giocatoreInAsta || '—' }}</dd></div>
        <div><dt>Puntata massima:</dt><dd>{{ puntataMassima }}</dd></div>
      </dl>
      <div class="puntate__offerta" role="group" aria-labelledby="offerta-titolo">
        <p id="offerta-titolo" class="puntate__etichetta">Fai un'offerta</p>
        <div class="puntate__rialzi">
          <button type="button" class="btn">+1</button>
          <button type="button" class="btn">+3</button>
          <button type="button" class="btn">+5</button>
        </div>
        <form class="puntate__libera" @submit.prevent>
          <input
            v-model.number="offerta"
            type="number"
            min="1"
            step="1"
            class="campo"
            placeholder="Importo"
            aria-label="Importo dell'offerta"
          />
          <button type="submit" class="btn" :disabled="!offertaValida">Ok</button>
        </form>
      </div>
    </StatoDati>
  </section>
</template>

<style scoped>
.puntate {
  display: flex; flex-direction: column; gap: var(--space-sm); height: 100%; min-height: 0; padding: var(--space-sm);
  background: var(--color-paper-2); border: var(--rule-hair) solid var(--color-rule-2); border-radius: var(--radius-card);
}
.puntate__titolo { margin: 0; font-size: var(--text-lg); font-weight: 600; text-align: center; }
.puntate__elenco { list-style: none; margin: 0; padding: 0; flex: 0 1 auto; min-height: 0; overflow: auto; font-size: var(--text-sm); }
.puntate__elenco li {
  display: grid; grid-template-columns: minmax(6rem, 1.4fr) repeat(5, minmax(2.2rem, 1fr)); align-items: center; gap: var(--space-xs);
  padding: var(--space-2xs, 4px) var(--space-xs); border-bottom: var(--rule-hair) dashed var(--color-rule);
}
.puntate__stato { margin: 0; padding-top: var(--space-sm); border-top: var(--rule-hair) solid var(--color-rule-2); display: grid; gap: var(--space-xs); font-size: var(--text-md); }
.puntate__stato div { display: flex; align-items: baseline; gap: var(--space-sm); }
.puntate__stato dt { color: var(--color-ink-2); }
.puntate__stato dd { margin: 0; font-weight: 700; font-variant-numeric: tabular-nums; }
.puntate__offerta { display: grid; gap: var(--space-sm); padding-top: var(--space-sm); border-top: var(--rule-hair) solid var(--color-rule-2); }
.puntate__etichetta { margin: 0; font-size: var(--text-md); color: var(--color-ink-2); }
.puntate__rialzi { display: grid; grid-template-columns: repeat(3, 1fr); gap: var(--space-sm); }
.puntate__libera { display: flex; gap: var(--space-sm); }
.puntate__libera .campo { flex: 1 1 auto; min-width: 0; }
.puntate__nome { min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-weight: 600; }
.puntate__spazio {
  text-align: center; padding: var(--space-2xs, 4px) 0; border: var(--rule-hair) solid var(--color-rule-2); border-radius: var(--radius-card);
  background: var(--color-paper); font-variant-numeric: tabular-nums; min-height: 1.8em;
}
.puntate__spazio.vuoto { border-style: dashed; background: transparent; }
</style>
