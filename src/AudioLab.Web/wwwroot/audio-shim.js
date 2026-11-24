// AudioLab audio shim. All logic is C#; this file moves data across two boundaries it cannot cross itself:
// in the audio worklet, rendered chunks from a message port into the output; in the worker, the .NET runtime that hosts the engine.
if (typeof AudioWorkletProcessor === 'function') {
  registerProcessor('audiolab-engine', class extends AudioWorkletProcessor {
    constructor(options) {
      super();
      const { capacity, chunk } = options.processorOptions;
      this.cap = capacity; this.chunk = chunk; this.slots = capacity / chunk;
      this.l = new Float32Array(capacity); this.r = new Float32Array(capacity);
      this.starts = new Float64Array(this.slots); this.live = new Uint8Array(this.slots); this.wasLive = false;
      this.wr = 0; this.rd = 0; this.fill = 0; this.consumed = 0; this.primed = false; this.inRun = false;
      this.events = 0; this.missed = 0; this.minFill = capacity; this.overflow = 0;
      // [position, fill, minFill, events, missed, primed, frames]: one array, reused, so a report allocates only what the message clone does.
      this.stats = new Float64Array(7); this.next = 0; this.every = 0; this.tx = null;
      this.port.onmessage = ({ data }) => {
        this.tx = data;
        data.onmessage = (e) => this.push(e.data);
        data.postMessage(this.slots);
      };
    }
    push(bytes) {
      // A chunk is [position, audible] as two doubles, then the left channel and the right as floats.
      if (this.fill + this.chunk > this.cap) { this.overflow++; return; }
      const head = new Float64Array(bytes.buffer, bytes.byteOffset, 2);
      const f = new Float32Array(bytes.buffer, bytes.byteOffset + 16, this.chunk * 2);
      const slot = this.wr % this.slots, at = slot * this.chunk, n = this.chunk;
      for (let i = 0; i < n; i++) { this.l[at + i] = f[i]; this.r[at + i] = f[n + i]; }
      this.starts[slot] = head[0]; this.live[slot] = head[1] === 1 ? 1 : 0;
      this.wr++; this.fill += n;
      if (!this.primed && this.fill >= this.cap) this.primed = true;
    }
    process(inputs, outputs) {
      const L = outputs[0][0], R = outputs[0][1], n = L.length;
      if (this.every === 0) this.every = Math.round(sampleRate / 25);
      if (this.primed) {
        if (this.fill < this.minFill) this.minFill = this.fill;
        const got = Math.min(this.fill, n);
        // Silence the engine chose to play (stopped, paused, no plan yet) is not a dropout; only audio that was due is.
        if (got > 0) this.wasLive = this.live[Math.floor(this.rd / this.chunk)] === 1;
        for (let i = 0; i < got; i++) { const p = (this.rd + i) % this.cap; L[i] = this.l[p]; R[i] = this.r[p]; }
        this.rd = (this.rd + got) % this.cap; this.fill -= got; this.consumed += got;
        if (got < n && this.wasLive) { this.missed += n - got; if (!this.inRun) { this.events++; this.inRun = true; } } else this.inRun = false;
        while (this.consumed >= this.chunk) { this.consumed -= this.chunk; this.tx.postMessage(1); }
      }
      if (currentFrame >= this.next) {
        this.next = currentFrame + this.every;
        const s = this.stats;
        s[0] = this.starts[Math.floor(this.rd / this.chunk)] + (this.rd % this.chunk); s[1] = this.fill; s[2] = this.minFill;
        s[3] = this.events; s[4] = this.missed; s[5] = this.primed ? 1 : 0; s[6] = currentFrame;
        this.port.postMessage(s);
      }
      return true;
    }
  });
} else {
  (async () => {
    const { dotnet } = await import(new URL('worker/_framework/dotnet.js', self.location).href);
    const runtime = await dotnet.create();
    const ex = (await runtime.getAssemblyExports('AudioLab.Audio.Worker.dll')).AudioLab.Web.Interop.Worker.WorkerExports;
    const calls = ['Start', 'SetDocument', 'Play', 'Pause', 'Stop', 'Seek', 'SetLoop'];
    let port = null;
    // The runtime hangs if onmessage is set before create() has finished, so it is set last.
    self.onmessage = async ({ data: m }) => {
      if (m.port) {
        port = m.port;
        port.onmessage = (e) => { for (let i = 0; i < e.data; i++) { const b = ex.RenderChunk(); port.postMessage(b, [b.buffer]); } };
        return;
      }
      if (!calls.includes(m.call)) { self.postMessage({ id: m.id, error: 'No such call: ' + m.call }); return; }
      try { await ex[m.call](...m.args); self.postMessage({ id: m.id }); } catch (err) { self.postMessage({ id: m.id, error: String(err) }); }
    };
    setInterval(() => { const status = ex.ReadStatus(); if (status) self.postMessage({ status }); }, 40);
    self.postMessage({ booted: true });
  })().catch((err) => self.postMessage({ error: String(err) }));
}
