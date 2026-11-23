<script setup>
import { ref, computed, watch } from 'vue'
import { api, elo, delta, giorno, ora } from '../api.js'
import StatoDati from '../componenti/StatoDati.vue'

const props = defineProps({ id: { type: String, required: true } })

const d = ref(null)
const caricamento = ref(true)
const errore = ref('')

async function carica() {
  caricamento.value = true
  errore.value = ''
  try {
    d.value = await api(`partite/${props.id}`)
    document.title = `${d.value.partita.dito ? `Dito ${d.value.partita.dito}` : 'Partita'}, FantaAge`
  } catch (e) {
    errore.value = e.message
  } finally {
    caricamento.value = false
  }
}
watch(() => props.id, carica, { immediate: true })

const squadre = computed(() => {
  if (!d.value) return []
  const media = (gs) => {
    const v = gs.map((g) => g.eloBefore).filter((x) => x != null)
    return v.length ? v.reduce((a, b) => a + b, 0) / v.length : null
  }
  const v = d.value.giocatori.filter((g) => g.vinto)
  const p = d.value.giocatori.filter((g) => !g.vinto)
  return [
    { nome: 'Vincitori', classe: 'vinto', giocatori: v, media: media(v) },
    { nome: 'Sconfitti', classe: 'perso', giocatori: p, media: media(p) },
  ]
})
</script>

<template>
  <nav class="briciole" aria-label="Percorso">
    <RouterLink to="/partite">Partite</RouterLink>
    <span aria-hidden="true">/</span>
    <span aria-current="page">{{ d?.partita.dito ? `Dito ${d.partita.dito}` : 'Partita' }}</span>
  </nav>

  <StatoDati :caricamento="caricamento" :errore="errore" :righe="8" @riprova="carica">
    <template v-if="d">
      <div class="testa-pagina">
        <h1>{{ d.partita.dito ? `Dito ${d.partita.dito}` : 'Partita' }}</h1>
        <p>
          {{ giorno(d.partita.startedAt) }} alle {{ ora(d.partita.startedAt) }}, {{ d.partita.durataInGameMinuti }}&thinsp;minuti di gioco<template v-if="d.partita.mapName">, mappa {{ d.partita.mapName }}</template>.
          <template v-if="!d.partita.valida"> Troppo corta per contare nell'ELO.</template>
        </p>
      </div>

      <div class="due">
        <section v-for="s in squadre" :key="s.nome" :aria-label="s.nome">
          <h2><span :class="s.classe">{{ s.nome }}</span></h2>
          <p class="muto piccolo" style="margin-bottom: var(--space-sm)">ELO medio prima della partita: <span class="num">{{ elo(s.media) }}</span></p>
          <div class="tabella">
            <table>
              <thead>
                <tr><th scope="col">Giocatore</th><th scope="col">Civiltà</th><th scope="col" class="num">ELO</th></tr>
              </thead>
              <tbody>
                <tr v-for="g in s.giocatori" :key="g.aoe2Id">
                  <td class="nome">
                    <RouterLink v-if="g.playerId" :to="`/giocatore/${g.playerId}`">{{ g.nick }}</RouterLink>
                    <span v-else class="muto">{{ g.nick || g.aoe2Id }}</span>
                  </td>
                  <td class="tenue">{{ g.civ || 'n.d.' }}</td>
                  <td class="num">
                    {{ elo(g.eloBefore) }}
                    <span class="clan" :class="g.eloDelta > 0 ? 'vinto' : 'perso'">{{ delta(g.eloDelta) }}</span>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>
      </div>

      <p class="muto piccolo sezione">
        Partita {{ d.partita.idGame }} su
        <a :href="`https://www.aoe2insights.com/match/${d.partita.idGame}/`" target="_blank" rel="noopener">AoE2 Insights</a>
      </p>
    </template>
  </StatoDati>
</template>
