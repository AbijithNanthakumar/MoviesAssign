using System;
using System.Diagnostics;
using Model;
using View;

namespace Controller
{
    public class AppController
    {
        private readonly ConsoleView _view = new ConsoleView();

        public void Run()
        {
            _view.ShowHeader();

            string datasetFolder = _view.AskDatasetFolder();
            if (string.IsNullOrWhiteSpace(datasetFolder))
            {
                _view.ShowMessage("Dataset path cannot be empty. Exiting.");
                return;
            }

            bool useMultithreading = _view.AskUseMultithreading();

            _view.ShowMessage("Loading dataset...");
            var totalSw = Stopwatch.StartNew();
            MovieLensData data;
            try
            {
                data = MovieLensData.LoadFromFolder(datasetFolder);
            }
            catch (Exception ex)
            {
                _view.ShowMessage($"Error: {ex.Message}");
                return;
            }

            var generator = new ReportGenerator(data);

            string outputRoot = "Output";
            string sub = useMultithreading ? "withMultithreading" : "withoutMultithreading";
            string outputFolder = System.IO.Path.Combine(outputRoot, sub);

            _view.ShowMessage($"Generating reports to: {outputFolder}");
            var sw = Stopwatch.StartNew();

            if (useMultithreading)
                generator.GenerateAllReportsMultiThreaded(outputFolder);
            else
                generator.GenerateAllReportsSingleThreaded(outputFolder);

            sw.Stop();
            totalSw.Stop();

            _view.ShowExecutionTime(sw.Elapsed, totalSw.Elapsed);
            _view.ShowMessage($"Reports are available in: {outputFolder}");
            _view.ShowMessage("Press Enter to exit.");
            Console.ReadLine();
        }
    }
}
