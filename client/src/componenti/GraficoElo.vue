<script setup>
import { computed } from 'vue'
import { data, elo } from '../api.js'

// Andamento dell'ELO interno partita dopo partita: una linea su filetti, il punto attuale nel segnale.
const props = defineProps({ punti: { type: Array, required: true } })

const L = 720
const A = 200
const M = { s: 44, d: 8, a: 12, b: 26 }

const g = computed(() => {
  const p = props.punti
  if (p.length < 2) return null
  const valori = p.map((x) => x.elo)
  const vmin = Math.min(...valori)
  const vmax = Math.max(...valori)
  const margine = Math.max(20, (vmax - vmin) * 0.12)
  const min = vmin - margine
  const max = vmax + margine
  const x = (i) => M.s + (i / (p.length - 1)) * (L - M.s - M.d)
  const y = (v) => M.a + (1 - (v - min) / (max - min)) * (A - M.a - M.b)
  const linea = p.map((q, i) => `${i ? 'L' : 'M'}${x(i).toFixed(1)},${y(q.elo).toFixed(1)}`).join(' ')
  const ultimo = p[p.length - 1]
  return {
    linea,
    tacche: [vmax, (vmax + vmin) / 2, vmin].map((v) => ({ v, y: y(v) })),
    punto: { x: x(p.length - 1), y: y(ultimo.elo) },
    inizio: data(p[0].data),
    fine: data(ultimo.data),
    descrizione: `Da ${elo(p[0].elo)} a ${elo(ultimo.elo)} in ${p.length} partite valide, massimo ${elo(vmax)}, minimo ${elo(vmin)}.`,
  }
})
</script>

<template>
  <figure v-if="g" class="grafico angoli" style="margin: 0">
    <svg :viewBox="`0 0 ${L} ${A}`" width="100%" role="img" :aria-label="g.descrizione">
      <g v-for="t in g.tacche" :key="t.v">
        <line :x1="M.s" :x2="L - M.d" :y1="t.y" :y2="t.y" stroke="var(--color-rule)" stroke-width="1" />
        <text :x="M.s - 8" :y="t.y + 4" text-anchor="end" font-size="12">{{ elo(t.v) }}</text>
      </g>
      <path :d="g.linea" fill="none" stroke="var(--color-ink-2)" stroke-width="1.5" stroke-linejoin="round" />
      <line :x1="g.punto.x" :x2="g.punto.x" :y1="g.punto.y" :y2="A - M.b" stroke="var(--color-rule-2)" stroke-width="1" />
      <circle :cx="g.punto.x" :cy="g.punto.y" r="3.5" fill="var(--color-accent)" />
      <text :x="M.s" :y="A - 6" font-size="12">{{ g.inizio }}</text>
      <text :x="L - M.d" :y="A - 6" font-size="12" text-anchor="end">{{ g.fine }}</text>
    </svg>
    <figcaption class="visually-hidden">{{ g.descrizione }}</figcaption>
  </figure>
</template>
