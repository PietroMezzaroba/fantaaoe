<script setup>
import { computed } from 'vue'
import { giorno, ora } from '../api.js'

const props = defineProps({ partita: Object, giocatori: Array })

const vincitori = computed(() => props.giocatori.filter((g) => g.vinto))
const perdenti = computed(() => props.giocatori.filter((g) => !g.vinto))
</script>

<template>
  <li class="partita-riga">
    <RouterLink :to="`/partita/${partita.idGame}`">
      <div class="partita-riga__quando">
        <span class="partita-riga__dito">{{ partita.dito ? `Dito ${partita.dito}` : 'Partita' }}</span>
        <span>{{ giorno(partita.startedAt) }}, {{ ora(partita.startedAt) }}</span>
        <span class="muto">
          {{ partita.durataInGameMinuti }}&thinsp;min<template v-if="!partita.valida">, non valida</template>
        </span>
      </div>
      <div class="squadre">
        <div class="squadra squadra--vinta">
          <span class="squadra__esito vinto">Vincitori</span>
          <span class="squadra__nomi">
            <span v-for="g in vincitori" :key="g.aoe2Id">{{ g.nick || g.aoe2Id }}</span>
          </span>
        </div>
        <div class="squadra">
          <span class="squadra__esito perso">Sconfitti</span>
          <span class="squadra__nomi">
            <span v-for="g in perdenti" :key="g.aoe2Id">{{ g.nick || g.aoe2Id }}</span>
          </span>
        </div>
      </div>
    </RouterLink>
  </li>
</template>
