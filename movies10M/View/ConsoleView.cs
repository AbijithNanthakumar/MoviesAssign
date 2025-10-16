using System;

namespace View
{
    public class ConsoleView
    {
        public void ShowHeader()
        {
            Console.Clear();
            Console.WriteLine("=== MovieLens 10M Report Generator (MVC) ===");
            Console.WriteLine();
        }

        public string AskDatasetFolder()
        {
            return @"C:\Users\abijith.nanthakumar\Desktop\Movieass1\Movielensass1\movies10M\ml-10m\ml-10M100K";
        }

        public bool AskUseMultithreading()
        {
            Console.Write("Run with multithreading? (y/n) [n]: ");
            var r = Console.ReadLine()?.Trim().ToLower();
            return r == "y" || r == "yes";
        }

        public void ShowMessage(string msg)
        {
            Console.WriteLine(msg);
        }

        public void ShowExecutionTime(TimeSpan reportElapsed, TimeSpan totalElapsed)
        {
            Console.WriteLine();
            Console.WriteLine($"Report generation time: {reportElapsed.TotalSeconds:0.###} seconds");
            Console.WriteLine($"Total execution time   : {totalElapsed.TotalSeconds:0.###} seconds");
            Console.WriteLine();
        }
    }
}
