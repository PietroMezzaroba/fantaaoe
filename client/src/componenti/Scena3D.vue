<script setup>
import { ref, onMounted, onUnmounted } from 'vue'
import * as THREE from 'three'
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js'
import { suonoRilancio } from '../suoni.js'

const props = defineProps({
  modello: { type: String, required: true },
  didascalia: { type: String, default: '' },
  distanza: { type: Number, default: 4.3 },
  altezza: { type: Number, default: 1.55 },
  rotazioneIniziale: { type: Number, default: 0.35 },
})

const boxRef = ref(null)
const pronto = ref(false)
let renderer = null
let raf = 0
let visibile = true
let io = null
let ro = null
let speedBoost = 1

function suClick() {
  speedBoost = 2.6
  suonoRilancio()
}

onMounted(() => {
  const el = boxRef.value
  if (!el) return

  const riduci = window.matchMedia('(prefers-reduced-motion: reduce)').matches
  const scene = new THREE.Scene()

  const camera = new THREE.PerspectiveCamera(34, el.clientWidth / Math.max(1, el.clientHeight), 0.1, 40)
  camera.position.set(0, props.altezza, props.distanza)
  camera.lookAt(0, 0.72, 0)

  renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true, powerPreference: 'low-power' })
  renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2))
  renderer.setSize(el.clientWidth, el.clientHeight)
  renderer.outputColorSpace = THREE.SRGBColorSpace
  renderer.toneMapping = THREE.ACESFilmicToneMapping
  renderer.toneMappingExposure = 1.15
  el.appendChild(renderer.domElement)

  // Illuminazione tattica Vantage: luce fredda + rim verde e rosso
  scene.add(new THREE.HemisphereLight(0xd8e8ff, 0x162230, 1.05))
  const key = new THREE.DirectionalLight(0xffffff, 1.8)
  key.position.set(3.5, 6, 4)
  scene.add(key)

  const rimVerde = new THREE.PointLight(0x4be096, 3.2, 10)
  rimVerde.position.set(-2.6, 2.2, -1.6)
  scene.add(rimVerde)

  const rimRosso = new THREE.PointLight(0xe56b6f, 2.4, 10)
  rimRosso.position.set(2.6, 1.8, -1.6)
  scene.add(rimRosso)

  const pivot = new THREE.Group()
  pivot.rotation.y = props.rotazioneIniziale
  scene.add(pivot)

  let mixer = null
  let targetRotY = props.rotazioneIniziale
  let targetRotX = 0.08

  const loader = new GLTFLoader()
  loader.load(props.modello, (gltf) => {
    const obj = gltf.scene
    // Centra orizzontalmente
    const box = new THREE.Box3().setFromObject(obj)
    const centro = box.getCenter(new THREE.Vector3())
    obj.position.x -= centro.x
    obj.position.z -= centro.z
    obj.position.y -= box.min.y
    pivot.add(obj)

    if (gltf.animations?.length) {
      mixer = new THREE.AnimationMixer(obj)
      gltf.animations.forEach((clip) => {
        const action = mixer.clipAction(clip)
        action.play()
      })
    }
    pronto.value = true
    if (riduci) renderer.render(scene, camera)
  })

  function suPointerMove(e) {
    const rect = el.getBoundingClientRect()
    const nx = ((e.clientX - rect.left) / rect.width) * 2 - 1
    const ny = ((e.clientY - rect.top) / rect.height) * 2 - 1
    targetRotY = props.rotazioneIniziale + nx * 0.55
    targetRotX = 0.08 + ny * 0.18
  }
  function suPointerLeave() {
    targetRotY = props.rotazioneIniziale
    targetRotX = 0.08
  }
  el.addEventListener('pointermove', suPointerMove, { passive: true })
  el.addEventListener('pointerleave', suPointerLeave, { passive: true })

  ro = new ResizeObserver(() => {
    if (!el || !renderer) return
    const w = el.clientWidth
    const h = Math.max(1, el.clientHeight)
    camera.aspect = w / h
    camera.updateProjectionMatrix()
    renderer.setSize(w, h)
    if (riduci) renderer.render(scene, camera)
  })
  ro.observe(el)

  io = new IntersectionObserver((entries) => {
    visibile = entries[0]?.isIntersecting ?? true
  }, { threshold: 0.05 })
  io.observe(el)

  const clock = new THREE.Clock()
  function loop() {
    raf = requestAnimationFrame(loop)
    if (!visibile || !renderer) return
    const dt = Math.min(clock.getDelta(), 0.05)
    speedBoost += (1 - speedBoost) * 0.05
    if (mixer && !riduci) mixer.update(dt * speedBoost)
    pivot.rotation.y += (targetRotY - pivot.rotation.y) * 0.08
    pivot.rotation.x += (targetRotX - pivot.rotation.x) * 0.08
    renderer.render(scene, camera)
  }
  if (!riduci) loop()

  onUnmounted(() => {
    cancelAnimationFrame(raf)
    ro?.disconnect()
    io?.disconnect()
    el.removeEventListener('pointermove', suPointerMove)
    el.removeEventListener('pointerleave', suPointerLeave)
    if (renderer) {
      renderer.dispose()
      renderer.domElement?.remove()
      renderer = null
    }
  })
})
</script>

<template>
  <figure class="diorama angoli" :class="{ 'diorama--pronto': pronto }" @click="suClick" title="Muovi il mouse o clicca">
    <div ref="boxRef" class="diorama__canvas"></div>
    <figcaption v-if="didascalia" class="diorama__cap piccolo muto">{{ didascalia }}</figcaption>
  </figure>
</template>
