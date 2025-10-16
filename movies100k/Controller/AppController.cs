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
            var swTotal = Stopwatch.StartNew();
            MovieLensData data;
            try
            {
                data = MovieLensData.LoadFromFolder(datasetFolder);
            }
            catch (Exception ex)
            {
                _view.ShowMessage($"Error loading dataset: {ex.Message}");
                return;
            }

            var reportGenerator = new ReportGenerator(data);

            string outputRoot = "Output";
            string sub = useMultithreading ? "withMultithreading" : "withoutMultithreading";
            string outputFolder = System.IO.Path.Combine(outputRoot, sub);

            _view.ShowMessage($"Generating reports ({(useMultithreading ? "multithreaded" : "single-threaded")}) to folder: {outputFolder}");

            var sw = Stopwatch.StartNew();
            if (useMultithreading)
            {
                reportGenerator.GenerateAllReportsMultiThreaded(outputFolder);
            }
            else
            {
                reportGenerator.GenerateAllReportsSingleThreaded(outputFolder);
            }
            sw.Stop();
            swTotal.Stop();

            _view.ShowExecutionTime(sw.Elapsed, swTotal.Elapsed);
            _view.ShowMessage($"Reports written to {outputFolder}");
            _view.ShowMessage("Done. Press Enter to exit.");
            Console.ReadLine();
        }
    }
}
