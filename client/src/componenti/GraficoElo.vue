<script setup>
import { computed } from 'vue'
import { data, elo } from '../api.js'

// Andamento dell'ELO interno partita dopo partita (una linea, senza librerie).
const props = defineProps({ punti: { type: Array, required: true } })

const L = 640
const A = 180
const M = { s: 40, d: 8, a: 10, b: 22 }

const g = computed(() => {
  const p = props.punti
  if (p.length < 2) return null
  const valori = p.map((x) => x.elo)
  let min = Math.min(...valori)
  let max = Math.max(...valori)
  const margine = Math.max(20, (max - min) * 0.1)
  min -= margine
  max += margine
  const x = (i) => M.s + (i / (p.length - 1)) * (L - M.s - M.d)
  const y = (v) => M.a + (1 - (v - min) / (max - min)) * (A - M.a - M.b)
  const linea = p.map((q, i) => `${i ? 'L' : 'M'}${x(i).toFixed(1)},${y(q.elo).toFixed(1)}`).join(' ')
  const area = `${linea} L${x(p.length - 1).toFixed(1)},${A - M.b} L${x(0).toFixed(1)},${A - M.b} Z`
  const tacche = [max - margine, (max + min) / 2, min + margine].map((v) => ({ v, y: y(v) }))
  return {
    linea, area, tacche,
    ultimo: { x: x(p.length - 1), y: y(p[p.length - 1].elo) },
    inizio: data(p[0].data),
    fine: data(p[p.length - 1].data),
  }
})
</script>

<template>
  <div v-if="g" class="pannello">
    <svg :viewBox="`0 0 ${L} ${A}`" width="100%" role="img" aria-label="Andamento dell'ELO interno">
      <defs>
        <linearGradient id="riempimento" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stop-color="var(--oro)" stop-opacity=".28" />
          <stop offset="1" stop-color="var(--oro)" stop-opacity="0" />
        </linearGradient>
      </defs>
      <g v-for="t in g.tacche" :key="t.v">
        <line :x1="M.s" :x2="L - M.d" :y1="t.y" :y2="t.y" stroke="var(--bordo)" stroke-dasharray="3 4" />
        <text :x="M.s - 6" :y="t.y + 4" text-anchor="end" font-size="11" fill="var(--tenue)">{{ elo(t.v) }}</text>
      </g>
      <path :d="g.area" fill="url(#riempimento)" />
      <path :d="g.linea" fill="none" stroke="var(--oro)" stroke-width="2" stroke-linejoin="round" />
      <circle :cx="g.ultimo.x" :cy="g.ultimo.y" r="4" fill="var(--oro)" />
      <text :x="M.s" :y="A - 6" font-size="11" fill="var(--tenue)">{{ g.inizio }}</text>
      <text :x="L - M.d" :y="A - 6" font-size="11" fill="var(--tenue)" text-anchor="end">{{ g.fine }}</text>
    </svg>
  </div>
</template>
