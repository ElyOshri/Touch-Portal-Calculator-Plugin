
using System;

namespace Calculator_Plugin
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine();
            var plugin = new Client();
            plugin.Run();
        }
    }
}
