<script setup>
import { ref, computed, watch } from 'vue'
import { api, percento } from '../api.js'

const civ = ref([])
const periodo = ref(null)
const ordine = ref('partite')
const caricamento = ref(true)
const errore = ref('')

const periodi = [
  { valore: null, titolo: 'Sempre' },
  { valore: 365, titolo: 'Ultimo anno' },
  { valore: 90, titolo: '90 giorni' },
]

watch(
  periodo,
  async (g) => {
    caricamento.value = true
    errore.value = ''
    try {
      civ.value = await api(g ? `civ?giorni=${g}` : 'civ')
    } catch (e) {
      errore.value = e.message
    } finally {
      caricamento.value = false
    }
  },
  { immediate: true },
)

const totalePick = computed(() => civ.value.reduce((a, c) => a + c.partite, 0))
const righe = computed(() =>
  civ.value
    .map((c) => ({ ...c, winrate: c.partite ? (c.vittorie / c.partite) * 100 : 0, pick: totalePick.value ? (c.partite / totalePick.value) * 100 : 0 }))
    .sort((a, b) => b[ordine.value] - a[ordine.value] || b.partite - a.partite),
)
</script>

<template>
  <h1>Civiltà</h1>
  <p class="tenue piccolo">
    Quante volte è uscita ogni civiltà nei TG Dito validi e quanto vince. Con poche partite il winrate dice poco.
  </p>

  <div class="barra-strumenti">
    <div class="interruttori" role="group" aria-label="Periodo">
      <button v-for="p in periodi" :key="p.titolo" :class="{ attivo: periodo === p.valore }" @click="periodo = p.valore">
        {{ p.titolo }}
      </button>
    </div>
  </div>

  <div v-if="caricamento" class="stato">Caricamento…</div>
  <div v-else-if="errore" class="stato errore">{{ errore }}</div>
  <div v-else class="tabella-contenitore">
    <table>
      <thead>
        <tr>
          <th>Civiltà</th>
          <th class="ordinabile num" :class="{ ordinato: ordine === 'partite' }" @click="ordine = 'partite'">
            Partite<span v-if="ordine === 'partite'"> ▾</span>
          </th>
          <th class="num nascondi-mobile">Pick</th>
          <th class="ordinabile" :class="{ ordinato: ordine === 'winrate' }" @click="ordine = 'winrate'">
            Winrate<span v-if="ordine === 'winrate'"> ▾</span>
          </th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="c in righe" :key="c.civ">
          <td>{{ c.civ }}</td>
          <td class="num">{{ c.partite }}</td>
          <td class="num tenue nascondi-mobile">{{ c.pick.toFixed(1) }}%</td>
          <td>
            <div style="display: flex; align-items: center; gap: 8px">
              <div class="barra" style="flex: 1"><span :style="{ width: c.winrate + '%' }" /></div>
              <span class="num piccolo" style="min-width: 3.2em">{{ percento(c.winrate) }}</span>
            </div>
          </td>
        </tr>
      </tbody>
    </table>
  </div>
</template>
