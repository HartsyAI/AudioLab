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
      this.wr = 0; this.rd = 0; this.fill = 0; this.consumed = 0;
      this.primed = false; this.inRun = false; this.events = 0; this.missed = 0; this.minFill = capacity; this.next = 0;
      this.tx = null;
      this.port.onmessage = ({ data }) => {
        this.tx = data;
        data.onmessage = (e) => this.push(e.data);
        data.postMessage(this.slots);
      };
    }
    push(bytes) {
      const at = (this.wr % this.slots) * this.chunk;
      const samples = new Float32Array(bytes.buffer, bytes.byteOffset + 16, this.chunk * 2);
      this.l.set(samples.subarray(0, this.chunk), at);
      this.r.set(samples.subarray(this.chunk), at);
      const head = new DataView(bytes.buffer, bytes.byteOffset, 16);
      this.starts[this.wr % this.slots] = head.getFloat64(0, true);
      this.live[this.wr % this.slots] = head.getInt32(8, true) & 1;
      this.wr++; this.fill += this.chunk;
      if (!this.primed && this.fill >= this.cap) this.primed = true;
    }
    process(inputs, outputs) {
      const L = outputs[0][0], R = outputs[0][1], n = L.length;
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
        this.next = currentFrame + 1024;
        this.port.postMessage(JSON.stringify({
          position: this.starts[Math.floor(this.rd / this.chunk)] + (this.rd % this.chunk), fill: this.fill, minFill: this.minFill,
          events: this.events, missed: this.missed, primed: this.primed }));
      }
      return true;
    }
  });
} else {
  (async () => {
    const { dotnet } = await import(new URL('worker/_framework/dotnet.js', self.location).href);
    const runtime = await dotnet.create();
    const ex = (await runtime.getAssemblyExports('AudioLab.Audio.Worker.dll')).AudioLab.Web.Interop.Worker.WorkerExports;
    let port = null;
    // The runtime hangs if onmessage is set before create() has finished, so it is set last.
    self.onmessage = async ({ data: m }) => {
      if (m.port) {
        port = m.port;
        port.onmessage = (e) => { for (let i = 0; i < e.data; i++) { const b = ex.RenderChunk(); port.postMessage(b, [b.buffer]); } };
        return;
      }
      try { await ex[m.call](...m.args); self.postMessage({ id: m.id }); } catch (err) { self.postMessage({ id: m.id, error: String(err) }); }
    };
    setInterval(() => { const status = ex.ReadStatus(); if (status) self.postMessage({ status }); }, 40);
    self.postMessage({ booted: true });
  })().catch((err) => self.postMessage({ error: String(err) }));
}
