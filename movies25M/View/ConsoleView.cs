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
            return @"C:\Users\abijith.nanthakumar\Desktop\Movieass1\Movielensass1\movies25M\ml-25m\ml-25m";
        }

        public bool AskUseMultithreading()
        {
            Console.Write("Run with multithreading? (y/n) [n]: ");
            var input = Console.ReadLine()?.Trim().ToLower();
            return input == "y" || input == "yes";
        }

        public void ShowMessage(string msg) => Console.WriteLine(msg);

        public void ShowExecutionTime(TimeSpan reportTime, TimeSpan totalTime)
        {
            Console.WriteLine();
            Console.WriteLine($"Report generation time: {reportTime.TotalSeconds:0.###} s");
            Console.WriteLine($"Total execution time  : {totalTime.TotalSeconds:0.###} s");
            Console.WriteLine();
        }
    }
}
