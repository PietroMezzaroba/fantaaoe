<script setup>
import { ref, onMounted, onUnmounted } from 'vue'
import { suonoHover } from '../suoni.js'

const progresso = ref(0)
const mostraBarra = ref(false)

// Cursore moderno (solo desktop con puntatore preciso)
const cursoreAttivo = ref(false)
const suInterattivo = ref(false)
const premuto = ref(false)
const puntoRef = ref(null)
const anelloRef = ref(null)

let mx = -100, my = -100
let ax = -100, ay = -100
let raf = 0
let ultimoTarget = null

const SELETTORE_INTERATTIVO = 'a, button, [role="button"], select, input[type="range"], input[type="checkbox"], summary'

function aggiornaScroll() {
  const max = document.documentElement.scrollHeight - window.innerHeight
  if (max <= 40) {
    mostraBarra.value = false
    progresso.value = 0
    return
  }
  mostraBarra.value = true
  progresso.value = Math.max(0, Math.min(1, window.scrollY / max))
}

function suMouseMove(e) {
  mx = e.clientX
  my = e.clientY
  if (puntoRef.value) {
    puntoRef.value.style.transform = `translate3d(${mx}px, ${my}px, 0)`
  }
  const el = e.target?.closest?.(SELETTORE_INTERATTIVO) || null
  suInterattivo.value = !!el
  if (el && el !== ultimoTarget) {
    ultimoTarget = el
    suonoHover()
  } else if (!el) {
    ultimoTarget = null
  }
}

function suMouseDown() { premuto.value = true }
function suMouseUp() { premuto.value = false }
function suMouseLeave() {
  mx = -100; my = -100
  if (puntoRef.value) puntoRef.value.style.transform = 'translate3d(-100px, -100px, 0)'
}

function animaAnello() {
  ax += (mx - ax) * 0.22
  ay += (my - ay) * 0.22
  if (anelloRef.value) {
    anelloRef.value.style.transform = `translate3d(${ax}px, ${ay}px, 0)`
  }
  raf = requestAnimationFrame(animaAnello)
}

onMounted(() => {
  aggiornaScroll()
  window.addEventListener('scroll', aggiornaScroll, { passive: true })
  window.addEventListener('resize', aggiornaScroll, { passive: true })

  const fine = window.matchMedia('(pointer: fine)').matches
  const riduci = window.matchMedia('(prefers-reduced-motion: reduce)').matches
  if (fine && !riduci) {
    cursoreAttivo.value = true
    document.documentElement.classList.add('cursore-moderno')
    window.addEventListener('pointermove', suMouseMove, { passive: true })
    window.addEventListener('pointerdown', suMouseDown, { passive: true })
    window.addEventListener('pointerup', suMouseUp, { passive: true })
    document.addEventListener('mouseleave', suMouseLeave)
    raf = requestAnimationFrame(animaAnello)
  }
})

onUnmounted(() => {
  window.removeEventListener('scroll', aggiornaScroll)
  window.removeEventListener('resize', aggiornaScroll)
  if (cursoreAttivo.value) {
    document.documentElement.classList.remove('cursore-moderno')
    window.removeEventListener('pointermove', suMouseMove)
    window.removeEventListener('pointerdown', suMouseDown)
    window.removeEventListener('pointerup', suMouseUp)
    document.removeEventListener('mouseleave', suMouseLeave)
    cancelAnimationFrame(raf)
  }
})
</script>

<template>
  <!-- Barra verticale di scorrimento sul lato destro -->
  <div v-if="mostraBarra" class="barra-scroll" aria-hidden="true">
    <div class="barra-scroll__traccia">
      <div class="barra-scroll__pieno" :style="{ transform: `scaleY(${progresso})` }"></div>
      <div class="barra-scroll__punto" :style="{ top: `${progresso * 100}%` }"></div>
    </div>
  </div>

  <!-- Cursore moderno Vantage (solo puntatore fine) -->
  <template v-if="cursoreAttivo">
    <div
      ref="anelloRef"
      class="cursore-anello"
      :class="{ 'cursore-anello--attivo': suInterattivo, 'cursore-anello--click': premuto }"
      aria-hidden="true"
    ></div>
    <div
      ref="puntoRef"
      class="cursore-punto"
      :class="{ 'cursore-punto--attivo': suInterattivo }"
      aria-hidden="true"
    ></div>
  </template>
</template>
