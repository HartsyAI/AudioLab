using System.Diagnostics;
using System.Runtime;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        string cmd = args.Length > 0 ? args[0] : "enum";
        Options o = new(args.Skip(1).ToArray());
        switch (cmd)
        {
            case "enum":
                Enumerate.RunAsync().GetAwaiter().GetResult();
                return 0;
            case "selftest":
                return SelfTest();
            case "run":
                return Run(o);
            case "midi":
                return Midi.Run(o).GetAwaiter().GetResult();
            default:
                Console.WriteLine("commands: enum | selftest | run --lib asio|pa|ma|xt [--api ..] [--in ..] [--out ..] [--driver ..] [--buffer frames] [--rate hz] [--mode exclusive|shared] [--seconds n] [--emit] [--stress] | midi");
                return 1;
        }
    }

    private static int Run(Options o)
    {
        int rate = o.GetInt("rate", 48000);
        int seconds = o.GetInt("seconds", 20);
        bool emit = o.GetBool("emit");
        Workload w = new(rate, emit);
        string lib = o.Get("lib", "");
        IBackend backend = lib switch
        {
            "asio" => new NAudioAsioBackend(o, w),
            "pa" => new PortAudioBackend(o, w),
            "ma" => new MiniaudioBackend(o, w),
            "xt" => new XtBackend(o, w),
            _ => throw new ArgumentException("--lib asio|pa|ma|xt"),
        };
        Console.WriteLine(backend.Describe);
        int gc0 = GC.CollectionCount(0), gc1 = GC.CollectionCount(1), gc2 = GC.CollectionCount(2);
        long mem0 = GC.GetTotalAllocatedBytes();
        using CancellationTokenSource cts = new();
        Thread? stress = null;
        if (o.GetBool("stress"))
        {
            stress = new Thread(() =>
            {
                List<byte[]> keep = new();
                while (!cts.IsCancellationRequested)
                {
                    keep.Add(new byte[85_000]);
                    if (keep.Count > 200)
                    {
                        keep.Clear();
                    }

                    for (int i = 0; i < 2000; i++)
                    {
                        _ = new string('x', 100);
                    }

                    Thread.Sleep(1);
                }
            }) { IsBackground = true };
            stress.Start();
        }

        Stopwatch sw = Stopwatch.StartNew();
        while (sw.Elapsed.TotalSeconds < seconds)
        {
            Thread.Sleep(250);
        }

        cts.Cancel();
        backend.Dispose();
        string title = $"{backend.Describe} | {seconds}s emit={emit} stress={o.GetBool("stress")} gcMode={GCSettings.LatencyMode} server={GCSettings.IsServerGC}";
        Console.WriteLine(w.Report(title));
        Console.WriteLine($"process GC collections during run: gen0={GC.CollectionCount(0) - gc0} gen1={GC.CollectionCount(1) - gc1} gen2={GC.CollectionCount(2) - gc2}; allocated by process {(GC.GetTotalAllocatedBytes() - mem0) / 1e6:F1} MB");
        return 0;
    }

    /// <summary>Feeds the click train through a known delay in software to prove the measurement reads that delay back.</summary>
    private static int SelfTest()
    {
        foreach (int delay in new[] { 0, 37, 480, 1024 })
        {
            int rate = 48000;
            Workload w = new(rate, true);
            float[] ring = new float[8192];
            int pos = 0;
            float[] inBuf = new float[128];
            float[] outBuf = new float[128];
            for (int cb = 0; cb < rate * 4 / 128; cb++)
            {
                w.OnAudio(inBuf, 1, outBuf, 1, 128);
                for (int i = 0; i < 128; i++)
                {
                    ring[pos % ring.Length] = outBuf[i];
                    inBuf[i] = ring[(pos - delay + ring.Length) % ring.Length];
                    pos++;
                }
            }

            string r = w.Report("selftest delay=" + delay + " frames");
            Console.WriteLine($"expected {(delay + 128) / (double)rate * 1000:F2} ms (delay plus the one-callback offset of this fake loop)");
            Console.WriteLine(r);
        }

        return 0;
    }
}
