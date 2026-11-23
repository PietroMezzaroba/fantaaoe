<script setup>
import { ref, computed, watch } from 'vue'
import { api, elo, percento, fa } from '../api.js'

const giocatori = ref([])
const caricamento = ref(true)
const errore = ref('')
const tutti = ref(false)
const cerca = ref('')
const ordine = ref('eloInterno')

const colonne = [
  { chiave: 'eloInterno', titolo: 'Interno' },
  { chiave: 'elo1v1', titolo: '1v1' },
  { chiave: 'eloTg', titolo: 'TG' },
]

async function carica() {
  caricamento.value = true
  errore.value = ''
  try {
    giocatori.value = await api(`giocatori?tutti=${tutti.value}`)
  } catch (e) {
    errore.value = e.message
  } finally {
    caricamento.value = false
  }
}
watch(tutti, carica, { immediate: true })

const righe = computed(() => {
  const q = cerca.value.trim().toLowerCase()
  const k = ordine.value
  return giocatori.value
    .filter((g) => !q || g.nick.toLowerCase().includes(q) || (g.clan || '').toLowerCase().includes(q))
    .slice()
    .sort((a, b) => (b[k] ?? -1) - (a[k] ?? -1) || a.nick.localeCompare(b.nick))
})
</script>

<template>
  <h1>Classifica</h1>
  <p class="tenue piccolo">
    ELO interno dei TG Dito e ELO ranked attuale (per chi ha più account, il più alto). Tocca una colonna per ordinare.
  </p>

  <div class="barra-strumenti">
    <input v-model="cerca" class="cerca" type="search" placeholder="Cerca giocatore o clan…" aria-label="Cerca" />
    <div class="interruttori" role="group" aria-label="Quali giocatori">
      <button :class="{ attivo: !tutti }" @click="tutti = false">Attivi (90 gg)</button>
      <button :class="{ attivo: tutti }" @click="tutti = true">Tutti</button>
    </div>
  </div>

  <div v-if="caricamento" class="stato">Caricamento…</div>
  <div v-else-if="errore" class="stato errore">{{ errore }}</div>
  <div v-else class="tabella-contenitore">
    <table>
      <thead>
        <tr>
          <th>#</th>
          <th>Giocatore</th>
          <th
            v-for="c in colonne"
            :key="c.chiave"
            class="ordinabile num"
            :class="{ ordinato: ordine === c.chiave }"
            @click="ordine = c.chiave"
          >
            {{ c.titolo }}<span v-if="ordine === c.chiave"> ▾</span>
          </th>
          <th class="num nascondi-mobile">Diti</th>
          <th class="num nascondi-mobile">Vinte</th>
          <th class="nascondi-mobile">Ultimo Dito</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="(g, i) in righe" :key="g.playerId">
          <td class="pos num">{{ i + 1 }}</td>
          <td>
            <RouterLink :to="`/giocatore/${g.playerId}`">{{ g.nick }}</RouterLink>
            <span v-if="g.clan" class="clan">{{ g.clan }}</span>
          </td>
          <td class="num">{{ elo(g.eloInterno) }}</td>
          <td class="num">{{ elo(g.elo1v1) }}</td>
          <td class="num">{{ elo(g.eloTg) }}</td>
          <td class="num nascondi-mobile">{{ g.nDiti }}</td>
          <td class="num nascondi-mobile">{{ g.nDiti ? percento(g.winrate) : '—' }}</td>
          <td class="tenue nascondi-mobile">{{ fa(g.ultimoDito) }}</td>
        </tr>
        <tr v-if="!righe.length">
          <td colspan="8" class="stato">Nessun giocatore trovato</td>
        </tr>
      </tbody>
    </table>
  </div>
  <p v-if="!caricamento && !errore" class="tenue piccolo">{{ righe.length }} giocatori</p>
</template>
