namespace WebviewCore
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static int Main(string[] args)
        {
            if (args.Any(a => string.Equals(a, "--self-test", StringComparison.OrdinalIgnoreCase)))
                return CoreSelfTest.RunAsync(args).GetAwaiter().GetResult();

            ApplicationConfiguration.Initialize();
            Application.Run(new Form1());
            return 0;
        }
    }
}
