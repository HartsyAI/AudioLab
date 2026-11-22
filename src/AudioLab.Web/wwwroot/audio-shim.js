// AudioLab audio shim. All logic is C#; this file only moves rendered chunks across two boundaries:
// in the audio worklet, from a message queue into the output, and in a worker, from the .NET runtime into a message port.
if (typeof AudioWorkletProcessor === 'function') {
  registerProcessor('audiolab-engine', class extends AudioWorkletProcessor {
    constructor(options) {
      super();
      this.cap = options.processorOptions.capacity;
      this.chunk = options.processorOptions.chunk;
      this.l = new Float32Array(this.cap);
      this.r = new Float32Array(this.cap);
      this.rd = 0; this.wr = 0; this.fill = 0; this.consumed = 0;
      this.primed = false; this.inRun = false;
      this.quanta = 0; this.counted = 0; this.events = 0; this.missed = 0; this.overflow = 0;
      this.minFill = this.cap; this.maxFill = 0; this.nextStats = 0;
      this.tx = this.port;
      this.port.onmessage = (e) => this.receive(e.data);
    }
    receive(d) {
      if (d instanceof MessagePort) { this.tx = d; d.onmessage = (e) => this.push(e.data); this.tx.postMessage(this.cap / this.chunk); }
      else if (d === 'go') this.tx.postMessage(this.cap / this.chunk);
      else this.push(d);
    }
    push(bytes) {
      const f = new Float32Array(bytes.buffer, bytes.byteOffset, bytes.byteLength >> 2), n = f.length >> 1;
      for (let i = 0; i < n; i++) { const w = (this.wr + i) % this.cap; this.l[w] = f[i]; this.r[w] = f[n + i]; }
      this.wr = (this.wr + n) % this.cap;
      this.fill += n;
      if (this.fill > this.cap) { this.overflow += this.fill - this.cap; this.fill = this.cap; }
      if (!this.primed && this.fill >= this.cap) this.primed = true;
    }
    process(inputs, outputs) {
      const L = outputs[0][0], R = outputs[0][1], n = L.length;
      this.quanta++;
      if (this.primed) {
        if (this.fill < this.minFill) this.minFill = this.fill;
        if (this.fill > this.maxFill) this.maxFill = this.fill;
        const got = this.fill < n ? this.fill : n;
        for (let i = 0; i < got; i++) { const p = (this.rd + i) % this.cap; L[i] = this.l[p]; R[i] = this.r[p]; }
        this.rd = (this.rd + got) % this.cap;
        this.fill -= got;
        this.counted += n;
        if (got < n) { this.missed += n - got; if (!this.inRun) { this.events++; this.inRun = true; } } else this.inRun = false;
        this.consumed += got;
        while (this.consumed >= this.chunk) { this.consumed -= this.chunk; this.tx.postMessage(1); }
      }
      if (currentFrame >= this.nextStats) {
        this.nextStats = currentFrame + (sampleRate >> 2);
        this.port.postMessage(JSON.stringify({ frame: currentFrame, time: currentTime, quanta: this.quanta, counted: this.counted,
          events: this.events, missed: this.missed, fill: this.fill, minFill: this.minFill, maxFill: this.maxFill,
          overflow: this.overflow, primed: this.primed }));
      }
      return true;
    }
  });
} else {
  let ex;
  self.onmessage = async ({ data: m }) => {
    if (typeof m === 'string') { ex.StallNext(+m.slice(6)); return; }
    if (m.port) {
      m.port.onmessage = (e) => { for (let i = 0; i < e.data; i++) { const b = ex.RenderChunk(); m.port.postMessage(b, [b.buffer]); } };
      return;
    }
    try {
      const { dotnet } = await import(m.runtime);
      const runtime = await dotnet.create();
      ex = (await runtime.getAssemblyExports('AudioLab.Audio.Worker.dll')).AudioLab.Web.Interop.Worker.WorkerExports;
      self.postMessage('ready:' + await ex.StartAsync(m.origin, m.rate, m.chunk, m.clips));
      setInterval(() => self.postMessage('engine:' + ex.ReadSnapshot()), 1000);
    } catch (err) { self.postMessage('error:' + err); }
  };
}
