namespace TcFormat.Cli;

internal static class Program
{
    public static int Main(string[] args) =>
        CliApplication.Run(args, Console.In, Console.Out, Console.Error);
}
