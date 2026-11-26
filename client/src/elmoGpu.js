// Motore WebGPU dell'elmo a punti (NuvolaElmo.vue).
// La fisica dei punti (ricomposizione, rotazione, prospettiva, spinta del puntatore e del Paladino) gira in un compute shader,
// un punto per thread; il disegno usa un quadrato per punto (istanze) con un disco morbido. Un punto che si muove veloce
// diventa una scia lunga quanto il suo spostamento nell'ultimo fotogramma: così l'ingresso e la carica del Paladino lasciano
// strisce, mentre l'elmo fermo resta fatto di punti netti.
// Se il browser non ha WebGPU (o qualcosa va storto) creaElmoGpu restituisce null e il componente disegna in 2D come prima.

const WGSL = /* wgsl */ `
struct Uniformi {
  dim: vec4f,       // larghezza, altezza (px CSS), dpr, numero di punti
  vista: vec4f,     // cos e sin dell'imbardata, cos e sin del beccheggio
  giro: vec4f,      // cos e sin del giro d'ingresso, quanto sono ancora sparsi (0..1), scala
  centro: vec4f,    // centro x, centro y, distanza focale, scia (0 = niente scia)
  puntatore: vec4f, // x, y, attivo, raggio
  cavallo: vec4f,   // x, y, attivo, raggio
  scan: vec4f,      // posizione del piano, attivo, smorzamento, azzera (1 = spostamenti e scie a zero)
  colori: array<vec4f, 7>, // lo, mid, hi, verde, bianco, rosso, luce (rgb sRGB 0..1, alfa)
}

@group(0) @binding(0) var<uniform> u: Uniformi;
@group(0) @binding(1) var<storage, read> punti: array<vec4f>;      // x, y, z, colore (0 grigio, 1 verde, 2 bianco, 3 rosso)
@group(0) @binding(2) var<storage, read> sparsi: array<vec4f>;     // da dove arrivano all'apertura
@group(0) @binding(3) var<storage, read_write> stato: array<vec4f>; // spostamento xy, posizione del fotogramma prima zw
@group(0) @binding(4) var<storage, read_write> uscita: array<vec4f>; // x, y, lato, codice colore
@group(0) @binding(5) var<storage, read_write> scia: array<vec2f>;   // spostamento a schermo dall'ultimo fotogramma

@compute @workgroup_size(64)
fn passo(@builtin(global_invocation_id) id: vec3u) {
  let i = id.x;
  if (i >= u32(u.dim.w)) { return; }
  let p = punti[i];
  let s = sparsi[i];
  let sp = u.giro.z;
  let x0 = p.x + s.x * sp;
  let y = p.y + s.y * sp;
  let z0 = p.z + s.z * sp;
  // durante l'ingresso l'elmo gira su se stesso
  let x = x0 * u.giro.x + z0 * u.giro.y;
  let z = -x0 * u.giro.y + z0 * u.giro.x;
  // imbardata poi beccheggio
  let x1 = x * u.vista.x + z * u.vista.y;
  let z1 = -x * u.vista.y + z * u.vista.x;
  let y1 = y * u.vista.z - z1 * u.vista.w;
  let z2 = y * u.vista.w + z1 * u.vista.z;
  let f = u.centro.z;
  let prosp = f / (f + z2 * 0.6);
  let base = vec2f(u.centro.x + x1 * u.giro.w * prosp, u.centro.y + y1 * u.giro.w * prosp);

  var st = stato[i];
  var d = st.xy;
  if (u.scan.w > 0.5) { d = vec2f(0.0); }
  // il puntatore scosta i punti vicini, che poi tornano a posto
  if (u.puntatore.z > 0.5) {
    let dv = base - u.puntatore.xy;
    let d2 = dot(dv, dv);
    let r = u.puntatore.w;
    if (d2 < r * r && d2 > 0.01) {
      let dd = sqrt(d2);
      d += dv / dd * (1.0 - dd / r) * 28.0 * 0.18;
    }
  }
  // il Paladino che sfonda l'elmo sparpaglia le particelle dal petto del cavallo
  if (u.cavallo.z > 0.5) {
    let dv = base - vec2f(u.cavallo.x - 22.0, u.cavallo.y);
    let d2 = dot(dv, dv);
    let r = u.cavallo.w;
    if (d2 < r * r && d2 > 0.01) {
      let dd = sqrt(d2);
      let forza = (1.0 - dd / r) * 72.0;
      d += vec2f(dv.x / dd * 0.8 - 0.5, dv.y / dd * 0.95 - 0.1) * forza * 0.22;
    }
  }
  d *= u.scan.z;
  let pos = base + d;
  var prima = st.zw;
  if (u.scan.w > 0.5) { prima = pos; }
  stato[i] = vec4f(d, pos);
  scia[i] = (pos - prima) * u.centro.w;

  let c = p.w;
  var lato = max(1.5, 2.3 * prosp * min(1.6, u.giro.w * 3.4));
  if (c > 0.5) { lato *= 1.25; }
  var codice = 0.0;
  if (c > 2.5) { codice = 5.0; }
  else if (c > 1.5) { codice = 4.0; }
  else if (c > 0.5) { codice = 3.0; }
  else if (u.scan.y > 0.5 && abs(x - u.scan.x) < 38.0) { codice = 6.0; }
  else if (z > 35.0) { codice = 2.0; }
  else if (z > -20.0) { codice = 1.0; }
  uscita[i] = vec4f(pos, lato, codice);
}

struct Frammento {
  @builtin(position) pos: vec4f,
  @location(0) locale: vec2f,     // px lungo la scia (x) e di traverso (y), rispetto al centro della scia
  @location(1) colore: vec4f,
  @location(2) misure: vec3f,     // mezza lunghezza della scia, raggio del punto, alone (0..1)
}

@group(0) @binding(4) var<storage, read> disegno: array<vec4f>;
@group(0) @binding(5) var<storage, read> scieLette: array<vec2f>;

@vertex
fn vertice(@builtin(vertex_index) v: u32, @builtin(instance_index) i: u32) -> Frammento {
  var angoli = array<vec2f, 6>(vec2f(-1.0, -1.0), vec2f(1.0, -1.0), vec2f(-1.0, 1.0), vec2f(-1.0, 1.0), vec2f(1.0, -1.0), vec2f(1.0, 1.0));
  let o = disegno[i];
  let codice = u32(o.w);
  let raggio = o.z * 0.6;
  // i punti del tricolore e quelli accesi dalla scansione hanno un alone tenue
  var alone = 0.0;
  if (codice == 3u || codice == 5u) { alone = 0.55; }
  if (codice == 6u) { alone = 1.0; }
  let margine = raggio * (1.0 + alone * 2.2) + 1.0;
  let sc = scieLette[i];
  let lung = min(length(sc), 48.0);
  var asse = vec2f(1.0, 0.0);
  if (lung > 0.35) { asse = normalize(sc); }
  let traverso = vec2f(-asse.y, asse.x);
  let mezza = select(0.0, lung * 0.5, lung > 0.35);
  // la scia va dalla posizione di prima a quella di adesso: il centro sta a metà
  let centro = o.xy - asse * mezza;
  let a = angoli[v];
  let locale = vec2f(a.x * (mezza + margine), a.y * margine);
  let px = centro + asse * locale.x + traverso * locale.y;
  var f: Frammento;
  f.pos = vec4f(px.x / u.dim.x * 2.0 - 1.0, 1.0 - px.y / u.dim.y * 2.0, 0.0, 1.0);
  f.locale = locale;
  f.colore = u.colori[codice];
  f.misure = vec3f(mezza, raggio, alone);
  return f;
}

@fragment
fn frammento(f: Frammento) -> @location(0) vec4f {
  let mezza = f.misure.x;
  let raggio = f.misure.y;
  // distanza dal segmento della scia: un disco se la scia è nulla, una capsula se il punto corre
  let q = vec2f(max(abs(f.locale.x) - mezza, 0.0), f.locale.y);
  let d = length(q);
  let bordo = 0.6 / u.dim.z + 0.35;
  var a = 1.0 - smoothstep(raggio - bordo, raggio + bordo, d);
  // la coda della scia sbiadisce: piena sulla testa (x > 0), un terzo sulla coda
  if (mezza > 0.0) {
    let t = clamp((f.locale.x + mezza) / (2.0 * mezza), 0.0, 1.0);
    a *= mix(0.3, 1.0, t * t);
  }
  let r2 = d / max(raggio, 0.5);
  a = max(a, f.misure.z * 0.32 * exp(-r2 * r2 * 0.5));
  a *= f.colore.a;
  return vec4f(f.colore.rgb * a, a);
}
`

// da un colore CSS qualsiasi (anche oklch) a rgb 0..1, passando da un canvas 2D di un pixel
function convertitore() {
  const c = document.createElement('canvas')
  c.width = c.height = 1
  const g = c.getContext('2d', { willReadFrequently: true })
  return (css, alfa = 1) => {
    g.clearRect(0, 0, 1, 1)
    g.fillStyle = '#000'
    g.fillStyle = css || '#888'
    g.fillRect(0, 0, 1, 1)
    const [r, gg, b] = g.getImageData(0, 0, 1, 1).data
    return [r / 255, gg / 255, b / 255, alfa]
  }
}

/**
 * Prova ad avviare WebGPU sulla tela. Restituisce null se non si può (il componente passa al 2D).
 * @param {HTMLCanvasElement} tela
 * @param {{ n: number, dati: Int16Array, sparsi: Float32Array, perso: () => void }} opz
 */
export async function creaElmoGpu(tela, { n, dati, sparsi, perso }) {
  // ?motore=2d nell'indirizzo forza il disegno 2D (per confrontare i due motori)
  if (!navigator.gpu || new URLSearchParams(location.search).get('motore') === '2d') return null
  let device
  try {
    const adattatore = await navigator.gpu.requestAdapter({ powerPreference: 'low-power' })
    if (!adattatore) return null
    device = await adattatore.requestDevice()
  } catch {
    return null
  }
  const ctx = tela.getContext('webgpu')
  if (!ctx) {
    device.destroy()
    return null
  }
  const formato = navigator.gpu.getPreferredCanvasFormat()
  ctx.configure({ device, format: formato, alphaMode: 'premultiplied' })

  let distrutto = false
  device.lost.then((info) => {
    if (!distrutto && info.reason !== 'destroyed') perso?.()
  })

  const modulo = device.createShaderModule({ code: WGSL })
  const usoStorage = GPUBufferUsage.STORAGE | GPUBufferUsage.COPY_DST

  const bufPunti = device.createBuffer({ size: n * 16, usage: usoStorage })
  const bufSparsi = device.createBuffer({ size: n * 16, usage: usoStorage })
  const bufStato = device.createBuffer({ size: n * 16, usage: usoStorage })
  const bufUscita = device.createBuffer({ size: n * 16, usage: GPUBufferUsage.STORAGE })
  const bufScia = device.createBuffer({ size: n * 8, usage: GPUBufferUsage.STORAGE })
  const bufUniformi = device.createBuffer({ size: 224, usage: GPUBufferUsage.UNIFORM | GPUBufferUsage.COPY_DST })

  const p = new Float32Array(n * 4)
  const s = new Float32Array(n * 4)
  for (let i = 0; i < n; i++) {
    p.set([dati[i * 4], dati[i * 4 + 1], dati[i * 4 + 2], dati[i * 4 + 3]], i * 4)
    s.set([sparsi[i * 3], sparsi[i * 3 + 1], sparsi[i * 3 + 2], 0], i * 4)
  }
  device.queue.writeBuffer(bufPunti, 0, p)
  device.queue.writeBuffer(bufSparsi, 0, s)

  const pipelineCalcolo = device.createComputePipeline({ layout: 'auto', compute: { module: modulo, entryPoint: 'passo' } })
  const pipelineDisegno = device.createRenderPipeline({
    layout: 'auto',
    vertex: { module: modulo, entryPoint: 'vertice' },
    fragment: {
      module: modulo,
      entryPoint: 'frammento',
      targets: [{
        format: formato,
        blend: {
          color: { srcFactor: 'one', dstFactor: 'one-minus-src-alpha', operation: 'add' },
          alpha: { srcFactor: 'one', dstFactor: 'one-minus-src-alpha', operation: 'add' },
        },
      }],
    },
    primitive: { topology: 'triangle-list' },
  })
  const gruppoCalcolo = device.createBindGroup({
    layout: pipelineCalcolo.getBindGroupLayout(0),
    entries: [bufUniformi, bufPunti, bufSparsi, bufStato, bufUscita, bufScia].map((buffer, binding) => ({ binding, resource: { buffer } })),
  })
  const gruppoDisegno = device.createBindGroup({
    layout: pipelineDisegno.getBindGroupLayout(0),
    entries: [
      { binding: 0, resource: { buffer: bufUniformi } },
      { binding: 4, resource: { buffer: bufUscita } },
      { binding: 5, resource: { buffer: bufScia } },
    ],
  })

  const uni = new Float32Array(56)
  const colore = convertitore()
  let azzeraProssimo = true

  return {
    motore: 'webgpu',
    // colori dai token del tema (getComputedStyle), come nel disegno 2D
    colori(c) {
      const lista = [colore(c.lo, 0.75), colore(c.mid, 0.9), colore(c.hi), colore(c.verde), colore(c.bianco), colore(c.rosso), colore(c.hi)]
      lista.forEach((v, k) => uni.set(v, 28 + k * 4))
    },
    dimensiona(larghezza, altezza, dpr) {
      tela.width = Math.max(1, Math.round(larghezza * dpr))
      tela.height = Math.max(1, Math.round(altezza * dpr))
      azzeraProssimo = true // dopo un cambio di misura le posizioni di prima non valgono: niente scie finte
    },
    azzera() { azzeraProssimo = true },
    /** f: tutto quello che il disegno 2D calcola prima del ciclo sui punti */
    disegna(f) {
      if (distrutto) return
      uni.set([f.larghezza, f.altezza, f.dpr, n], 0)
      uni.set([f.cy, f.sy, f.cx, f.sx], 4)
      uni.set([f.cg, f.sg, 1 - f.raccolta, f.scala], 8)
      uni.set([f.ox, f.oy, f.focale, f.scia], 12)
      uni.set([f.puntatoreX, f.puntatoreY, f.puntatoreAttivo ? 1 : 0, f.raggio], 16)
      uni.set([f.cavalloX, f.cavalloY, f.cavalloAttivo ? 1 : 0, f.raggioCavallo], 20)
      uni.set([f.piano, f.scansioneAttiva ? 1 : 0, 0.93, azzeraProssimo || f.azzera ? 1 : 0], 24)
      azzeraProssimo = false
      device.queue.writeBuffer(bufUniformi, 0, uni)

      const enc = device.createCommandEncoder()
      const calcolo = enc.beginComputePass()
      calcolo.setPipeline(pipelineCalcolo)
      calcolo.setBindGroup(0, gruppoCalcolo)
      calcolo.dispatchWorkgroups(Math.ceil(n / 64))
      calcolo.end()
      const passata = enc.beginRenderPass({
        colorAttachments: [{ view: ctx.getCurrentTexture().createView(), loadOp: 'clear', storeOp: 'store', clearValue: { r: 0, g: 0, b: 0, a: 0 } }],
      })
      passata.setPipeline(pipelineDisegno)
      passata.setBindGroup(0, gruppoDisegno)
      passata.draw(6, n)
      passata.end()
      device.queue.submit([enc.finish()])
    },
    distruggi() {
      distrutto = true
      device.destroy()
    },
  }
}
