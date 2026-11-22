using O9;

// usage: O9.Console [audioSeconds=30] [rounds=7]
double seconds = args.Length > 0 ? double.Parse(args[0]) : 30;
int rounds = args.Length > 1 ? int.Parse(args[1]) : 7;
Runner runner = new(Console.WriteLine, seconds, rounds);
runner.RunAll();
return runner.Failures;
