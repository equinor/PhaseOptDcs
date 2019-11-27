using System;
using System.Globalization;
using Topshelf;

namespace PhaseOptDcs
{
    class Program
    {
        static void Main()
        {
            var exitCode = HostFactory.Run(x =>
            {
                x.Service<PhaseOptDcsService>(s =>
                {
                    s.ConstructUsing(PhaseOptDcsService => new PhaseOptDcsService());
                    s.WhenStarted(PhaseOptDcsService => PhaseOptDcsService.Start());
                    s.WhenStopped(PhaseOptDcsService => PhaseOptDcsService.Stop());
                });

                x.SetServiceName("PhaseOptDcs");
            });

            int exitCodeValue = (int)Convert.ChangeType(exitCode, exitCode.GetTypeCode(), CultureInfo.InvariantCulture);
            Environment.ExitCode = exitCodeValue;
        }
    }
}
