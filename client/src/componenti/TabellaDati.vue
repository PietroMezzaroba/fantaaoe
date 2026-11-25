<script setup>
// Tabella con filtro a riga: sotto l'intestazione c'è una riga di campi, uno per colonna, che restringono le righe mostrate.
//  - colonne: [{ chiave, titolo, classe?, formato?(riga) => testo, tipo?: 'bool' }]
//    "formato" decide cosa si vede (e su cosa filtra il campo di testo); senza, si usa riga[chiave].
//    tipo 'bool' mostra un menu Tutti / Sì / No invece del campo di testo.
//  - slot "cella-<chiave>" ({ riga }) sostituisce il contenuto di una cella; slot "azioni" ({ riga }) aggiunge una colonna di tasti.
import { reactive, computed } from 'vue'

const props = defineProps({
  colonne: { type: Array, required: true },
  righe: { type: Array, required: true },
  chiaveRiga: { type: [String, Function], required: true },
  titolo: { type: String, required: true }, // didascalia per gli screen reader
  conAzioni: Boolean,
  filtrabile: { type: Boolean, default: true }, // false: niente riga di filtri (tabelle con poche righe)
  etichettaSi: { type: String, default: 'Sì' },
  etichettaNo: { type: String, default: 'No' },
  // i filtri possono stare nel contenitore, così sopravvivono a ricariche dei dati e al cambio di scheda
  filtri: { type: Object, default: () => reactive({}) },
})

const testoCella = (c, riga) => {
  const v = c.formato ? c.formato(riga) : riga[c.chiave]
  return v === null || v === undefined ? '' : String(v)
}
const mostra = (c, riga) => testoCella(c, riga) || '—'
const idRiga = (riga) => (typeof props.chiaveRiga === 'function' ? props.chiaveRiga(riga) : riga[props.chiaveRiga])

const visibili = computed(() =>
  props.righe.filter((riga) =>
    props.colonne.every((c) => {
      const f = props.filtri[c.chiave]
      if (f === undefined || f === '') return true
      if (c.tipo === 'bool') return !!riga[c.chiave] === (f === 'si')
      return testoCella(c, riga).toLowerCase().includes(f.trim().toLowerCase())
    }),
  ),
)
const filtriAttivi = computed(() => Object.values(props.filtri).some((f) => f !== undefined && f !== ''))
const azzera = () => { for (const k of Object.keys(props.filtri)) props.filtri[k] = '' }
</script>

<template>
  <div class="tabella">
    <table>
      <caption class="visually-hidden">{{ titolo }}</caption>
      <thead>
        <tr>
          <th v-for="c in colonne" :key="c.chiave" scope="col" :class="c.classe?.includes('num') ? 'num' : c.classe?.includes('centro') ? 'centro' : ''">{{ c.titolo }}</th>
          <th v-if="conAzioni" scope="col" class="azioni-col"><span class="visually-hidden">Azioni</span></th>
        </tr>
        <tr v-if="filtrabile" class="filtri">
          <th v-for="c in colonne" :key="c.chiave" scope="col">
            <select v-if="c.tipo === 'bool'" v-model="filtri[c.chiave]" class="filtro" :aria-label="`Filtra per ${c.titolo}`">
              <option value="">Tutti</option>
              <option value="si">{{ etichettaSi }}</option>
              <option value="no">{{ etichettaNo }}</option>
            </select>
            <input
              v-else
              v-model="filtri[c.chiave]"
              type="search"
              class="filtro"
              placeholder="Filtra…"
              autocomplete="off"
              :aria-label="`Filtra per ${c.titolo}`"
            />
          </th>
          <th v-if="conAzioni" class="azioni-col">
            <button v-if="filtriAttivi" type="button" class="btn btn--linea filtri__azzera" @click="azzera">Azzera</button>
          </th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="riga in visibili" :key="idRiga(riga)">
          <td v-for="c in colonne" :key="c.chiave" :class="c.classe">
            <slot :name="`cella-${c.chiave}`" :riga="riga">{{ mostra(c, riga) }}</slot>
          </td>
          <td v-if="conAzioni" class="azioni"><slot name="azioni" :riga="riga" /></td>
        </tr>
        <tr v-if="!visibili.length">
          <td :colspan="colonne.length + (conAzioni ? 1 : 0)" class="muto nessuno">
            Nessun risultato con questi filtri.
            <button type="button" class="btn btn--linea" @click="azzera">Azzera i filtri</button>
          </td>
        </tr>
      </tbody>
    </table>
    <p v-if="filtriAttivi" class="conteggio muto" role="status">{{ visibili.length }} di {{ righe.length }} righe</p>
  </div>
</template>

<style scoped>
.filtri th { padding-block: var(--space-2xs, 4px) var(--space-xs); font-weight: 400; }
.filtro {
  width: 100%; min-width: 5.5rem; min-height: 32px; padding: 0 var(--space-xs); font: inherit; font-size: var(--text-sm);
  color: var(--color-ink); background: var(--color-paper); border: var(--rule-hair) solid var(--color-rule-2); border-radius: var(--radius-card);
}
.azioni { display: flex; align-items: center; justify-content: center; gap: var(--space-xs); white-space: nowrap; }
.azioni-col { width: 1%; white-space: nowrap; }
.filtri__azzera { min-height: 32px; padding-inline: var(--space-sm); font-size: var(--text-sm); }
/* tutti i testi delle tabelle Admin al centro (anche le colonne numeriche, che altrove stanno a destra) */
th, td, th.num, td.num, td.centro, th.centro { text-align: center; }
.filtro { text-align: center; }
.nessuno { text-align: center; padding: var(--space-lg); }
.nessuno .btn { margin-left: var(--space-xs); }
.conteggio { margin: var(--space-xs) 0 0; font-size: var(--text-sm); text-align: center; }
</style>
