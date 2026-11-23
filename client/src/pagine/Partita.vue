<script setup>
import { ref, computed, watch } from 'vue'
import { api, elo, delta, dataConOra } from '../api.js'

const props = defineProps({ id: { type: String, required: true } })

const d = ref(null)
const errore = ref('')

watch(
  () => props.id,
  async (id) => {
    d.value = null
    errore.value = ''
    try {
      d.value = await api(`partite/${id}`)
    } catch (e) {
      errore.value = e.message
    }
  },
  { immediate: true },
)

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
    { nome: 'Perdenti', classe: 'perso', giocatori: p, media: media(p) },
  ]
})
</script>

<template>
  <div v-if="errore" class="stato errore">{{ errore }}</div>
  <div v-else-if="!d" class="stato">Caricamento…</div>
  <template v-else>
    <p class="piccolo"><RouterLink to="/partite">← Partite</RouterLink></p>
    <h1>
      <span v-if="d.partita.dito" class="etichetta-dito">Dito #{{ d.partita.dito }}</span>
      <span v-else>Partita</span>
    </h1>
    <p class="tenue">
      {{ dataConOra(d.partita.startedAt) }} · {{ d.partita.durataInGameMinuti }} min di gioco
      <template v-if="d.partita.mapName"> · {{ d.partita.mapName }}</template>
      <template v-if="!d.partita.valida"> · non valida per l'ELO</template>
    </p>

    <div class="due-colonne">
      <section v-for="s in squadre" :key="s.nome">
        <h2 :class="s.classe">{{ s.nome }} <span class="tenue piccolo">media {{ elo(s.media) }}</span></h2>
        <div class="tabella-contenitore">
          <table>
            <thead>
              <tr><th>Giocatore</th><th>Civiltà</th><th class="num">ELO</th></tr>
            </thead>
            <tbody>
              <tr v-for="g in s.giocatori" :key="g.aoe2Id">
                <td>
                  <RouterLink v-if="g.playerId" :to="`/giocatore/${g.playerId}`">{{ g.nick }}</RouterLink>
                  <span v-else class="tenue">{{ g.nick || g.aoe2Id }}</span>
                </td>
                <td>{{ g.civ || '—' }}</td>
                <td class="num">
                  {{ elo(g.eloBefore) }}
                  <span class="piccolo" :class="g.eloDelta > 0 ? 'vinto' : 'perso'">{{ delta(g.eloDelta) }}</span>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>
    </div>

    <p class="tenue piccolo">
      Partita <a :href="`https://www.aoe2insights.com/match/${d.partita.idGame}/`" target="_blank" rel="noopener">{{ d.partita.idGame }} ↗</a>
    </p>
  </template>
</template>
