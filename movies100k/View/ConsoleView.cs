using System;

namespace View
{
    public class ConsoleView
    {
        public void ShowHeader()
        {
            Console.Clear();
            Console.WriteLine("=== MovieLens 100K Report Generator (MVC) ===");
            Console.WriteLine();
        }

        public string AskDatasetFolder()
        {
          
            return @"C:\Users\abijith.nanthakumar\Desktop\Movieass1\Movielensass1\movies100k\ml-100k\ml-100k";
        }

        public bool AskUseMultithreading()
        {
            Console.Write("Run with multithreading? (y/n) [n]: ");
            string resp = Console.ReadLine().Trim().ToLower();
            if (resp == "y" || resp == "yes") return true;
            return false;
        }

        public void ShowMessage(string msg)
        {
            Console.WriteLine(msg);
        }

        public void ShowExecutionTime(TimeSpan elapsedForReports, TimeSpan totalElapsed)
        {
            Console.WriteLine();
            Console.WriteLine($"Report generation time: {elapsedForReports.TotalSeconds:0.###} seconds");
            Console.WriteLine($"Total execution time:    {totalElapsed.TotalSeconds:0.###} seconds");
            Console.WriteLine();
        }
    }
}
